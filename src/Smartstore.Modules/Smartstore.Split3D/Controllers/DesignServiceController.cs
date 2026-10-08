using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Identity;
using Smartstore.Core.Security;
using Smartstore.Core.Web;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// The "Thiết kế" page: 3D design services and a design request form with reference file upload.
/// Requests are stored as <see cref="PrintQuoteRequest"/> entries that need design.
/// </summary>
[Route("thiet-ke")]
public class DesignServiceController : PublicController
{
    // Upper bound for the request body. The configured file limit (StudioSettings.QuoteMaxFileSizeMb) is checked in the action.
    private const long MaxRequestSize = 520L * 1024 * 1024;

    private readonly PrintQuoteService _quoteService;
    private readonly IWebHelper _webHelper;
    private readonly StudioSettings _settings;

    public DesignServiceController(PrintQuoteService quoteService, IWebHelper webHelper, StudioSettings settings)
    {
        _quoteService = quoteService;
        _webHelper = webHelper;
        _settings = settings;
    }

    [HttpGet("", Name = StudioStorefrontSetup.DesignServiceRouteName)]
    public IActionResult Index(int? sent, string cat)
    {
        var model = PrepareModel(new DesignRequestFormModel { Category = DesignCategory.Find(cat)?.Key });
        model.SubmittedId = sent;

        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken, ValidateHoneypot]
    [RequestSizeLimit(MaxRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
    public async Task<IActionResult> Index([FromForm(Name = "Form")] DesignRequestFormModel form, IFormFile referenceFile)
    {
        if (referenceFile != null && referenceFile.Length > 0)
        {
            if (!PrintQuoteService.IsAllowedFile(referenceFile.FileName))
            {
                ModelState.AddModelError("referenceFile", "Định dạng file chưa được hỗ trợ. Hãy nén file thành .zip hoặc gửi link tải.");
            }
            else if (referenceFile.Length > _quoteService.MaxFileSize)
            {
                ModelState.AddModelError("referenceFile", $"File vượt quá {_settings.QuoteMaxFileSizeMb} MB. Hãy gửi link Google Drive / Dropbox.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(PrepareModel(form));
        }

        var customer = Services.WorkContext.CurrentCustomer;
        var category = DesignCategory.Find(form.Category);
        var request = new PrintQuoteRequest
        {
            CustomerId = customer.IsRegistered() ? customer.Id : 0,
            Name = form.Name.Trim(),
            Phone = form.Phone.Trim(),
            Email = form.Email?.Trim().NullEmpty() ?? (customer.IsRegistered() ? customer.Email : null),
            Technology = PrintQuoteService.DesignTechnology,
            Material = category?.Title ?? "Chưa rõ hạng mục",
            Quantity = 1,
            NeedsDesign = true,
            Note = (form.AlsoPrint ? "[Cần in sau khi thiết kế]\n" : "[Chỉ thiết kế, không in]\n") + form.Note.Trim(),
            FileLink = form.FileLink?.Trim().NullEmpty(),
            IpAddress = _webHelper.ClientInfo.IpAddress?.ToString()
        };

        await _quoteService.SubmitAsync(request, referenceFile, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index), new { sent = request.Id });
    }

    private DesignPageModel PrepareModel(DesignRequestFormModel form)
    {
        return new DesignPageModel
        {
            Contact = StudioContactModel.Create(_settings),
            Form = form,
            MaxFileSizeMb = _settings.QuoteMaxFileSizeMb,
            AllowedExtensions = string.Join(",", PrintQuoteService.AllowedExtensions)
        };
    }
}
