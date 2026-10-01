using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Core.DataExchange.Import;
using Smartstore.Core.Messaging;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Input for issuing a key manually.
/// </summary>
public class Split3DIssueRequest
{
    /// <summary>
    /// The addon the key unlocks. Its product code is embedded in the signed payload.
    /// </summary>
    public Split3DAddon Addon { get; set; }

    public string Email { get; set; }
    public string CustomerName { get; set; }
    public string Phone { get; set; }
    public string KeyType { get; set; } = Split3DPlans.OneYear;

    /// <summary>
    /// Only used for <see cref="Split3DPlans.Custom"/>.
    /// </summary>
    public int? Days { get; set; }

    public decimal Price { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string Notes { get; set; }
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public int CustomerId { get; set; }

    /// <summary>
    /// Devices the key may activate. <c>null</c> uses the default from the settings.
    /// </summary>
    public int? MaxDevices { get; set; }
}

/// <summary>
/// What a catalog product grants: an addon and a plan.
/// </summary>
public sealed record Split3DProductPlan(Split3DAddon Addon, string KeyType, int? Days, int? MaxDevices = null);

public class Split3DImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; } = [];
}

public class Split3DLicenseService
{
    private static readonly CultureInfo _vnCulture = CultureInfo.GetCultureInfo("vi-VN");

    private readonly SmartDbContext _db;
    private readonly Split3DSettings _settings;
    private readonly IEmailAccountService _emailAccountService;

    public Split3DLicenseService(SmartDbContext db, Split3DSettings settings, IEmailAccountService emailAccountService)
    {
        _db = db;
        _settings = settings;
        _emailAccountService = emailAccountService;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Creates a signer from the configured keys.
    /// </summary>
    /// <exception cref="Split3DKeyException">Keys are missing or invalid.</exception>
    public Split3DKeySigner CreateSigner(bool requirePrivateKey)
    {
        var signer = Split3DKeySigner.Create(_settings.PublicKeyJson, _settings.PrivateKeyJson);
        if (requirePrivateKey && !signer.CanSign)
        {
            throw new Split3DKeyException("Private signing key (private_key.json) is not configured.");
        }

        return signer;
    }

    /// <summary>
    /// Validates the request, signs a new key and stores it (without committing).
    /// </summary>
    /// <exception cref="ArgumentException">The request is invalid.</exception>
    /// <exception cref="Split3DKeyException">Keys are missing or invalid.</exception>
    public Split3DLicense Issue(Split3DIssueRequest request, Split3DKeySigner signer = null)
    {
        Guard.NotNull(request);

        if (request.Addon == null || request.Addon.ProductCode.IsEmpty())
        {
            throw new ArgumentException("Please select the addon the key is for.", nameof(request));
        }

        var email = CleanEmail(request.Email);
        var customerName = request.CustomerName?.Trim();
        if (customerName.IsEmpty())
        {
            throw new ArgumentException("Customer name is required.", nameof(request));
        }

        var keyType = request.KeyType.NullEmpty() ?? Split3DPlans.OneYear;
        int? days;
        if (keyType == Split3DPlans.Custom)
        {
            days = request.Days;
            if (days is null or < 1 or > Split3DPlans.MaxDays)
            {
                throw new ArgumentException($"Days must be between 1 and {Split3DPlans.MaxDays}.", nameof(request));
            }
        }
        else if (Split3DPlans.All.Contains(keyType))
        {
            days = Split3DPlans.GetFixedDays(keyType);
        }
        else
        {
            throw new ArgumentException($"Unknown plan '{keyType}'.", nameof(request));
        }

        if (request.Price < 0 || request.Price != decimal.Truncate(request.Price))
        {
            throw new ArgumentException("Price must be a non-negative integer VND amount.", nameof(request));
        }

        signer ??= CreateSigner(true);

        var issued = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new Split3DKeyPayload
        {
            Product = request.Addon.ProductCode,
            Customer = customerName,
            Email = email,
            Issued = issued,
            Expires = days.HasValue ? issued + days.Value * 86400L : null,
            Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12))
        };

        var license = new Split3DLicense
        {
            LicenseId = payload.Id,
            AddonId = request.Addon.Id,
            ProductCode = request.Addon.ProductCode,
            Email = email,
            CustomerName = customerName,
            Phone = request.Phone?.Trim().Truncate(100),
            KeyType = keyType,
            Days = days,
            Price = request.Price,
            PurchaseDateUtc = (request.PurchaseDate ?? DateTime.UtcNow).Date,
            IssuedOnUtc = DateTimeOffset.FromUnixTimeSeconds(payload.Issued).UtcDateTime,
            ExpiresOnUtc = payload.Expires.HasValue ? DateTimeOffset.FromUnixTimeSeconds(payload.Expires.Value).UtcDateTime : null,
            Token = signer.Sign(payload),
            Notes = request.Notes?.Trim(),
            OrderId = request.OrderId,
            OrderItemId = request.OrderItemId,
            CustomerId = request.CustomerId,
            MaxDevices = request.MaxDevices is > 0 ? request.MaxDevices : null
        };

        _db.Split3DLicenses().Add(license);

        return license;
    }

    /// <summary>
    /// Issues keys for all order items whose product is mapped to a plan. Idempotent per order item:
    /// already issued keys are counted and never issued twice.
    /// </summary>
    /// <returns>The newly issued licenses.</returns>
    public async Task<List<Split3DLicense>> IssueForOrderAsync(Order order, CancellationToken cancelToken = default)
    {
        Guard.NotNull(order);

        var plans = await GetProductPlansAsync(cancelToken);
        if (plans.Count == 0)
        {
            return [];
        }

        var productIds = plans.Keys.ToArray();
        var items = await _db.OrderItems
            .AsNoTracking()
            .Where(x => x.OrderId == order.Id && productIds.Contains(x.ProductId))
            .ToListAsync(cancelToken);

        if (items.Count == 0)
        {
            return [];
        }

        var itemIds = items.Select(x => x.Id).ToArray();
        var existing = await _db.Split3DLicenses()
            .Where(x => itemIds.Contains(x.OrderItemId))
            .GroupBy(x => x.OrderItemId)
            .Select(x => new { OrderItemId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.OrderItemId, x => x.Count, cancelToken);

        var billingAddress = order.BillingAddressId.HasValue
            ? await _db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == order.BillingAddressId.Value, cancelToken)
            : null;
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == order.CustomerId, cancelToken);

        var email = billingAddress?.Email.NullEmpty() ?? customer?.Email;
        var customerName = billingAddress?.GetFullName(false).NullEmpty() ?? customer?.FullName.NullEmpty() ?? email;

        var signer = CreateSigner(true);
        var result = new List<Split3DLicense>();

        foreach (var item in items)
        {
            var missing = item.Quantity - existing.GetValueOrDefault(item.Id);
            for (var i = 0; i < missing; i++)
            {
                var plan = plans[item.ProductId];
                result.Add(Issue(new Split3DIssueRequest
                {
                    Addon = plan.Addon,
                    Email = email,
                    CustomerName = customerName,
                    Phone = billingAddress?.PhoneNumber,
                    KeyType = plan.KeyType,
                    Days = plan.Days,
                    Price = decimal.Round(item.UnitPriceInclTax, 0),
                    PurchaseDate = order.CreatedOnUtc,
                    Notes = $"Order #{order.GetOrderNumber()}",
                    OrderId = order.Id,
                    OrderItemId = item.Id,
                    CustomerId = order.CustomerId,
                    MaxDevices = plan.MaxDevices
                }, signer));
            }
        }

        if (result.Count > 0)
        {
            await _db.SaveChangesAsync(cancelToken);

            var addonNames = plans.Values.Select(x => x.Addon).DistinctBy(x => x.Id).ToDictionary(x => x.Id, x => x.Name);
            var note = new StringBuilder("Split3D activation key(s):");
            foreach (var license in result)
            {
                note.AppendLine().AppendLine()
                    .AppendLine($"{addonNames.GetValueOrDefault(license.AddonId)} · {license.KeyType} · {FormatExpiry(license.ExpiresOnUtc)}")
                    .Append(license.Token);
            }

            _db.OrderNotes.Add(order, note.ToString(), displayToCustomer: true);

            if (_settings.SendEmail)
            {
                foreach (var license in result)
                {
                    QueueEmail(license);
                }
            }

            await _db.SaveChangesAsync(cancelToken);
        }

        return result;
    }

    /// <summary>
    /// Queues the key email for <paramref name="license"/> (without committing).
    /// </summary>
    /// <returns><c>false</c> if no email account is configured.</returns>
    public bool QueueEmail(Split3DLicense license)
    {
        Guard.NotNull(license);

        var emailAccount = _emailAccountService.GetDefaultEmailAccount();
        if (emailAccount == null)
        {
            Logger.Warn("Split3D: cannot send the key email because no email account is configured.");
            return false;
        }

        var addonName = license.AddonId > 0
            ? _db.Split3DAddons().Where(x => x.Id == license.AddonId).Select(x => x.Name).FirstOrDefault()
            : null;

        var subject = ReplacePlaceholders(_settings.EmailSubject.NullEmpty() ?? "Split3D activation key", license, addonName, html: false);
        var body = ReplacePlaceholders(_settings.EmailBody.NullEmpty() ?? "{Key}", license, addonName, html: true);

        if (addonName.HasValue() && !(_settings.EmailBody ?? string.Empty).Contains("{Addon}"))
        {
            // Older templates do not mention the addon: prefix it so multi-addon customers know which key is which.
            body = $"<p><strong>{System.Net.WebUtility.HtmlEncode(addonName)}</strong></p>" + body;
            subject = $"{subject} – {addonName}";
        }

        _db.QueuedEmails.Add(new QueuedEmail
        {
            From = emailAccount.ToMailAddress().ToString(),
            To = license.Email,
            Subject = subject,
            Body = $"<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.5\">{body}</div>",
            CreatedOnUtc = DateTime.UtcNow,
            EmailAccountId = emailAccount.Id,
            Priority = 5
        });

        license.EmailSent = true;

        return true;
    }

    /// <summary>
    /// Imports keys from a table (CSV/XLSX). The signed token is the source of truth for
    /// email, name, issue and expiry dates; the other columns are optional transaction info.
    /// </summary>
    public async Task<Split3DImportResult> ImportAsync(IDataTable table, CancellationToken cancelToken = default)
    {
        Guard.NotNull(table);

        var result = new Split3DImportResult();
        var signer = CreateSigner(false);

        var keyColumn = FindColumn(table, "Key", "Token");
        if (keyColumn == null)
        {
            result.Errors.Add("The file has no \"Key\" column.");
            return result;
        }

        var priceColumn = FindColumn(table, "Price", "Giá tiền (VND)", "Giá tiền");
        var purchaseColumn = FindColumn(table, "PurchaseDate", "Ngày mua");
        var keyTypeColumn = FindColumn(table, "KeyType", "Loại key", "Gói key");
        var phoneColumn = FindColumn(table, "Phone", "Điện thoại");
        var notesColumn = FindColumn(table, "Notes", "Ghi chú");
        var nameColumn = FindColumn(table, "Buyer", "Người mua");

        var existingIds = (await _db.Split3DLicenses().Select(x => x.LicenseId).ToListAsync(cancelToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var addons = (await _db.Split3DAddons().ToListAsync(cancelToken))
            .ToDictionary(x => x.ProductCode, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var token = GetString(row, keyColumn);
            if (token.IsEmpty())
            {
                continue;
            }

            Split3DKeyPayload payload;
            try
            {
                payload = signer.Verify(token);
            }
            catch (Split3DKeyException ex)
            {
                result.Errors.Add($"Row {i + 2}: {ex.Message}");
                continue;
            }

            if (!addons.TryGetValue(payload.Product ?? string.Empty, out var addon))
            {
                result.Errors.Add($"Row {i + 2}: no addon with product code '{payload.Product}'. Create the addon first.");
                continue;
            }

            if (!existingIds.Add(payload.Id))
            {
                result.Skipped++;
                continue;
            }

            int? days = payload.Expires.HasValue ? (int)((payload.Expires.Value - payload.Issued) / 86400) : null;
            var keyType = GetString(row, keyTypeColumn);

            _db.Split3DLicenses().Add(new Split3DLicense
            {
                LicenseId = payload.Id,
                AddonId = addon.Id,
                ProductCode = addon.ProductCode,
                Email = payload.Email,
                CustomerName = GetString(row, nameColumn).NullEmpty() ?? payload.Customer,
                Phone = GetString(row, phoneColumn).Truncate(100),
                KeyType = Split3DPlans.All.Contains(keyType) ? keyType : Split3DPlans.FromDays(days),
                Days = days,
                Price = ParsePrice(GetString(row, priceColumn)),
                PurchaseDateUtc = ParseDate(GetString(row, purchaseColumn)),
                IssuedOnUtc = DateTimeOffset.FromUnixTimeSeconds(payload.Issued).UtcDateTime,
                ExpiresOnUtc = payload.Expires.HasValue ? DateTimeOffset.FromUnixTimeSeconds(payload.Expires.Value).UtcDateTime : null,
                Token = token.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", ""),
                Notes = GetString(row, notesColumn)
            });

            result.Imported++;
        }

        await _db.SaveChangesAsync(cancelToken);

        return result;
    }

    /// <summary>
    /// Maps catalog product ids to the addon and plan they grant. Only active addons are included.
    /// </summary>
    public async Task<Dictionary<int, Split3DProductPlan>> GetProductPlansAsync(CancellationToken cancelToken = default)
    {
        var rows = await (
            from m in _db.Split3DAddonProducts().AsNoTracking()
            join a in _db.Split3DAddons().AsNoTracking() on m.AddonId equals a.Id
            where a.Active
            select new { m, a })
            .ToListAsync(cancelToken);

        var result = new Dictionary<int, Split3DProductPlan>();
        foreach (var row in rows)
        {
            // One addon per product for now; the first mapping wins.
            result.TryAdd(row.m.ProductId, new Split3DProductPlan(row.a, row.m.KeyType, row.m.Days, row.m.MaxDevices));
        }

        return result;
    }

    public static string FormatExpiry(DateTime? expiresOnUtc)
        => expiresOnUtc.HasValue ? expiresOnUtc.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC" : Split3DPlans.Lifetime;

    public static string FormatPrice(decimal price)
        => price.ToString("#,##0", _vnCulture.NumberFormat).Replace('.', ',') + " VND";

    private static string ReplacePlaceholders(string template, Split3DLicense license, string addonName, bool html)
    {
        string Encode(string value) => html ? System.Net.WebUtility.HtmlEncode(value) : value;

        var text = Encode(template)
            .Replace("{CustomerName}", Encode(license.CustomerName))
            .Replace("{Email}", Encode(license.Email))
            .Replace("{Addon}", Encode(addonName ?? license.ProductCode))
            .Replace("{Plan}", Encode(license.KeyType))
            .Replace("{ExpiresOn}", Encode(FormatExpiry(license.ExpiresOnUtc)))
            .Replace("{Price}", Encode(FormatPrice(license.Price)))
            .Replace("{Key}", html
                ? $"<code style=\"display:block;word-break:break-all;padding:8px;background:#f1f6fa\">{Encode(license.Token)}</code>"
                : license.Token);

        return html ? text.Replace("\r\n", "\n").Replace("\n", "<br>") : text.Replace("\r", "").Replace("\n", " ");
    }

    private static string CleanEmail(string email)
    {
        email = email?.Trim().ToLowerInvariant();
        if (email.IsEmpty() || email.Length > 254 || !email.IsEmail())
        {
            throw new ArgumentException("Please enter a valid email address.", nameof(email));
        }

        return email;
    }

    private static string FindColumn(IDataTable table, params string[] names)
    {
        return table.Columns
            .Select(x => x.Name)
            .FirstOrDefault(x => names.Any(n => n.Equals(x?.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private static string GetString(IDataRow row, string column)
        => column == null ? null : Convert.ToString(row[column], CultureInfo.InvariantCulture)?.Trim().NullEmpty();

    private static decimal ParsePrice(string value)
    {
        if (value.IsEmpty())
        {
            return 0;
        }

        value = value.Replace(",", "").Replace("VND", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price >= 0
            ? decimal.Round(price, 0)
            : 0;
    }

    private static DateTime? ParseDate(string value)
    {
        if (value.IsEmpty())
        {
            return null;
        }

        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd/MM/yyyy HH:mm 'UTC'", "yyyy-MM-dd HH:mm:ss"];
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
        {
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        }

        // Excel cells are read as DateTime or OLE automation numbers.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var oa) && oa > 0 && oa < 2958466)
        {
            return DateTime.SpecifyKind(DateTime.FromOADate(oa).Date, DateTimeKind.Utc);
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date)
            ? DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)
            : null;
    }
}
