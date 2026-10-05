using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Messaging;
using Smartstore.Core.Security;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// "TT Minimal Studio &gt; Email": layout of all store emails and the key email template, with live preview and test sending.
/// </summary>
public class StudioMailController : AdminController
{
    private static readonly string[] _previewTemplateNames =
    [
        "OrderPlaced.CustomerNotification",
        "OrderCompleted.CustomerNotification",
        "Customer.NewOrderNote",
        "Customer.WelcomeMessage",
        "Customer.PasswordRecovery"
    ];

    private readonly SmartDbContext _db;
    private readonly StudioMailService _mailService;
    private readonly IMessageFactory _messageFactory;
    private readonly IEmailAccountService _emailAccountService;
    private readonly ILocalizedEntityService _localizedEntityService;
    private readonly ILanguageService _languageService;
    private readonly StudioMailSettings _mailSettings;
    private readonly Split3DSettings _split3DSettings;

    public StudioMailController(
        SmartDbContext db,
        StudioMailService mailService,
        IMessageFactory messageFactory,
        IEmailAccountService emailAccountService,
        ILocalizedEntityService localizedEntityService,
        ILanguageService languageService,
        StudioMailSettings mailSettings,
        Split3DSettings split3DSettings)
    {
        _db = db;
        _mailService = mailService;
        _messageFactory = messageFactory;
        _emailAccountService = emailAccountService;
        _localizedEntityService = localizedEntityService;
        _languageService = languageService;
        _mailSettings = mailSettings;
        _split3DSettings = split3DSettings;
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Index()
    {
        var template = await _mailService.GetOrCreateLicenseTemplateAsync();
        var languageId = _languageService.GetMasterLanguageId();

        var model = new StudioMailModel
        {
            BrandingEnabled = _mailSettings.BrandingEnabled,
            Layout = _mailSettings.Layout,
            HeaderStyle = _mailSettings.HeaderStyle,
            ShowLogo = _mailSettings.ShowLogo,
            AccentColor = _mailSettings.AccentColor,
            HighlightColor = _mailSettings.HighlightColor,
            BackgroundColor = _mailSettings.BackgroundColor,
            InkColor = _mailSettings.InkColor,
            HeaderNote = _mailSettings.HeaderNote,
            FooterText = _mailSettings.FooterText,
            ShowContact = _mailSettings.ShowContact,
            SendEmail = _split3DSettings.SendEmail,
            TemplateId = template.Id,
            TemplateActive = template.IsActive,
            Subject = template.GetLocalized(x => x.Subject, languageId, false, false).Value.NullEmpty() ?? template.Subject,
            Body = template.GetLocalized(x => x.Body, languageId, false, false).Value.NullEmpty() ?? template.Body,
            EmailAccountId = template.EmailAccountId,
            Bcc = template.BccEmailAddresses,
            TestEmail = Services.WorkContext.CurrentCustomer.Email
        };

        await PrepareModelAsync(model);

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Index(StudioMailModel model)
    {
        ValidateModel(model);

        if (!ModelState.IsValid)
        {
            await PrepareModelAsync(model);
            return View(model);
        }

        ApplyLayout(model, _mailSettings);
        await Services.SettingFactory.SaveSettingsAsync(_mailSettings);

        _split3DSettings.SendEmail = model.SendEmail;
        await Services.SettingFactory.SaveSettingsAsync(_split3DSettings);

        var template = await _mailService.GetOrCreateLicenseTemplateAsync();
        template.IsActive = model.TemplateActive;
        template.Subject = model.Subject.Trim();
        template.Body = model.Body;
        template.EmailAccountId = model.EmailAccountId;
        template.BccEmailAddresses = model.Bcc?.Trim().NullEmpty();

        // This page edits one text for all languages: drop translations that would override it.
        foreach (var language in await _db.Languages.AsNoTracking().ToListAsync())
        {
            await _localizedEntityService.ApplyLocalizedValueAsync(template, x => x.Subject, null, language.Id);
            await _localizedEntityService.ApplyLocalizedValueAsync(template, x => x.Body, null, language.Id);
        }

        await _db.SaveChangesAsync();

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Renders an email with the unsaved form values.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Preview(StudioMailModel model, string previewTemplate)
    {
        try
        {
            var result = await RenderAsync(model, previewTemplate);
            if (result.Email == null)
            {
                return Json(new { success = false, message = T("Plugins.Split3D.Mail.TemplateMissing", previewTemplate).Value });
            }

            return Json(new
            {
                success = true,
                subject = result.Email.Subject,
                from = result.Email.From,
                to = result.Email.To,
                attachments = result.Email.Attachments.Select(x => x.Name).ToArray(),
                html = result.Email.Body
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Queues the previewed email (unsaved form values, demo data) to <see cref="StudioMailModel.TestEmail"/>.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> SendTest(StudioMailModel model, string previewTemplate)
    {
        var to = model.TestEmail?.Trim();
        if (to.IsEmpty() || !to.IsEmail())
        {
            return Json(new { success = false, message = T("Plugins.Split3D.Mail.TestEmailInvalid").Value });
        }

        try
        {
            var result = await RenderAsync(model, previewTemplate);
            if (result.Email == null)
            {
                return Json(new { success = false, message = T("Plugins.Split3D.Mail.TemplateMissing", previewTemplate).Value });
            }

            result.Email.To = to;
            result.Email.Bcc = null;
            result.Email.ReplyTo = null;
            result.Email.SendManually = false;
            result.Email.Subject = "[TEST] " + result.Email.Subject;
            result.Email.Priority = 1;

            await _messageFactory.QueueMessageAsync(result.MessageContext, result.Email);

            return Json(new { success = true, message = T("Plugins.Split3D.Mail.TestQueued", to).Value });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Studio mail: test email failed.");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Read)]
    public IActionResult DefaultBody()
    {
        return Json(new
        {
            subject = StudioMailService.DefaultLicenseSubject,
            body = StudioMailService.GetDefaultLicenseBody()
        });
    }

    private async Task<CreateMessageResult> RenderAsync(StudioMailModel model, string templateName)
    {
        _mailService.LayoutOverride = ApplyLayout(model, new StudioMailSettings());

        var messageContext = new MessageContext
        {
            TestMode = true,
            LanguageId = _languageService.GetMasterLanguageId()
        };

        object[] parts = [];

        if (templateName.IsEmpty() || templateName == StudioMailService.LicenseTemplateName)
        {
            var saved = await _mailService.GetOrCreateLicenseTemplateAsync();

            // Transient copy: renders the unsaved subject and body without touching the stored template.
            messageContext.MessageTemplate = new MessageTemplate
            {
                Name = saved.Name,
                To = saved.To.NullEmpty() ?? StudioMailService.DefaultLicenseTo,
                Subject = model.Subject.NullEmpty() ?? StudioMailService.DefaultLicenseSubject,
                Body = model.Body.NullEmpty() ?? StudioMailService.GetDefaultLicenseBody(),
                ModelTypes = StudioMailService.LicenseModelName,
                EmailAccountId = model.EmailAccountId,
                IsActive = true
            };

            parts = [await _mailService.CreateDemoLicensePartAsync()];
        }
        else
        {
            if (!_previewTemplateNames.Contains(templateName))
            {
                throw new ArgumentException($"Unknown template '{templateName}'.", nameof(templateName));
            }

            messageContext.MessageTemplateName = templateName;
        }

        // The message factory resolves the sender from the template, falling back to the default account.
        if (!await _db.EmailAccounts.AnyAsync())
        {
            throw new InvalidOperationException(T("Plugins.Split3D.Mail.NoAccount"));
        }

        return await _messageFactory.CreateMessageAsync(messageContext, false, parts);
    }

    private void ValidateModel(StudioMailModel model)
    {
        foreach (var (name, value) in new[]
        {
            (nameof(model.AccentColor), model.AccentColor),
            (nameof(model.HighlightColor), model.HighlightColor),
            (nameof(model.BackgroundColor), model.BackgroundColor),
            (nameof(model.InkColor), model.InkColor)
        })
        {
            if (!StudioMailService.IsHexColor(value?.Trim()))
            {
                ModelState.AddModelError(name, T("Plugins.Split3D.Mail.ColorInvalid"));
            }
        }

        if (model.Subject.IsEmpty())
        {
            ModelState.AddModelError(nameof(model.Subject), T("Plugins.Split3D.Mail.SubjectRequired"));
        }

        if (model.Body.IsEmpty())
        {
            ModelState.AddModelError(nameof(model.Body), T("Plugins.Split3D.Mail.BodyRequired"));
        }
    }

    private static StudioMailSettings ApplyLayout(StudioMailModel model, StudioMailSettings settings)
    {
        settings.BrandingEnabled = model.BrandingEnabled;
        settings.Layout = model.Layout is "clean" ? "clean" : "pop";
        settings.HeaderStyle = model.HeaderStyle is "accent" or "light" ? model.HeaderStyle : "ink";
        settings.ShowLogo = model.ShowLogo;
        settings.AccentColor = model.AccentColor?.Trim();
        settings.HighlightColor = model.HighlightColor?.Trim();
        settings.BackgroundColor = model.BackgroundColor?.Trim();
        settings.InkColor = model.InkColor?.Trim();
        settings.HeaderNote = model.HeaderNote?.Trim().NullEmpty();
        settings.FooterText = model.FooterText?.Trim();
        settings.ShowContact = model.ShowContact;

        return settings;
    }

    private async Task PrepareModelAsync(StudioMailModel model)
    {
        var accounts = await _db.EmailAccounts.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var defaultAccount = _emailAccountService.GetDefaultEmailAccount();

        model.DefaultAccountEmail = defaultAccount?.Email;
        model.EmailAccounts = accounts
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.DisplayName.HasValue() ? $"{x.DisplayName} <{x.Email}>" : x.Email,
                Selected = x.Id == model.EmailAccountId
            })
            .ToList();

        var existing = await _db.MessageTemplates
            .AsNoTracking()
            .Where(x => _previewTemplateNames.Contains(x.Name))
            .Select(x => x.Name)
            .ToListAsync();

        model.PreviewTemplates =
        [
            new SelectListItem { Value = StudioMailService.LicenseTemplateName, Text = T("Plugins.Split3D.Mail.Preview.License"), Selected = true },
            .. _previewTemplateNames
                .Where(existing.Contains)
                .Select(x => new SelectListItem { Value = x, Text = T("Plugins.Split3D.Mail.Preview." + x.Replace(".", string.Empty)) })
        ];
    }
}
