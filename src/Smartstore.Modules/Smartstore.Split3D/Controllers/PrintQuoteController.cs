using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Data;
using Smartstore.Http;
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
    private readonly PrintQuoteFollowUpService _followUpService;
    private readonly PrintOrderService _printOrderService;
    private readonly StudioStorefrontSetup _studioSetup;
    private readonly StudioSettings _settings;

    public PrintQuoteController(
        SmartDbContext db,
        PrintQuoteService quoteService,
        PrintQuoteFollowUpService followUpService,
        PrintOrderService printOrderService,
        StudioStorefrontSetup studioSetup,
        StudioSettings settings)
    {
        _db = db;
        _quoteService = quoteService;
        _followUpService = followUpService;
        _printOrderService = printOrderService;
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
            OpenCount = await quotes.CountAsync(x => x.StatusId == (int)PrintQuoteStatus.Contacted
                || x.StatusId == (int)PrintQuoteStatus.Quoted
                || x.StatusId == (int)PrintQuoteStatus.Printing),
            DueCount = await _followUpService.CountDueAsync()
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
        var jobs = await LoadJobsAsync(rows);

        return Json(new GridModel<PrintQuoteModel>
        {
            Rows = rows.Select(x => ToModel(x, technologies, jobs)).ToList(),
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
        ViewBag.Channels = GetChannels();
        ViewBag.DepositPercent = _printOrderService.DepositPercent;

        var model = ToModel(request, PrintPriceList.Parse(_settings.PrintPriceTable), await LoadJobsAsync([request]));
        model.ContactLog = (await _followUpService.GetLogAsync(request.Id))
            .Select(x => new PrintQuoteContactModel
            {
                Id = x.Id,
                CreatedOn = Services.DateTimeHelper.ConvertToUserTime(x.CreatedOnUtc, DateTimeKind.Utc),
                ChannelId = x.ChannelId,
                ChannelName = PrintQuoteFollowUpService.ChannelName(x.Channel),
                ChannelIcon = PrintQuoteFollowUpService.ChannelIcon(x.Channel),
                IsIncoming = x.IsIncoming,
                Message = x.Message,
                UserName = x.UserName
            })
            .ToList();

        return View(model);
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
        request.FollowUpOnUtc = model.FollowUpOn?.Date;
        request.UpdatedOnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Edit), new { id = request.Id });
    }

    /// <summary>
    /// Writes a contact into the log of a request: the studio called, sent a Zalo or SMS message, chatted
    /// on Messenger or mailed. Called by the contact buttons (AJAX) and by the manual form.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> LogContact(PrintQuoteActionModel model)
    {
        var request = await _db.PrintQuoteRequests().FindByIdAsync(model.Id);
        if (request == null)
        {
            return NotFound();
        }

        if (!Enum.IsDefined(typeof(PrintContactChannel), model.ChannelId))
        {
            return BadRequest();
        }

        var user = Services.WorkContext.CurrentCustomer;
        var entry = await _followUpService.LogAsync(
            request,
            (PrintContactChannel)model.ChannelId,
            model.Message,
            model.IsIncoming,
            user.Id,
            user.GetFullName().NullEmpty() ?? user.Email,
            model.FollowUpDays);

        if (Request.IsAjax())
        {
            return Json(new
            {
                success = true,
                statusName = T("Plugins.Split3D.PrintQuote.Status." + request.Status).Value,
                contactCount = request.ContactCount,
                channelName = PrintQuoteFollowUpService.ChannelName(entry.Channel)
            });
        }

        NotifySuccess(T("Plugins.Split3D.PrintQuote.ContactLogged", PrintQuoteFollowUpService.ChannelName(entry.Channel)));

        return RedirectToAction(nameof(Edit), new { id = request.Id });
    }

    /// <summary>
    /// Schedules the next follow-up, or clears it with <paramref name="days"/> = 0.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> FollowUp(int id, int days)
    {
        var request = await _db.PrintQuoteRequests().FindByIdAsync(id);
        if (request == null)
        {
            return NotFound();
        }

        await _followUpService.SetFollowUpAsync(request, days > 0 ? DateTime.UtcNow.AddDays(days) : null);

        NotifySuccess(days > 0
            ? T("Plugins.Split3D.PrintQuote.FollowUpSet", days)
            : T("Plugins.Split3D.PrintQuote.FollowUpCleared"));

        return RedirectToAction(nameof(Edit), new { id = request.Id });
    }

    /// <summary>
    /// Turns a request into a print job with the settled price. The customer accepts it by paying through
    /// the job's payment link, which the studio sends with the quote message.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> CreateOrder(PrintQuoteActionModel model)
    {
        var request = await _db.PrintQuoteRequests().FindByIdAsync(model.Id);
        if (request == null)
        {
            return NotFound();
        }

        if (model.Price <= 0)
        {
            NotifyError(T("Plugins.Split3D.PrintQuote.PriceRequired"));
            return RedirectToAction(nameof(Edit), new { id = request.Id });
        }

        if (request.PrintOrderId > 0)
        {
            var existing = await _db.PrintOrders().FindByIdAsync(request.PrintOrderId, false);
            if (existing != null && existing.OrderId == 0 && existing.Status != PrintOrderStatus.Cancelled)
            {
                NotifyWarning(T("Plugins.Split3D.PrintQuote.OrderExists", existing.Code!));
                return RedirectToAction(nameof(Edit), new { id = request.Id });
            }
        }

        var job = await _printOrderService.CreateFromQuoteAsync(request, model.Price, model.DepositPercent > 0 ? model.DepositPercent : null);

        await _followUpService.LogAsync(
            request,
            PrintContactChannel.Note,
            T("Plugins.Split3D.PrintQuote.OrderCreatedLog", job.Code!, PrintPriceList.FormatPrice(job.PriceEstimate)).Value,
            userId: Services.WorkContext.CurrentCustomer.Id,
            userName: Services.WorkContext.CurrentCustomer.GetFullName().NullEmpty() ?? Services.WorkContext.CurrentCustomer.Email);

        NotifySuccess(T("Plugins.Split3D.PrintQuote.OrderCreated", job.Code!));

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
            DepositPercent = _settings.DepositPercent,
            AllowPickup = _settings.AllowPickup,
            DepositNote = _settings.DepositNote,
            NameplateMinLength = _settings.NameplateMinLength,
            NameplateMaxLength = _settings.NameplateMaxLength,
            NameplateBaseLength = _settings.NameplateBaseLength,
            NameplatePercentPerCm = _settings.NameplatePercentPerCm,
            QuoteMessageTemplate = _settings.QuoteMessageTemplate,
            FdmWeightFactor = _settings.FdmWeightFactor,
            ResinWeightFactor = _settings.ResinWeightFactor,
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

        if (model.NameplateMinLength > model.NameplateMaxLength)
        {
            ModelState.AddModelError(nameof(model.NameplateMaxLength), T("Plugins.Split3D.Studio.NameplateLengthRangeInvalid"));
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
        _settings.DepositPercent = Math.Clamp(model.DepositPercent, 1, 100);
        _settings.AllowPickup = model.AllowPickup;
        _settings.DepositNote = model.DepositNote?.Trim();
        _settings.NameplateMinLength = model.NameplateMinLength;
        _settings.NameplateMaxLength = model.NameplateMaxLength;
        _settings.NameplateBaseLength = Math.Clamp(model.NameplateBaseLength, model.NameplateMinLength, model.NameplateMaxLength);
        _settings.NameplatePercentPerCm = model.NameplatePercentPerCm;
        _settings.QuoteMessageTemplate = model.QuoteMessageTemplate?.Trim().NullEmpty() ?? StudioSettings.DefaultQuoteMessageTemplate;
        _settings.FdmWeightFactor = model.FdmWeightFactor;
        _settings.ResinWeightFactor = model.ResinWeightFactor;

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

    /// <summary>
    /// Loads the print jobs created from the given requests, by request id.
    /// </summary>
    private async Task<Dictionary<int, PrintOrder>> LoadJobsAsync(IEnumerable<PrintQuoteRequest> requests)
    {
        var ids = requests.Where(x => x.PrintOrderId > 0).Select(x => x.PrintOrderId).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var jobs = await _db.PrintOrders().AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync();

        return jobs.ToDictionary(x => x.Id);
    }

    private PrintQuoteModel ToModel(PrintQuoteRequest x, List<PrintTechnology> technologies, Dictionary<int, PrintOrder> jobs = null)
    {
        var status = x.Status;
        var job = x.PrintOrderId > 0 && (jobs?.TryGetValue(x.PrintOrderId, out var found) ?? false) ? found : null;
        var payUrl = job != null && job.PayToken.HasValue() && job.OrderId == 0 ? _printOrderService.BuildPayUrl(job) : null;
        var message = _followUpService.BuildMessage(x, payUrl);
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
            DownloadUrl = x.FilePath.HasValue() ? Url.Action(nameof(Download), new { id = x.Id }) : null,
            ContactCount = x.ContactCount,
            LastContactOn = x.LastContactOnUtc.HasValue
                ? Services.DateTimeHelper.ConvertToUserTime(x.LastContactOnUtc.Value, DateTimeKind.Utc)
                : null,
            FollowUpOn = x.FollowUpOnUtc.HasValue
                ? Services.DateTimeHelper.ConvertToUserTime(x.FollowUpOnUtc.Value, DateTimeKind.Utc)
                : null,
            IsDue = status == PrintQuoteStatus.New || (x.FollowUpOnUtc.HasValue && x.FollowUpOnUtc <= DateTime.UtcNow),
            PrintOrderId = x.PrintOrderId,
            PrintOrderCode = job?.Code,
            PrintOrderUrl = job != null ? Url.Action("Edit", "PrintJob", new { id = job.Id }) : null,
            PrintOrderStatusName = job != null ? T("Plugins.Split3D.PrintJob.Status." + job.Status).Value : null,
            PayUrl = payUrl,
            Message = message,
            TelUrl = PrintQuoteFollowUpService.TelUrl(x.Phone),
            ZaloUrl = PrintQuoteFollowUpService.ZaloUrl(x.Phone),
            SmsUrl = PrintQuoteFollowUpService.SmsUrl(x.Phone, message),
            MailUrl = x.Email.HasValue()
                ? PrintQuoteFollowUpService.MailToUrl(x.Email, $"[{_settings.BrandName}] Báo giá in 3D #{x.Id}", message)
                : null,
            MessengerUrl = _settings.FacebookUrl.NullEmpty()
        };
    }

    private List<(int Id, string Name)> GetStatuses()
        => Enum.GetValues<PrintQuoteStatus>()
            .Select(x => ((int)x, T("Plugins.Split3D.PrintQuote.Status." + x).Value))
            .ToList();

    private static List<(int Id, string Name)> GetChannels()
        => Enum.GetValues<PrintContactChannel>()
            .Select(x => ((int)x, PrintQuoteFollowUpService.ChannelName(x)))
            .ToList();
}
