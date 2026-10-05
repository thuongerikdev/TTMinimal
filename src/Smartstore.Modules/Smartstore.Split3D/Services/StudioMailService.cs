using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Messaging;
using Smartstore.Core.Stores;
using Smartstore.Utilities;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Sends the key email through Smartstore's message templates (Admin &gt; CMS &gt; Message templates) and
/// provides the studio branding (<c>Studio</c> model part, theme colors) used by every store email.
/// </summary>
public partial class StudioMailService
{
    /// <summary>
    /// System name of the key email message template.
    /// </summary>
    public const string LicenseTemplateName = "Split3D.LicenseKey.CustomerNotification";

    public const string LicenseModelName = "License";
    public const string DefaultLicenseSubject = "[{{ Store.Name }}] Key kích hoạt {{ License.Addon }}";
    public const string DefaultLicenseTo = "{{ License.Email }}";

    private const string LicenseBodyResource = "Smartstore.Split3D.Mail.LicenseKey.liquid";

    /// <summary>
    /// SHA-256 of earlier shipped default bodies (trimmed, LF line ends). A stored template still equal to one of
    /// them was never customized and is upgraded to the current default.
    /// </summary>
    private static readonly string[] _previousDefaultBodyHashes =
    [
        "0405845b2608e3b6fa00d2b1bd37955f464dfacf642dada95bfd9ba70c6959bf"
    ];
    private static readonly CultureInfo _vnCulture = CultureInfo.GetCultureInfo("vi-VN");

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();

    private readonly SmartDbContext _db;
    private readonly IMessageFactory _messageFactory;
    private readonly IEmailAccountService _emailAccountService;
    private readonly ILanguageService _languageService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly Split3DRepoService _repoService;
    private readonly Split3DSettings _settings;

    public StudioMailService(
        SmartDbContext db,
        IMessageFactory messageFactory,
        IEmailAccountService emailAccountService,
        ILanguageService languageService,
        IStoreContext storeContext,
        IWorkContext workContext,
        Split3DRepoService repoService,
        Split3DSettings settings)
    {
        _db = db;
        _messageFactory = messageFactory;
        _emailAccountService = emailAccountService;
        _languageService = languageService;
        _storeContext = storeContext;
        _workContext = workContext;
        _repoService = repoService;
        _settings = settings;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Unsaved layout settings used instead of the stored ones while rendering a preview.
    /// </summary>
    public StudioMailSettings LayoutOverride { get; set; }

    #region License email

    /// <summary>
    /// Gets the key email message template, creating it with the default content if it does not exist yet.
    /// </summary>
    public async Task<MessageTemplate> GetOrCreateLicenseTemplateAsync(CancellationToken cancelToken = default)
    {
        var template = await _db.MessageTemplates.FirstOrDefaultAsync(x => x.Name == LicenseTemplateName, cancelToken);
        if (template != null)
        {
            if (_previousDefaultBodyHashes.Contains(HashBody(template.Body)))
            {
                template.Body = GetDefaultLicenseBody();
                await _db.SaveChangesAsync(cancelToken);
            }

            return template;
        }

        template = new MessageTemplate
        {
            Name = LicenseTemplateName,
            To = DefaultLicenseTo,
            Subject = DefaultLicenseSubject,
            Body = GetDefaultLicenseBody(),
            ModelTypes = LicenseModelName,
            IsActive = true
        };

        _db.MessageTemplates.Add(template);
        await _db.SaveChangesAsync(cancelToken);

        return template;
    }

    /// <summary>
    /// The shipped body of the key email (Liquid).
    /// </summary>
    public static string GetDefaultLicenseBody()
    {
        using var stream = typeof(StudioMailService).Assembly.GetManifestResourceStream(LicenseBodyResource)
            ?? throw new InvalidOperationException($"Embedded resource {LicenseBodyResource} is missing.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static string HashBody(string body)
    {
        var normalized = (body ?? string.Empty).Replace("\r\n", "\n").Trim();
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>
    /// Creates the key email from the message template and puts it into the send queue (commits).
    /// The email links the newest installer of the key's addon (direct download, no login) and attaches it
    /// when <see cref="Split3DSettings.AttachInstaller"/> is on.
    /// </summary>
    /// <returns><c>false</c> if the template is inactive or no email account is configured.</returns>
    public async Task<bool> QueueLicenseEmailAsync(Split3DLicense license, CancellationToken cancelToken = default)
    {
        Guard.NotNull(license);

        var template = await GetOrCreateLicenseTemplateAsync(cancelToken);
        if (!template.IsActive)
        {
            Logger.Warn($"Split3D: the message template '{LicenseTemplateName}' is inactive, key email for {license.Email} not sent.");
            return false;
        }

        if (template.EmailAccountId == 0 && _emailAccountService.GetDefaultEmailAccount() == null)
        {
            Logger.Warn("Split3D: cannot send the key email because no email account is configured.");
            return false;
        }

        var order = license.OrderId > 0
            ? await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == license.OrderId, cancelToken)
            : null;

        var installer = license.AddonId > 0 ? await _repoService.GetLatestPackageAsync(license.AddonId, cancelToken) : null;
        var part = await CreateLicensePartAsync(license, order, installer?.Package, cancelToken);

        var storeId = order?.StoreId > 0 ? order.StoreId : _storeContext.CurrentStore.Id;
        var messageContext = new MessageContext
        {
            MessageTemplate = template,
            StoreId = storeId,
            LanguageId = _languageService.GetMasterLanguageId(storeId),
            Customer = await FindCustomerAsync(license, order, cancelToken)
        };

        var result = await _messageFactory.CreateMessageAsync(messageContext, false, part);
        if (result.Email == null)
        {
            return false;
        }

        if (_settings.AttachInstaller && installer is { } entry && entry.Download.MediaFileId is int mediaFileId)
        {
            result.Email.Attachments.Add(new QueuedEmailAttachment
            {
                StorageLocation = EmailAttachmentStorageLocation.FileReference,
                MediaFileId = mediaFileId,
                Name = entry.Package.FileName,
                MimeType = entry.Download.MediaFile?.MimeType.NullEmpty() ?? "application/zip"
            });
        }
        else if (installer == null && license.AddonId > 0)
        {
            Logger.Warn($"Split3D: no installer file uploaded for addon {license.AddonId}; the key email has no download.");
        }

        license.EmailSent = true;
        await _messageFactory.QueueMessageAsync(messageContext, result.Email);

        return true;
    }

    /// <summary>
    /// Creates the <c>License</c> model part of the key email.
    /// </summary>
    public async Task<NamedModelPart> CreateLicensePartAsync(
        Split3DLicense license,
        Order order,
        Split3DPackageInfo installer,
        CancellationToken cancelToken = default)
    {
        var addonName = license.AddonId > 0
            ? await _db.Split3DAddons().Where(x => x.Id == license.AddonId).Select(x => x.Name).FirstOrDefaultAsync(cancelToken)
            : null;

        var store = order != null ? _storeContext.GetStoreById(order.StoreId) ?? _storeContext.CurrentStore : _storeContext.CurrentStore;
        var baseUrl = store.GetBaseUrl().TrimEnd('/');

        var part = new NamedModelPart(LicenseModelName)
        {
            ["CustomerName"] = license.CustomerName,
            ["Email"] = license.Email,
            ["Addon"] = addonName.NullEmpty() ?? license.ProductCode,
            ["ProductCode"] = license.ProductCode,
            ["Plan"] = license.KeyType,
            ["IsLifetime"] = !license.ExpiresOnUtc.HasValue,
            ["ExpiresOn"] = license.ExpiresOnUtc.HasValue
                ? license.ExpiresOnUtc.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : Split3DPlans.Lifetime,
            ["IssuedOn"] = license.IssuedOnUtc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["Price"] = Split3DLicenseService.FormatPrice(license.Price),
            ["HasPrice"] = license.Price > 0,
            ["MaxDevices"] = Math.Max(1, license.MaxDevices ?? _settings.DefaultMaxDevices),
            ["Key"] = license.Token,
            ["KeyId"] = license.LicenseId.NullEmpty()?.Truncate(8) ?? license.Id.ToString(CultureInfo.InvariantCulture),
            ["OrderNumber"] = order?.GetOrderNumber(),
            ["OrderUrl"] = order != null ? $"{baseUrl}/order/details/{order.Id}" : null,
            ["MyKeysUrl"] = $"{baseUrl}/split3dkeys"
        };

        if (installer != null)
        {
            part["Installer"] = new Dictionary<string, object>
            {
                ["FileName"] = installer.FileName,
                ["Version"] = installer.Version,
                ["Size"] = Prettifier.HumanizeBytes(installer.Size),
                ["Attached"] = _settings.AttachInstaller,
                ["DownloadUrl"] = Split3DRepoService.GetDownloadUrl(license, baseUrl)
            };
        }

        return part;
    }

    /// <summary>
    /// A made-up key for previews and test emails.
    /// </summary>
    public async Task<NamedModelPart> CreateDemoLicensePartAsync(CancellationToken cancelToken = default)
    {
        var addon = await _db.Split3DAddons().AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancelToken);
        var baseUrl = _storeContext.CurrentStore.GetBaseUrl().TrimEnd('/');
        var expires = DateTime.UtcNow.AddDays(365);

        return new NamedModelPart(LicenseModelName)
        {
            ["CustomerName"] = "Nguyễn Văn An",
            ["Email"] = "khachhang@example.com",
            ["Addon"] = addon?.Name ?? "Split3D Print",
            ["ProductCode"] = addon?.ProductCode ?? "split3d-print",
            ["Plan"] = Split3DPlans.OneYear,
            ["IsLifetime"] = false,
            ["ExpiresOn"] = expires.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["IssuedOn"] = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["Price"] = 390000m.ToString("#,##0", _vnCulture.NumberFormat).Replace('.', ',') + " VND",
            ["HasPrice"] = true,
            ["MaxDevices"] = Math.Max(1, _settings.DefaultMaxDevices),
            ["Key"] = "eyJwcm9kdWN0Ijoic3BsaXQzZC1wcmludCIsImN1c3RvbWVyIjoiTmd1eWVuIFZhbiBBbiIsImV4cGlyZXMiOjE3OTk5OTk5OTl9.DEMO-KEY-7f3a9c2e81b44d0f9a6e5c1b2d3f4a5b6c7d8e9f",
            ["KeyId"] = "7f3a9c2e",
            ["OrderNumber"] = "1024",
            ["OrderUrl"] = $"{baseUrl}/order/details/1024",
            ["MyKeysUrl"] = $"{baseUrl}/split3dkeys",
            ["Installer"] = new Dictionary<string, object>
            {
                ["FileName"] = "split3d_print-3.0.140.zip",
                ["Version"] = "3.0.140",
                ["Size"] = "1.2 MB",
                ["Attached"] = _settings.AttachInstaller,
                // A made-up key has no download folder: the demo button opens "My keys".
                ["DownloadUrl"] = $"{baseUrl}/split3dkeys"
            }
        };
    }

    private async Task<Customer> FindCustomerAsync(Split3DLicense license, Order order, CancellationToken cancelToken)
    {
        var customerId = order?.CustomerId ?? license.CustomerId;
        var customer = customerId > 0
            ? await _db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancelToken)
            : null;

        if (customer == null && license.Email.HasValue())
        {
            customer = await _db.Customers
                .Where(x => !x.Deleted && x.Email == license.Email)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancelToken);
        }

        customer ??= _workContext.CurrentCustomer;

        // A system account (e.g. the background task customer) cannot receive messages.
        return customer?.IsSystemAccount == true ? null : customer;
    }

    #endregion

    #region Branding

    /// <summary>
    /// Creates the <c>Studio</c> model part rendered by the TT Minimal email layout.
    /// </summary>
    public static Dictionary<string, object> CreateStudioPart(StudioMailSettings mail, StudioSettings studio, string storeUrl)
    {
        Guard.NotNull(mail);
        Guard.NotNull(studio);

        var host = Uri.TryCreate(storeUrl, UriKind.Absolute, out var uri) ? uri.Host : storeUrl;
        var zalo = studio.ZaloPhone?.Where(char.IsDigit).ToArray();

        return new Dictionary<string, object>
        {
            ["Layout"] = mail.Layout is "clean" ? "clean" : "pop",
            ["HeaderStyle"] = mail.HeaderStyle is "accent" or "light" ? mail.HeaderStyle : "ink",
            ["ShowLogo"] = mail.ShowLogo,
            ["BrandName"] = studio.BrandName.NullEmpty(),
            ["HeaderNote"] = mail.HeaderNote.NullEmpty() ?? studio.Tagline.NullEmpty(),
            ["FooterLines"] = (mail.FooterText ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            ["ShowContact"] = mail.ShowContact,
            ["Address"] = studio.Address.NullEmpty(),
            ["Phone1"] = studio.Phone1.NullEmpty(),
            ["Phone2"] = studio.Phone2.NullEmpty(),
            ["ZaloUrl"] = zalo?.Length > 0 ? "https://zalo.me/" + new string(zalo) : null,
            ["FacebookUrl"] = studio.FacebookUrl.NullEmpty(),
            ["StoreHost"] = host
        };
    }

    /// <summary>
    /// Creates the <c>Theme</c> model part (colors and font of every email) from the studio colors.
    /// </summary>
    public static Dictionary<string, object> CreateThemePart(StudioMailSettings mail)
    {
        Guard.NotNull(mail);

        var defaults = new StudioMailSettings();
        var ink = Color(mail.InkColor, defaults.InkColor);

        return new Dictionary<string, object>
        {
            ["FontFamily"] = "'Be Vietnam Pro', -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
            ["BodyBg"] = Color(mail.BackgroundColor, defaults.BackgroundColor),
            ["BodyColor"] = "#3b3a36",
            ["TitleColor"] = ink,
            ["ContentBg"] = "#ffffff",
            ["ShadeColor"] = "#ece3c8",
            ["LinkColor"] = ink,
            ["BrandPrimary"] = Color(mail.AccentColor, defaults.AccentColor),
            ["BrandSuccess"] = "#1f9a73",
            ["BrandWarning"] = Color(mail.HighlightColor, defaults.HighlightColor),
            ["BrandDanger"] = "#e5484d",
            ["MutedColor"] = "#6d685d"
        };
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a #rgb or #rrggbb color. Colors are written into CSS, nothing else is allowed.
    /// </summary>
    public static bool IsHexColor(string value)
        => value.HasValue() && HexColorRegex().IsMatch(value);

    private static string Color(string value, string fallback)
        => IsHexColor(value?.Trim()) ? value.Trim() : fallback;

    #endregion
}
