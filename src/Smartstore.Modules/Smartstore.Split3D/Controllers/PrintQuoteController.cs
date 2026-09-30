using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Web.Controllers;
using Smartstore.Utilities;
using Smartstore.Web.Models.DataGrid;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Admin pages of the TT Minimal studio: 3D printing quote requests and studio settings.
/// </summary>
public class PrintQuoteController : AdminController
{
    private readonly SmartDbContext _db;
    private readonly PrintQuoteService _quoteService;
    private readonly StudioStorefrontSetup _studioSetup;
    private readonly StudioSettings _settings;

    public PrintQuoteController(SmartDbContext db, PrintQuoteService quoteService, StudioStorefrontSetup studioSetup, StudioSettings settings)
    {
        _db = db;
        _quoteService = quoteService;
        _studioSetup = studioSetup;
        _settings = settings;
    }

    public IActionResult Index()
        => RedirectToAction(nameof(List));

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> List()
    {
        var quotes = _db.PrintQuoteRequests().AsNoTracking();
        var model = new PrintQuoteListModel
        {
            NewCount = await quotes.CountAsync(x => x.StatusId == (int)PrintQuoteStatus.New),
            OpenCount = await quotes.CountAsync(x => x.StatusId == (int)PrintQuoteStatus.Quoted || x.StatusId == (int)PrintQuoteStatus.Printing)
        };

        ViewBag.Statuses = GetStatuses();

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> QuoteList(GridCommand command, PrintQuoteListModel model)
    {
        var query = _db.PrintQuoteRequests().AsNoTracking();

        if (model.SearchStatusId.HasValue)
        {
            query = query.Where(x => x.StatusId == model.SearchStatusId.Value);
        }

        if (model.SearchTerm.HasValue())
        {
            var term = model.SearchTerm.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Phone.Contains(term) || x.Email.Contains(term) || x.Note.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(x => x.StatusId == (int)PrintQuoteStatus.New)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToListAsync();

        var technologies = PrintPriceList.Parse(_settings.PrintPriceTable);

        return Json(new GridModel<PrintQuoteModel>
        {
            Rows = rows.Select(x => ToModel(x, technologies)).ToList(),
            Total = total
        });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> QuoteDelete(GridSelection selection)
    {
        var ids = selection.GetEntityIds().ToArray();
        var requests = await _db.PrintQuoteRequests().Where(x => ids.Contains(x.Id)).ToListAsync();
        var count = await _quoteService.DeleteAsync(requests);

        return Json(new { Success = true, Count = count });
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Edit(int id)
    {
        var request = await _db.PrintQuoteRequests().FindByIdAsync(id, false);
        if (request == null)
        {
            return NotFound();
        }

        ViewBag.Statuses = GetStatuses();

        return View(ToModel(request, PrintPriceList.Parse(_settings.PrintPriceTable)));
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Edit(PrintQuoteModel model)
    {
        var request = await _db.PrintQuoteRequests().FindByIdAsync(model.Id);
        if (request == null)
        {
            return NotFound();
        }

        request.StatusId = Enum.IsDefined(typeof(PrintQuoteStatus), model.StatusId) ? model.StatusId : request.StatusId;
        request.QuotedPrice = model.QuotedPrice;
        request.AdminNote = model.AdminNote;
        request.UpdatedOnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Edit), new { id = request.Id });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Delete(int id)
    {
        var request = await _db.PrintQuoteRequests().FindByIdAsync(id);
        if (request != null)
        {
            await _quoteService.DeleteAsync([request]);
            NotifySuccess(T("Admin.Common.DataDeletionSuccess"));
        }

        return RedirectToAction(nameof(List));
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Download(int id)
    {
        var request = await _db.PrintQuoteRequests().FindByIdAsync(id, false);
        var file = await _quoteService.GetFileAsync(request);
        if (file == null)
        {
            return NotFound();
        }

        return File(await file.OpenReadAsync(), "application/octet-stream", request.FileName.NullEmpty() ?? file.Name);
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public IActionResult Settings()
    {
        return View(new StudioConfigurationModel
        {
            BrandName = _settings.BrandName,
            Tagline = _settings.Tagline,
            Address = _settings.Address,
            Phone1 = _settings.Phone1,
            Phone2 = _settings.Phone2,
            ZaloPhone = _settings.ZaloPhone,
            FacebookUrl = _settings.FacebookUrl,
            Email = _settings.Email,
            MapUrl = _settings.MapUrl,
            PrintPriceTable = PrintPriceList.Normalize(_settings.PrintPriceTable),
            PrintPriceNote = _settings.PrintPriceNote,
            QuoteNotifyEmail = _settings.QuoteNotifyEmail,
            QuoteMaxFileSizeMb = _settings.QuoteMaxFileSizeMb,
            LayoutVersion = _settings.LayoutVersion,
            CurrentLayoutVersion = StudioStorefrontSetup.CurrentVersion,
            ParsedPrices = PrintPriceList.Parse(_settings.PrintPriceTable)
        });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Settings(StudioConfigurationModel model)
    {
        if (model.PrintPriceTable.HasValue() && PrintPriceList.Parse(model.PrintPriceTable).Count == 0)
        {
            ModelState.AddModelError(nameof(model.PrintPriceTable), T("Plugins.Split3D.Studio.PriceTableInvalid"));
        }

        if (!ModelState.IsValid)
        {
            model.LayoutVersion = _settings.LayoutVersion;
            model.CurrentLayoutVersion = StudioStorefrontSetup.CurrentVersion;
            return View(model);
        }

        _settings.BrandName = model.BrandName.Trim();
        _settings.Tagline = model.Tagline?.Trim();
        _settings.Address = model.Address?.Trim();
        _settings.Phone1 = model.Phone1?.Trim();
        _settings.Phone2 = model.Phone2?.Trim();
        _settings.ZaloPhone = model.ZaloPhone?.Trim();
        _settings.FacebookUrl = model.FacebookUrl?.Trim();
        _settings.Email = model.Email?.Trim();
        _settings.MapUrl = model.MapUrl?.Trim();
        _settings.PrintPriceTable = model.PrintPriceTable.NullEmpty() ?? StudioSettings.DefaultPrintPriceTable;
        _settings.PrintPriceNote = model.PrintPriceNote?.Trim();
        _settings.QuoteNotifyEmail = model.QuoteNotifyEmail?.Trim();
        _settings.QuoteMaxFileSizeMb = model.QuoteMaxFileSizeMb;

        await Services.SettingFactory.SaveSettingsAsync(_settings);
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> ApplyLayout()
    {
        await _studioSetup.ApplyAsync(true, HttpContext.RequestAborted);
        NotifySuccess(T("Plugins.Split3D.Studio.LayoutApplied"));

        return RedirectToAction(nameof(Settings));
    }

    private PrintQuoteModel ToModel(PrintQuoteRequest x, List<PrintTechnology> technologies)
    {
        var status = x.Status;
        string estimate = null;

        var material = technologies.FirstOrDefault(t => t.Name.EqualsNoCase(x.Technology))?.FindMaterial(x.Material);
        if (material != null && x.EstimatedGrams > 0)
        {
            var grams = x.EstimatedGrams.Value * Math.Max(x.Quantity, 1);
            var tier = material.GetTier(grams);
            if (tier != null)
            {
                estimate = $"{PrintPriceList.FormatPrice(grams * tier.PricePerGram)} ({PrintPriceList.FormatPrice(tier.PricePerGram)}/g × {grams:N0} g)";
            }
        }

        return new PrintQuoteModel
        {
            Id = x.Id,
            CreatedOn = Services.DateTimeHelper.ConvertToUserTime(x.CreatedOnUtc, DateTimeKind.Utc),
            Name = x.Name,
            Phone = x.Phone,
            Email = x.Email,
            Technology = x.Technology,
            Material = x.Material,
            Quantity = x.Quantity,
            EstimatedGrams = x.EstimatedGrams,
            EstimatedPrice = estimate,
            NeedsDesign = x.NeedsDesign,
            Note = x.Note,
            FileLink = x.FileLink,
            FileName = x.FileName,
            FileSize = x.FileSize > 0 ? Prettifier.HumanizeBytes(x.FileSize) : null,
            HasFile = x.FilePath.HasValue(),
            StatusId = x.StatusId,
            StatusName = T("Plugins.Split3D.PrintQuote.Status." + status),
            StatusBadge = status switch
            {
                PrintQuoteStatus.New => "badge-danger",
                PrintQuoteStatus.Quoted => "badge-warning",
                PrintQuoteStatus.Printing => "badge-info",
                PrintQuoteStatus.Completed => "badge-success",
                _ => "badge-secondary"
            },
            QuotedPrice = x.QuotedPrice,
            AdminNote = x.AdminNote,
            CustomerId = x.CustomerId,
            IpAddress = x.IpAddress,
            EditUrl = Url.Action(nameof(Edit), new { id = x.Id }),
            DownloadUrl = x.FilePath.HasValue() ? Url.Action(nameof(Download), new { id = x.Id }) : null
        };
    }

    private List<(int Id, string Name)> GetStatuses()
        => Enum.GetValues<PrintQuoteStatus>()
            .Select(x => ((int)x, T("Plugins.Split3D.PrintQuote.Status." + x).Value))
            .ToList();
}
