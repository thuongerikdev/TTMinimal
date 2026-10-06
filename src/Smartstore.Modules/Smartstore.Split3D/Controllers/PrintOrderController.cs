#nullable enable

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Security;
using Smartstore.Core.Web;
using Smartstore.Utilities;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// The "Đặt in" pages: takes the models weighed in the price calculator, collects where the print has to go
/// and hands the job over to the checkout, which collects the deposit. Jobs the studio created from a quote
/// request are paid through their token link.
/// </summary>
[Route("dat-in")]
public class PrintOrderController : PublicController
{
    // Upper bound for the request body; the configured limit (StudioSettings.QuoteMaxFileSizeMb) is checked in the action.
    private const long MaxRequestSize = 520L * 1024 * 1024;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SmartDbContext _db;
    private readonly PrintOrderService _printOrderService;
    private readonly IShoppingCartService _cartService;
    private readonly IWebHelper _webHelper;
    private readonly StudioSettings _settings;

    public PrintOrderController(
        SmartDbContext db,
        PrintOrderService printOrderService,
        IShoppingCartService cartService,
        IWebHelper webHelper,
        StudioSettings settings)
    {
        _db = db;
        _printOrderService = printOrderService;
        _cartService = cartService;
        _webHelper = webHelper;
        _settings = settings;
    }

    private long MaxFileSize => Math.Clamp(_settings.QuoteMaxFileSizeMb, 1, 500) * 1024L * 1024L;

    /// <summary>
    /// Creates a print job from the price calculator and puts it into the cart. Called by studio.js with the
    /// weighed models and their files.
    /// </summary>
    [HttpPost("them")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
    public async Task<IActionResult> Add(
        [FromForm] string? models,
        [FromForm] string? technology,
        [FromForm] string? material,
        [FromForm] string? fill,
        List<IFormFile>? modelFile)
    {
        List<PrintOrderModel>? parsed = null;
        if (models.HasValue())
        {
            try
            {
                parsed = JsonSerializer.Deserialize<List<PrintOrderModel>>(models!, _jsonOptions);
            }
            catch (JsonException)
            {
            }
        }

        var sanitized = PrintOrderService.SanitizeModels(parsed);
        if (sanitized.Count == 0)
        {
            return Json(new { ok = false, error = "Chưa có mô hình nào để đặt in. Hãy thả file 3D vào bộ tính giá." });
        }

        var files = (modelFile ?? []).Where(x => x != null && x.Length > 0).ToList();
        if (files.FirstOrDefault(x => !PrintQuoteService.IsAllowedFile(x.FileName)) is { } badFile)
        {
            return Json(new { ok = false, error = $"Định dạng file {badFile.FileName} chưa được hỗ trợ." });
        }

        if (files.Sum(x => x.Length) > MaxFileSize)
        {
            return Json(new { ok = false, error = $"File vượt quá {_settings.QuoteMaxFileSizeMb} MB. Hãy gửi yêu cầu báo giá kèm link tải file." });
        }

        var customer = Services.WorkContext.CurrentCustomer;
        var job = new PrintOrder
        {
            CustomerId = customer.Id,
            Technology = technology?.Trim().Truncate(100),
            Material = material?.Trim().Truncate(400),
            Fill = fill?.Trim().Truncate(200),
            Email = customer.IsRegistered() ? customer.Email : null,
            DeliveryMethod = PrintDeliveryMethod.Shipping,
            IpAddress = _webHelper.ClientInfo.IpAddress?.ToString()
        };

        job.Models = sanitized;

        await _printOrderService.CreateDraftAsync(job, files, HttpContext.RequestAborted);

        if (job.DepositAmount <= 0)
        {
            return Json(new { ok = false, error = "Không tính được giá cho đơn in này. Hãy gửi yêu cầu báo giá để studio xem file." });
        }

        var warnings = await _printOrderService.AddToCartAsync(job, customer, Services.StoreContext.CurrentStore.Id, HttpContext.RequestAborted);
        if (warnings.Count > 0)
        {
            return Json(new { ok = false, error = string.Join(" ", warnings) });
        }

        return Json(new { ok = true, url = Url.Action(nameof(Index), new { ma = job.Code }) });
    }

    /// <summary>
    /// Delivery details of the print job in the cart.
    /// </summary>
    [HttpGet("", Name = StudioStorefrontSetup.PrintOrderRouteName)]
    public async Task<IActionResult> Index(string? ma)
    {
        var job = await FindJobAsync(ma);
        if (job == null)
        {
            NotifyInfo("Chưa có đơn in nào đang chờ. Hãy thả file 3D vào bộ tính giá để đặt in.");
            return RedirectToRoute(StudioStorefrontSetup.PrintServiceRouteName);
        }

        return View(await PrepareModelAsync(job, null));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken, ValidateHoneypot]
    public async Task<IActionResult> Index(PrintOrderFormModel form)
    {
        var job = await FindJobAsync(null, form.Id);
        if (job == null)
        {
            NotifyInfo("Đơn in này không còn trong giỏ hàng.");
            return RedirectToRoute(StudioStorefrontSetup.PrintServiceRouteName);
        }

        var pickup = form.DeliveryMethodId == (int)PrintDeliveryMethod.Pickup && _settings.AllowPickup;
        if (!pickup)
        {
            if (form.AddressLine.IsEmpty())
            {
                ModelState.AddModelError(nameof(form.AddressLine), "Hãy nhập địa chỉ nhận hàng.");
            }

            if (form.City.IsEmpty())
            {
                ModelState.AddModelError(nameof(form.City), "Hãy nhập tỉnh / thành phố.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(await PrepareModelAsync(job, form));
        }

        job.RecipientName = form.Name.Trim();
        job.Phone = form.Phone.Trim();
        job.Email = form.Email?.Trim().NullEmpty();
        job.DeliveryMethod = pickup ? PrintDeliveryMethod.Pickup : PrintDeliveryMethod.Shipping;
        job.AddressLine = pickup ? null : form.AddressLine?.Trim();
        job.City = pickup ? null : form.City?.Trim();
        job.DesiredOnUtc = form.DesiredOn?.Date;
        job.Note = form.Note?.Trim().NullEmpty();
        job.UpdatedOnUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return RedirectToRoute("Checkout");
    }

    /// <summary>
    /// Payment link of a job the studio created from a quote request: puts it into the cart and continues
    /// with the delivery details.
    /// </summary>
    [HttpGet("thanh-toan/{token}")]
    public async Task<IActionResult> Pay(string token)
    {
        var job = await _printOrderService.GetByTokenAsync(token);
        if (job == null)
        {
            return NotFound();
        }

        if (job.OrderId > 0)
        {
            NotifyInfo($"Đơn in {job.Code} đã được thanh toán.");
            return RedirectToAction("Details", "Order", new { id = job.OrderId, area = string.Empty });
        }

        if (job.Status == PrintOrderStatus.Cancelled)
        {
            NotifyInfo($"Báo giá cho đơn in {job.Code} không còn hiệu lực. Hãy liên hệ studio để được báo giá lại.");
            return RedirectToRoute(StudioStorefrontSetup.PrintServiceRouteName);
        }

        var customer = Services.WorkContext.CurrentCustomer;
        var warnings = await _printOrderService.AddToCartAsync(job, customer, Services.StoreContext.CurrentStore.Id);
        if (warnings.Count > 0)
        {
            NotifyError(string.Join(" ", warnings));
            return RedirectToRoute(StudioStorefrontSetup.PrintServiceRouteName);
        }

        return RedirectToAction(nameof(Index), new { ma = job.Code });
    }

    /// <summary>
    /// Gets the job the customer is working on: the one asked for, or the last print job in the cart.
    /// </summary>
    private async Task<PrintOrder?> FindJobAsync(string? code, int? id = null)
    {
        var customer = Services.WorkContext.CurrentCustomer;
        var cart = await _cartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, Services.StoreContext.CurrentStore.Id);
        var codes = cart.Items
            .Select(x => PrintOrderService.ReadJobCode(x.Item.RawAttributes))
            .Where(x => x != null)
            .ToList();

        if (codes.Count == 0)
        {
            return null;
        }

        var jobs = await _db.PrintOrders()
            .Where(x => codes.Contains(x.Code))
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        return id > 0
            ? jobs.FirstOrDefault(x => x.Id == id.Value)
            : code.HasValue()
                ? jobs.FirstOrDefault(x => x.Code.EqualsNoCase(code))
                : jobs.FirstOrDefault();
    }

    private async Task<PrintOrderPageModel> PrepareModelAsync(PrintOrder job, PrintOrderFormModel? form)
    {
        var customer = Services.WorkContext.CurrentCustomer;
        var tier = PrintPriceList.Parse(_settings.PrintPriceTable)
            .FirstOrDefault(x => x.Name.EqualsNoCase(job.Technology))?
            .FindMaterial(job.Material)?
            .GetTier(job.TotalGrams);

        var model = new PrintOrderPageModel
        {
            Contact = StudioContactModel.Create(_settings),
            AllowPickup = _settings.AllowPickup,
            DepositNote = _settings.DepositNote,
            PriceNote = _settings.PrintPriceNote,
            RequiresLogin = !customer.IsRegistered(),
            LoginUrl = Url.RouteUrl("Login", new { returnUrl = Url.Action(nameof(Index), new { ma = job.Code }) }),
            CalculatorUrl = Url.RouteUrl(StudioStorefrontSetup.PrintServiceRouteName),
            Job = new PrintOrderSummaryModel
            {
                Id = job.Id,
                Code = job.Code,
                Technology = job.Technology,
                Material = job.Material,
                Fill = job.Fill,
                TotalGrams = job.TotalGrams,
                Pieces = job.Pieces,
                ModelCount = job.ModelCount,
                PricePerGram = job.PricePerGram,
                TierLabel = tier?.Label,
                PriceEstimate = job.FinalPrice ?? job.PriceEstimate,
                DepositPercent = job.DepositPercent,
                DepositAmount = job.DepositAmount,
                Outstanding = job.Outstanding,
                FileName = job.FileName,
                FileSize = job.FileSize > 0 ? Prettifier.HumanizeBytes(job.FileSize) : null,
                FromQuote = job.QuoteRequestId > 0,
                Models = job.Models.Select(x => new PrintOrderModelLine
                {
                    Name = x.Name,
                    Grams = x.Grams,
                    Quantity = x.Quantity,
                    Size = x.Size,
                    Fill = x.Fill,
                    Manual = x.Manual
                }).ToList()
            }
        };

        model.Form = form ?? new PrintOrderFormModel
        {
            Id = job.Id,
            Name = job.RecipientName.NullEmpty() ?? customer.GetFullName().NullEmpty() ?? string.Empty,
            Phone = job.Phone.NullEmpty() ?? customer.GenericAttributes.Phone.NullEmpty() ?? string.Empty,
            Email = job.Email.NullEmpty() ?? (customer.IsRegistered() ? customer.Email : null),
            DeliveryMethodId = job.DeliveryMethodId,
            AddressLine = job.AddressLine,
            City = job.City,
            DesiredOn = job.DesiredOnUtc,
            Note = job.Note
        };

        model.Form.Id = job.Id;

        if (!model.AllowPickup)
        {
            model.Form.DeliveryMethodId = (int)PrintDeliveryMethod.Shipping;
        }

        return model;
    }
}
