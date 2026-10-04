using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Identity;
using Smartstore.Core.Security;
using Smartstore.Core.Web;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// The "In 3D theo yêu cầu" page: price list, price estimate and quote request with model upload.
/// </summary>
[Route("in-3d")]
public class PrintServiceController : PublicController
{
    // Upper bound for the request body. The configured file limit (StudioSettings.QuoteMaxFileSizeMb) is checked in the action.
    private const long MaxRequestSize = 520L * 1024 * 1024;

    private readonly PrintQuoteService _quoteService;
    private readonly IWebHelper _webHelper;
    private readonly StudioSettings _settings;

    public PrintServiceController(
        PrintQuoteService quoteService,
        IWebHelper webHelper,
        StudioSettings settings)
    {
        _quoteService = quoteService;
        _webHelper = webHelper;
        _settings = settings;
    }

    [HttpGet("", Name = StudioStorefrontSetup.PrintServiceRouteName)]
    public IActionResult Index(int? sent, string tech, string mat, int? g, int? q)
    {
        var model = PrepareModel(new PrintQuoteFormModel
        {
            Technology = tech,
            Material = mat,
            EstimatedGrams = g > 0 ? g : null,
            Quantity = q > 0 ? q.Value : 1
        });
        model.SubmittedId = sent;

        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken, ValidateHoneypot]
    [RequestSizeLimit(MaxRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
    public async Task<IActionResult> Index([FromForm(Name = "Form")] PrintQuoteFormModel form, List<IFormFile> modelFile)
    {
        // Several model files may be sent at once (the price calculator hands over every weighed model).
        var files = (modelFile ?? []).Where(x => x != null && x.Length > 0).ToList();
        if (files.FirstOrDefault(x => !PrintQuoteService.IsAllowedFile(x.FileName)) is { } badFile)
        {
            ModelState.AddModelError("modelFile", $"Định dạng file {badFile.FileName} chưa được hỗ trợ. Hãy nén file thành .zip hoặc gửi link tải.");
        }
        else if (files.Sum(x => x.Length) > _quoteService.MaxFileSize)
        {
            ModelState.AddModelError("modelFile", $"File vượt quá {_settings.QuoteMaxFileSizeMb} MB. Hãy gửi link Google Drive / Dropbox.");
        }

        if (!ModelState.IsValid)
        {
            return View(PrepareModel(form));
        }

        var customer = Services.WorkContext.CurrentCustomer;
        var request = new PrintQuoteRequest
        {
            CustomerId = customer.IsRegistered() ? customer.Id : 0,
            Name = form.Name.Trim(),
            Phone = form.Phone.Trim(),
            Email = form.Email?.Trim().NullEmpty() ?? (customer.IsRegistered() ? customer.Email : null),
            Technology = form.Technology?.Trim().NullEmpty(),
            Material = form.Material?.Trim().NullEmpty(),
            Quantity = Math.Max(form.Quantity, 1),
            EstimatedGrams = form.EstimatedGrams,
            NeedsDesign = form.NeedsDesign,
            Note = string.Join("\n", new[]
                {
                    form.Measurement?.Trim().NullEmpty() is string measurement ? "Đo từ file: " + measurement : null,
                    form.Note?.Trim().NullEmpty()
                }.Where(x => x != null)).NullEmpty(),
            FileLink = form.FileLink?.Trim().NullEmpty(),
            IpAddress = _webHelper.ClientInfo.IpAddress?.ToString()
        };

        await _quoteService.SubmitAsync(request, files, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index), new { sent = request.Id });
    }

    private PrintServicePageModel PrepareModel(PrintQuoteFormModel form)
    {
        return new PrintServicePageModel
        {
            Contact = StudioContactModel.Create(_settings),
            Technologies = PrintPriceList.Parse(_settings.PrintPriceTable),
            PrintPriceNote = _settings.PrintPriceNote,
            Form = form,
            MaxFileSizeMb = _settings.QuoteMaxFileSizeMb,
            AllowedExtensions = string.Join(",", PrintQuoteService.AllowedExtensions)
        };
    }
}
