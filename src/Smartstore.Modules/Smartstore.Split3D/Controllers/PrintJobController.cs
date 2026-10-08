#nullable enable

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Utilities;
using Smartstore.Web.Controllers;
using Smartstore.Web.Models.DataGrid;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Admin pages of the paid 3D printing jobs: the list the studio works through every day and the workflow
/// from "deposit received" to "handed over".
/// </summary>
public class PrintJobController : AdminController
{
    // Upper bound for the model files of a job entered in the admin area.
    private const long MaxRequestSize = 520L * 1024 * 1024;

    private readonly SmartDbContext _db;
    private readonly PrintOrderService _printOrderService;
    private readonly StudioOrderClassifier _orderClassifier;
    private readonly StudioSettings _settings;

    public PrintJobController(
        SmartDbContext db,
        PrintOrderService printOrderService,
        StudioOrderClassifier orderClassifier,
        StudioSettings settings)
    {
        _db = db;
        _printOrderService = printOrderService;
        _orderClassifier = orderClassifier;
        _settings = settings;
    }

    public IActionResult Index()
        => RedirectToAction(nameof(List));

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> List()
    {
        var jobs = _db.PrintOrders().AsNoTracking();
        var model = new PrintJobListModel
        {
            ToConfirmCount = await jobs.CountAsync(x => x.StatusId == (int)PrintOrderStatus.Paid),
            OpenCount = await jobs.CountAsync(x => x.StatusId >= (int)PrintOrderStatus.Confirmed && x.StatusId <= (int)PrintOrderStatus.Ready)
        };

        ViewBag.Statuses = GetStatuses();

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> JobList(GridCommand command, PrintJobListModel model)
    {
        var query = _db.PrintOrders().AsNoTracking();

        if (model.SearchKindId.HasValue)
        {
            query = query.Where(x => x.KindId == model.SearchKindId.Value);
        }

        if (model.SearchStatusId.HasValue)
        {
            query = query.Where(x => x.StatusId == model.SearchStatusId.Value);
        }

        if (model.SearchTerm.HasValue())
        {
            var term = model.SearchTerm!.Trim();
            query = query.Where(x => x.Code!.Contains(term)
                || x.RecipientName!.Contains(term)
                || x.Phone!.Contains(term)
                || x.Email!.Contains(term)
                || x.Note!.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(x => x.StatusId == (int)PrintOrderStatus.Paid)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToListAsync();

        var orders = await LoadOrdersAsync(rows);

        return Json(new GridModel<PrintJobModel>
        {
            Rows = rows.Select(x => ToModel(x, orders)).ToList(),
            Total = total
        });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> JobDelete(GridSelection selection)
    {
        var ids = selection.GetEntityIds().ToArray();
        var jobs = await _db.PrintOrders().Where(x => ids.Contains(x.Id)).ToListAsync();
        var count = await _printOrderService.DeleteAsync(jobs);

        return Json(new { Success = true, Count = count });
    }

    /// <summary>
    /// Form for a job the studio enters itself, e.g. ordered by phone. The job starts as a draft with a payment link.
    /// </summary>
    [Permission(Permissions.Configuration.Module.Update)]
    public IActionResult Create()
    {
        var model = new PrintJobCreateModel
        {
            DepositPercent = _printOrderService.DepositPercent,
            DeliveryMethodId = (int)(_settings.AllowPickup ? PrintDeliveryMethod.Pickup : PrintDeliveryMethod.Shipping)
        };

        ViewBag.Technologies = PrintPriceList.Parse(_settings.PrintPriceTable);

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    [RequestSizeLimit(MaxRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
    public async Task<IActionResult> Create(PrintJobCreateModel model, List<IFormFile> files)
    {
        var uploads = (files ?? []).Where(x => x != null && x.Length > 0).ToList();
        if (uploads.Sum(x => x.Length) > MaxRequestSize)
        {
            ModelState.AddModelError(string.Empty, T("Plugins.Split3D.PrintQuote.FileTooLarge", MaxRequestSize / 1024 / 1024));
        }

        var shipping = model.DeliveryMethodId == (int)PrintDeliveryMethod.Shipping;
        if (shipping && model.AddressLine.IsEmpty())
        {
            ModelState.AddModelError(nameof(model.AddressLine), T("Plugins.Split3D.PrintJob.AddressRequired"));
        }

        PrintOrder? job = null;
        if (ModelState.IsValid)
        {
            var email = model.Email?.Trim().NullEmpty();
            var customerId = email != null
                ? await _db.Customers.Where(x => x.Email == email && !x.Deleted).Select(x => x.Id).FirstOrDefaultAsync()
                : 0;

            job = await _printOrderService.CreateManualAsync(
                new PrintOrder
                {
                    CustomerId = customerId,
                    RecipientName = model.RecipientName!.Trim(),
                    Phone = model.Phone!.Trim(),
                    Email = email,
                    Technology = model.Technology?.Trim().NullEmpty(),
                    Material = model.Material?.Trim().NullEmpty(),
                    Fill = model.Fill?.Trim().NullEmpty(),
                    TotalGrams = model.TotalGrams,
                    Pieces = model.Pieces,
                    DeliveryMethod = shipping ? PrintDeliveryMethod.Shipping : PrintDeliveryMethod.Pickup,
                    AddressLine = shipping ? model.AddressLine?.Trim().NullEmpty() : null,
                    City = shipping ? model.City?.Trim().NullEmpty() : null,
                    DesiredOnUtc = model.DesiredOn?.Date,
                    FileLink = model.FileLink?.Trim().NullEmpty(),
                    Note = model.Note?.Trim().NullEmpty(),
                    AdminNote = model.AdminNote?.Trim().NullEmpty()
                },
                model.Price,
                model.DepositPercent > 0 ? model.DepositPercent : null,
                uploads,
                HttpContext.RequestAborted);

            if (job == null)
            {
                ModelState.AddModelError(nameof(model.Price), T("Plugins.Split3D.PrintJob.PriceRequired"));
            }
        }

        if (job == null)
        {
            ViewBag.Technologies = PrintPriceList.Parse(_settings.PrintPriceTable);
            return View(model);
        }

        NotifySuccess(T("Plugins.Split3D.PrintJob.Created", job.Code!));

        return RedirectToAction(nameof(Edit), new { id = job.Id });
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Edit(int id)
    {
        var job = await _db.PrintOrders().FindByIdAsync(id, false);
        if (job == null)
        {
            return NotFound();
        }

        ViewBag.Statuses = GetStatuses();

        return View(ToModel(job, await LoadOrdersAsync([job])));
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Edit(PrintJobModel model)
    {
        var job = await _db.PrintOrders().FindByIdAsync(model.Id);
        if (job == null)
        {
            return NotFound();
        }

        job.FinalPrice = model.FinalPrice > 0 ? decimal.Round(model.FinalPrice.Value, 0) : null;
        job.ShippingFee = model.ShippingFee > 0 ? decimal.Round(model.ShippingFee.Value, 0) : null;
        job.AdminNote = model.AdminNote;
        job.UpdatedOnUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Edit), new { id = job.Id });
    }

    /// <summary>
    /// Moves a job through the workflow: confirm, start printing, done, handed over, cancelled.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> SetStatus(int id, int statusId, string? message, bool notifyCustomer = true)
    {
        var job = await _db.PrintOrders().FindByIdAsync(id);
        if (job == null)
        {
            return NotFound();
        }

        if (!Enum.IsDefined(typeof(PrintOrderStatus), statusId))
        {
            return BadRequest();
        }

        await ApplyStatusAsync(job, (PrintOrderStatus)statusId, message, notifyCustomer);

        return RedirectToAction(nameof(Edit), new { id = job.Id });
    }

    /// <summary>
    /// Workflow step taken on the admin order page (<see cref="Components.OrderWorkflowViewComponent"/>). The card sits
    /// inside the order edit form, so its buttons post that form here via <c>formaction</c>; the job and the step come
    /// from the query string, the form's own fields (e.g. its "Id") are ignored.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Order.Update)]
    public async Task<IActionResult> OrderStep([FromQuery] int jobId, [FromQuery] int step)
    {
        // Field names carry the job id: an order with several jobs renders one message box per job.
        var message = Request.Form["ttStepMessage_" + jobId].ToString().NullEmpty();
        var notifyCustomer = Request.Form["ttStepNotify_" + jobId].ToString().EqualsNoCase("true");

        var job = await _db.PrintOrders().FindByIdAsync(jobId);
        if (job == null)
        {
            return NotFound();
        }

        if (!Enum.IsDefined(typeof(PrintOrderStatus), step))
        {
            return BadRequest();
        }

        await ApplyStatusAsync(job, (PrintOrderStatus)step, message, notifyCustomer);

        return job.OrderId > 0
            ? RedirectToAction("Edit", "Order", new { id = job.OrderId, area = "Admin" })
            : RedirectToAction(nameof(Edit), new { id = job.Id });
    }

    /// <summary>
    /// Puts the products of an order into the studio workflow, for orders placed before the workflow covered goods
    /// and for orders entered in the admin area (whose products are added after the order was created).
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Order.Update)]
    public async Task<IActionResult> CreateForOrder([FromQuery] int orderId)
    {
        var order = await _db.Orders.FindByIdAsync(orderId);
        if (order == null)
        {
            return NotFound();
        }

        var goods = await _orderClassifier.GetGoodsItemsAsync(order.Id);
        var job = await _printOrderService.CreateGoodsJobAsync(order, goods, false);

        if (job != null)
        {
            NotifySuccess(T("Plugins.Split3D.PrintJob.Created", job.Code!));
        }
        else
        {
            NotifyWarning(T("Plugins.Split3D.PrintJob.NoGoods"));
        }

        return RedirectToAction("Edit", "Order", new { id = order.Id, area = "Admin" });
    }

    private async Task ApplyStatusAsync(PrintOrder job, PrintOrderStatus status, string? message, bool notifyCustomer)
    {
        var warning = await _printOrderService.SetStatusAsync(job, status, message, notifyCustomer);

        NotifySuccess(T("Plugins.Split3D.PrintJob.StatusChanged", job.Code!, PrintOrderService.GetStatusText(status, job.Kind)));

        if (warning.HasValue())
        {
            NotifyWarning(warning!);
        }
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Delete(int id)
    {
        var job = await _db.PrintOrders().FindByIdAsync(id);
        if (job != null)
        {
            await _printOrderService.DeleteAsync([job]);
            NotifySuccess(T("Admin.Common.DataDeletionSuccess"));
        }

        return RedirectToAction(nameof(List));
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Download(int id)
    {
        var job = await _db.PrintOrders().FindByIdAsync(id, false);
        var file = await _printOrderService.GetFileAsync(job);
        if (file == null)
        {
            return NotFound();
        }

        return File(await file.OpenReadAsync(), "application/octet-stream", job!.FileName.NullEmpty() ?? file.Name);
    }

    private async Task<Dictionary<int, Order>> LoadOrdersAsync(IEnumerable<PrintOrder> jobs)
    {
        var orderIds = jobs.Where(x => x.OrderId > 0).Select(x => x.OrderId).Distinct().ToArray();

        return orderIds.Length > 0
            ? await _db.Orders.AsNoTracking().Where(x => orderIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id)
            : [];
    }

    private PrintJobModel ToModel(PrintOrder x, Dictionary<int, Order> orders)
    {
        var status = x.Status;
        var order = x.OrderId > 0 && orders.TryGetValue(x.OrderId, out var o) ? o : null;

        return new PrintJobModel
        {
            Id = x.Id,
            Code = x.Code,
            CreatedOn = Services.DateTimeHelper.ConvertToUserTime(x.CreatedOnUtc, DateTimeKind.Utc),
            StatusId = x.StatusId,
            StatusName = GetStatusName(status, x.Kind),
            KindId = x.KindId,
            IsGoods = x.Kind == PrintJobKind.Goods,
            KindName = T("Plugins.Split3D.PrintJob.Kind." + x.Kind),
            StatusBadge = status switch
            {
                PrintOrderStatus.Draft => "badge-secondary",
                PrintOrderStatus.AwaitingPayment => "badge-warning",
                PrintOrderStatus.Paid => "badge-danger",
                PrintOrderStatus.Confirmed => "badge-info",
                PrintOrderStatus.Printing => "badge-primary",
                PrintOrderStatus.Ready => "badge-info",
                PrintOrderStatus.Completed => "badge-success",
                _ => "badge-secondary"
            },
            RecipientName = x.RecipientName,
            Phone = x.Phone,
            Email = x.Email,
            Technology = x.Technology,
            Material = x.Material,
            Fill = x.Fill,
            TotalGrams = x.TotalGrams,
            Pieces = x.Pieces,
            ModelCount = x.ModelCount,
            PricePerGram = x.PricePerGram,
            PriceEstimate = x.PriceEstimate,
            DepositAmount = x.DepositAmount,
            DepositPercent = x.DepositPercent,
            FinalPrice = x.FinalPrice,
            ShippingFee = x.ShippingFee,
            Outstanding = x.Outstanding,
            DeliveryMethodId = x.DeliveryMethodId,
            DeliveryMethodName = x.DeliveryMethod == PrintDeliveryMethod.Pickup
                ? T("Plugins.Split3D.PrintJob.Pickup")
                : T("Plugins.Split3D.PrintJob.Shipping"),
            Address = x.DeliveryMethod == PrintDeliveryMethod.Pickup
                ? _settings.Address
                : string.Join(", ", new[] { x.AddressLine, x.City }.Where(v => v.HasValue())),
            DesiredOn = x.DesiredOnUtc,
            Note = x.Note,
            AdminNote = x.AdminNote,
            OrderId = x.OrderId,
            OrderNumber = order?.GetOrderNumber(),
            OrderUrl = order != null ? Url.Action("Edit", "Order", new { id = order.Id, area = "Admin" }) : null,
            PaymentStatusName = order != null ? Services.Localization.GetLocalizedEnum(order.PaymentStatus) : null,
            IsPaid = order?.PaymentStatus == Core.Checkout.Payment.PaymentStatus.Paid,
            FileName = x.FileName,
            FileSize = x.FileSize > 0 ? Prettifier.HumanizeBytes(x.FileSize) : null,
            HasFile = x.FilePath.HasValue(),
            FileLink = x.FileLink,
            DownloadUrl = x.FilePath.HasValue() ? Url.Action(nameof(Download), new { id = x.Id }) : null,
            EditUrl = Url.Action(nameof(Edit), new { id = x.Id }),
            PayUrl = x.PayToken.HasValue() && x.OrderId == 0 ? _printOrderService.BuildPayUrl(x) : null,
            QuoteRequestId = x.QuoteRequestId,
            QuoteUrl = x.QuoteRequestId > 0 ? Url.Action("Edit", "PrintQuote", new { id = x.QuoteRequestId }) : null,
            CustomerId = x.CustomerId,
            PaidOn = x.PaidOnUtc,
            ConfirmedOn = x.ConfirmedOnUtc,
            CompletedOn = x.CompletedOnUtc,
            Models = x.Models.Select(m => new PrintOrderModelLine
            {
                Name = m.Name,
                Grams = m.Grams,
                Quantity = m.Quantity,
                Size = m.Size,
                Fill = m.Fill,
                Manual = m.Manual
            }).ToList()
        };
    }

    private List<(int Id, string Name)> GetStatuses()
        => Enum.GetValues<PrintOrderStatus>()
            .Select(x => ((int)x, T("Plugins.Split3D.PrintJob.Status." + x).Value))
            .ToList();

    /// <summary>
    /// Status name; the production steps of goods have their own wording ("Đang làm" instead of "Đang in").
    /// </summary>
    private string GetStatusName(PrintOrderStatus status, PrintJobKind kind)
        => T(PrintJobSteps.StatusKey(status, kind == PrintJobKind.Goods));
}
