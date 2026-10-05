#nullable enable

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Seo;

namespace Smartstore.Split3D.Services;

/// <summary>
/// A bigger package the customer can upgrade a key to.
/// </summary>
public sealed class Split3DUpgradeOption
{
    public required Product Product { get; init; }
    public required string KeyType { get; init; }

    /// <summary>
    /// Validity in days from the key's issue date. <c>null</c> means lifetime.
    /// </summary>
    public int? Days { get; init; }

    public int MaxDevices { get; init; }

    /// <summary>
    /// Expiry after the upgrade. <c>null</c> means lifetime.
    /// </summary>
    public DateTime? ExpiresOnUtc { get; init; }

    /// <summary>
    /// Current price of the package.
    /// </summary>
    public decimal FullPrice { get; init; }

    /// <summary>
    /// What the customer pays: package price minus what the key cost.
    /// </summary>
    public decimal Price { get; init; }
}

/// <summary>
/// What an admin upgrades a key to.
/// </summary>
public sealed class Split3DUpgradeSpec
{
    public string KeyType { get; set; } = Split3DPlans.Lifetime;

    /// <summary>
    /// Only used for <see cref="Split3DPlans.Custom"/>.
    /// </summary>
    public int? Days { get; set; }

    /// <summary>
    /// Explicit new expiry (overrides the plan). <c>null</c>: computed from the key's issue date and the plan.
    /// </summary>
    public DateTime? ExpiresOnUtc { get; set; }

    /// <summary>
    /// New device limit. <c>null</c> keeps the current one.
    /// </summary>
    public int? MaxDevices { get; set; }

    /// <summary>
    /// Amount charged in VND, added to the key's price.
    /// </summary>
    public decimal Price { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Upgrades keys to a bigger plan without changing the key id: the device activations stay, the key is
/// re-signed with the new expiry and the activation server hands the new key to the addon at its next
/// online check, so nothing has to be reinstalled or entered again.
/// Customers buy an upgrade through the cart (one hidden "upgrade" product priced by
/// <see cref="Split3DUpgradePriceCalculator"/>); admins upgrade directly.
/// </summary>
public class Split3DUpgradeService
{
    public const string UpgradeProductSku = "SPLIT3D-UPGRADE";
    public const string UpgradeProductName = "Nâng cấp gói key";

    private readonly SmartDbContext _db;
    private readonly Split3DLicenseService _licenseService;
    private readonly Split3DDeviceService _deviceService;
    private readonly IShoppingCartService _cartService;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IUrlService _urlService;
    private readonly Split3DSettings _settings;

    public Split3DUpgradeService(
        SmartDbContext db,
        Split3DLicenseService licenseService,
        Split3DDeviceService deviceService,
        IShoppingCartService cartService,
        IPriceCalculationService priceCalculationService,
        IUrlService urlService,
        Split3DSettings settings)
    {
        _db = db;
        _licenseService = licenseService;
        _deviceService = deviceService;
        _cartService = cartService;
        _priceCalculationService = priceCalculationService;
        _urlService = urlService;
        _settings = settings;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Expiry of a key issued at <paramref name="issuedOnUtc"/> with a plan of <paramref name="days"/> days.
    /// </summary>
    public static DateTime? GetExpiry(DateTime issuedOnUtc, int? days)
        => days.HasValue ? DateTime.SpecifyKind(issuedOnUtc, DateTimeKind.Utc).AddDays(days.Value) : null;

    /// <summary>
    /// Days of a plan, <paramref name="customDays"/> for <see cref="Split3DPlans.Custom"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Unknown plan or invalid custom days.</exception>
    public static int? GetPlanDays(string keyType, int? customDays)
    {
        if (keyType == Split3DPlans.Custom)
        {
            if (customDays is null or < 1 or > Split3DPlans.MaxDays)
            {
                throw new ArgumentException($"Days must be between 1 and {Split3DPlans.MaxDays}.", nameof(customDays));
            }

            return customDays;
        }

        if (!Split3DPlans.All.Contains(keyType))
        {
            throw new ArgumentException($"Unknown plan '{keyType}'.", nameof(keyType));
        }

        return Split3DPlans.GetFixedDays(keyType);
    }

    /// <summary>
    /// Whether the customer may buy an upgrade for the key at all (not for blocked or expired keys).
    /// </summary>
    public static bool CanBuyUpgrade(Split3DLicense license)
        => !license.Blocked && (!license.ExpiresOnUtc.HasValue || license.ExpiresOnUtc.Value > DateTime.UtcNow);

    /// <summary>
    /// Packages of the key's addon that are bigger than the key: never shorter, never fewer devices, and bigger
    /// in at least one of them. The price is the package price minus what the key cost; free upgrades are not offered.
    /// </summary>
    public async Task<List<Split3DUpgradeOption>> GetOptionsAsync(Split3DLicense license, CancellationToken cancelToken = default)
    {
        Guard.NotNull(license);

        if (!CanBuyUpgrade(license))
        {
            return [];
        }

        var mappings = await _db.Split3DAddonProducts().AsNoTracking()
            .Where(x => x.AddonId == license.AddonId)
            .ToListAsync(cancelToken);
        if (mappings.Count == 0)
        {
            return [];
        }

        var productIds = mappings.Select(x => x.ProductId).Distinct().ToArray();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.Published && !x.Deleted)
            .ToDictionaryAsync(x => x.Id, cancelToken);

        var now = DateTime.UtcNow;
        var currentDevices = _deviceService.GetMaxDevices(license);
        var priceOptions = _priceCalculationService.CreateDefaultOptions(false);
        var result = new List<Split3DUpgradeOption>();

        foreach (var mapping in mappings)
        {
            if (!products.TryGetValue(mapping.ProductId, out var product))
            {
                continue;
            }

            int? days;
            try
            {
                days = GetPlanDays(mapping.KeyType, mapping.Days);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var expires = GetExpiry(license.IssuedOnUtc, days);
            var devices = Math.Max(1, mapping.MaxDevices ?? _settings.DefaultMaxDevices);

            var longer = IsLonger(expires, license.ExpiresOnUtc);
            var sameLength = expires == license.ExpiresOnUtc;
            if (devices < currentDevices || !(longer || (sameLength && devices > currentDevices)))
            {
                continue;
            }

            if (expires.HasValue && expires.Value <= now)
            {
                continue;
            }

            var fullPrice = (await _priceCalculationService.CalculatePriceAsync(new PriceCalculationContext(product, priceOptions))).FinalPrice.Amount;
            var price = Math.Max(0, decimal.Round(fullPrice - license.Price, 0));
            if (price <= 0)
            {
                continue;
            }

            result.Add(new Split3DUpgradeOption
            {
                Product = product,
                KeyType = mapping.KeyType,
                Days = days,
                MaxDevices = devices,
                ExpiresOnUtc = expires,
                FullPrice = fullPrice,
                Price = price
            });
        }

        return result
            .OrderBy(x => x.MaxDevices)
            .ThenBy(x => x.ExpiresOnUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.Price)
            .ToList();
    }

    /// <summary>
    /// Puts the upgrade of <paramref name="license"/> to <paramref name="productId"/> into the customer's cart.
    /// Any upgrade still in the cart is replaced: one upgrade per order.
    /// </summary>
    /// <returns>Warnings; empty on success.</returns>
    public async Task<IList<string>> AddToCartAsync(Customer customer, int storeId, Split3DLicense license, int productId, CancellationToken cancelToken = default)
    {
        Guard.NotNull(customer);
        Guard.NotNull(license);

        var option = (await GetOptionsAsync(license, cancelToken)).FirstOrDefault(x => x.Product.Id == productId);
        if (option == null)
        {
            return ["Gói nâng cấp này không còn khả dụng cho key."];
        }

        var product = await EnsureUpgradeProductAsync(cancelToken);

        var pending = await _db.Split3DLicenseUpgrades()
            .Where(x => x.CustomerId == customer.Id && x.StatusId == (int)Split3DUpgradeStatus.Pending)
            .ToListAsync(cancelToken);
        pending.Each(x => x.Status = Split3DUpgradeStatus.Cancelled);

        _db.Split3DLicenseUpgrades().Add(new Split3DLicenseUpgrade
        {
            Split3DLicenseId = license.Id,
            Status = Split3DUpgradeStatus.Pending,
            CustomerId = customer.Id,
            TargetProductId = option.Product.Id,
            FromKeyType = license.KeyType,
            ToKeyType = option.KeyType,
            ToDays = option.Days,
            FromExpiresOnUtc = license.ExpiresOnUtc,
            ToExpiresOnUtc = option.ExpiresOnUtc,
            FromMaxDevices = _deviceService.GetMaxDevices(license),
            ToMaxDevices = option.MaxDevices,
            Price = option.Price,
            CreatedOnUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancelToken);

        // The upgrade line is priced from the pending row, so an old line in the cart is simply reused.
        var cart = await _cartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);
        if (cart.Items.Any(x => x.Item.ProductId == product.Id))
        {
            return [];
        }

        var ctx = new AddToCartContext
        {
            Customer = customer,
            Product = product,
            CartType = ShoppingCartType.ShoppingCart,
            StoreId = storeId,
            Quantity = 1
        };

        return await _cartService.AddToCartAsync(ctx) ? [] : ctx.Warnings;
    }

    /// <summary>
    /// Links the customer's pending upgrade to the order that contains the upgrade product (on order placement).
    /// </summary>
    public async Task<Split3DLicenseUpgrade?> AttachToOrderAsync(Order order, CancellationToken cancelToken = default)
    {
        Guard.NotNull(order);

        var productId = await GetUpgradeProductIdAsync(cancelToken);
        if (productId == 0 || !await _db.OrderItems.AnyAsync(x => x.OrderId == order.Id && x.ProductId == productId, cancelToken))
        {
            return null;
        }

        var upgrade = await _db.Split3DLicenseUpgrades()
            .Where(x => x.CustomerId == order.CustomerId && x.StatusId == (int)Split3DUpgradeStatus.Pending)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancelToken);

        if (upgrade == null)
        {
            _db.OrderNotes.Add(order, "Split3D: the order contains a key upgrade, but no pending upgrade was found. Upgrade the key manually.");
            await _db.SaveChangesAsync(cancelToken);
            return null;
        }

        upgrade.OrderId = order.Id;
        upgrade.Status = Split3DUpgradeStatus.Ordered;

        var license = await _db.Split3DLicenses().FindByIdAsync(upgrade.Split3DLicenseId, false, cancelToken);
        _db.OrderNotes.Add(order, "Nâng cấp key: " + Describe(upgrade, license), displayToCustomer: true);

        await _db.SaveChangesAsync(cancelToken);

        return upgrade;
    }

    /// <summary>
    /// Applies the paid upgrades of <paramref name="order"/>. Idempotent: applied upgrades are skipped.
    /// </summary>
    /// <returns>The upgraded licenses.</returns>
    public async Task<List<Split3DLicense>> ApplyForOrderAsync(Order order, CancellationToken cancelToken = default)
    {
        Guard.NotNull(order);

        var upgrades = await _db.Split3DLicenseUpgrades()
            .Where(x => x.OrderId == order.Id && x.StatusId == (int)Split3DUpgradeStatus.Ordered)
            .ToListAsync(cancelToken);

        var result = new List<Split3DLicense>();
        foreach (var upgrade in upgrades)
        {
            var license = await _db.Split3DLicenses().FindByIdAsync(upgrade.Split3DLicenseId, true, cancelToken);
            if (license == null)
            {
                _db.OrderNotes.Add(order, $"Split3D: the key of upgrade {upgrade.Id} no longer exists.");
                upgrade.Status = Split3DUpgradeStatus.Cancelled;
                continue;
            }

            // Expiry is recomputed from the plan: the key may have changed since the upgrade was bought.
            var expires = GetExpiry(license.IssuedOnUtc, upgrade.ToDays);
            Apply(license, upgrade, upgrade.ToKeyType, upgrade.ToDays, expires, upgrade.ToMaxDevices, $"Order #{order.GetOrderNumber()}");
            result.Add(license);

            _db.OrderNotes.Add(order,
                $"Key đã được nâng cấp: {Describe(upgrade, license)}\n\nKey mới (addon trong Blender tự nhận ở lần kiểm tra online kế tiếp, không cần nhập lại):\n{license.Token}",
                displayToCustomer: true);
        }

        if (upgrades.Count > 0)
        {
            await _db.SaveChangesAsync(cancelToken);
            await SendEmailsAsync(result, order, cancelToken);
        }

        return result;
    }

    /// <summary>
    /// Upgrades a key directly (admin). Commits.
    /// </summary>
    /// <exception cref="ArgumentException">The spec is invalid.</exception>
    /// <exception cref="Split3DKeyException">Keys are missing or invalid.</exception>
    public async Task<Split3DLicenseUpgrade> UpgradeByAdminAsync(Split3DLicense license, Split3DUpgradeSpec spec, CancellationToken cancelToken = default)
    {
        Guard.NotNull(license);
        Guard.NotNull(spec);

        var keyType = spec.KeyType.NullEmpty() ?? Split3DPlans.Lifetime;
        var days = GetPlanDays(keyType, spec.Days);
        var expires = keyType == Split3DPlans.Lifetime
            ? null
            : spec.ExpiresOnUtc.HasValue ? DateTime.SpecifyKind(spec.ExpiresOnUtc.Value, DateTimeKind.Utc) : GetExpiry(license.IssuedOnUtc, days);

        if (expires.HasValue && expires.Value <= license.IssuedOnUtc)
        {
            throw new ArgumentException("The new expiry must be after the issue date of the key.", nameof(spec));
        }

        if (spec.MaxDevices is < 1 or > 1000)
        {
            throw new ArgumentException("Devices must be between 1 and 1000.", nameof(spec));
        }

        if (spec.Price < 0 || spec.Price != decimal.Truncate(spec.Price))
        {
            throw new ArgumentException("Price must be a non-negative integer VND amount.", nameof(spec));
        }

        if (expires.HasValue)
        {
            // An explicit date: store the matching number of days, so "Days" stays consistent with the expiry.
            days = (int)Math.Ceiling((expires.Value - DateTime.SpecifyKind(license.IssuedOnUtc, DateTimeKind.Utc)).TotalDays);
            if (keyType != Split3DPlans.Custom && Split3DPlans.FromDays(days) != keyType)
            {
                keyType = Split3DPlans.FromDays(days);
            }
        }

        var upgrade = new Split3DLicenseUpgrade
        {
            Split3DLicenseId = license.Id,
            Status = Split3DUpgradeStatus.Pending,
            ByAdmin = true,
            FromKeyType = license.KeyType,
            ToKeyType = keyType,
            ToDays = days,
            FromExpiresOnUtc = license.ExpiresOnUtc,
            ToExpiresOnUtc = expires,
            FromMaxDevices = _deviceService.GetMaxDevices(license),
            ToMaxDevices = spec.MaxDevices,
            Price = spec.Price,
            Notes = spec.Notes?.Trim().NullEmpty(),
            CreatedOnUtc = DateTime.UtcNow
        };

        _db.Split3DLicenseUpgrades().Add(upgrade);
        Apply(license, upgrade, keyType, days, expires, spec.MaxDevices, "Admin" + (upgrade.Notes != null ? ": " + upgrade.Notes : null));

        await _db.SaveChangesAsync(cancelToken);

        return upgrade;
    }

    /// <summary>
    /// Upgrades of one key, newest first.
    /// </summary>
    public Task<List<Split3DLicenseUpgrade>> GetHistoryAsync(int licenseId, CancellationToken cancelToken = default)
    {
        return _db.Split3DLicenseUpgrades().AsNoTracking()
            .Where(x => x.Split3DLicenseId == licenseId && x.StatusId == (int)Split3DUpgradeStatus.Applied)
            .OrderByDescending(x => x.AppliedOnUtc)
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// Id of the hidden upgrade product, 0 if it was not created yet.
    /// </summary>
    public Task<int> GetUpgradeProductIdAsync(CancellationToken cancelToken = default)
    {
        return _db.Products
            .Where(x => x.Sku == UpgradeProductSku && !x.Deleted)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancelToken);
    }

    /// <summary>
    /// "Split3D Print · 6 tháng (2 máy) → Vĩnh viễn (3 máy)".
    /// </summary>
    public static string Describe(Split3DLicenseUpgrade upgrade, Split3DLicense? license)
    {
        static string Devices(int? count) => count.HasValue ? $" ({count} máy)" : string.Empty;

        var to = upgrade.ToMaxDevices.HasValue && upgrade.ToMaxDevices != upgrade.FromMaxDevices
            ? upgrade.ToKeyType + Devices(upgrade.ToMaxDevices)
            : upgrade.ToKeyType;
        var from = upgrade.ToMaxDevices.HasValue && upgrade.ToMaxDevices != upgrade.FromMaxDevices
            ? upgrade.FromKeyType + Devices(upgrade.FromMaxDevices)
            : upgrade.FromKeyType;
        var key = license != null ? $"{license.ProductCode} {license.LicenseId[..Math.Min(8, license.LicenseId.Length)]}… · " : string.Empty;

        return $"{key}{from} → {to}";
    }

    /// <summary>
    /// Re-signs the key (same id, customer, email and issue date; new expiry) and records the upgrade (without committing).
    /// </summary>
    private void Apply(Split3DLicense license, Split3DLicenseUpgrade upgrade, string keyType, int? days, DateTime? expiresOnUtc, int? maxDevices, string note)
    {
        var signer = _licenseService.CreateSigner(true);
        var payload = signer.Verify(license.Token);

        long? expires = expiresOnUtc.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(expiresOnUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds()
            : null;

        license.Token = signer.Sign(new Split3DKeyPayload
        {
            Product = payload.Product,
            Customer = payload.Customer,
            Email = payload.Email,
            Issued = payload.Issued,
            Expires = expires,
            Id = payload.Id
        });

        license.KeyType = keyType;
        license.Days = days;
        license.ExpiresOnUtc = expires.HasValue ? DateTimeOffset.FromUnixTimeSeconds(expires.Value).UtcDateTime : null;
        license.Price += upgrade.Price;

        // Purchases only ever raise the device limit; admins may also lower it.
        if (maxDevices is > 0 && (upgrade.ByAdmin || maxDevices.Value > _deviceService.GetMaxDevices(license)))
        {
            license.MaxDevices = maxDevices;
        }

        var line = $"{DateTime.UtcNow:dd/MM/yyyy} upgrade {upgrade.FromKeyType} → {keyType}"
            + (upgrade.Price > 0 ? $", {Split3DLicenseService.FormatPrice(upgrade.Price)}" : null)
            + $" ({note})";
        license.Notes = license.Notes.HasValue() ? license.Notes + "\n" + line : line;

        upgrade.ToKeyType = keyType;
        upgrade.ToDays = days;
        upgrade.ToExpiresOnUtc = license.ExpiresOnUtc;
        upgrade.ToMaxDevices = _deviceService.GetMaxDevices(license);
        upgrade.Status = Split3DUpgradeStatus.Applied;
        upgrade.AppliedOnUtc = DateTime.UtcNow;
    }

    private async Task SendEmailsAsync(List<Split3DLicense> licenses, Order order, CancellationToken cancelToken)
    {
        if (!_settings.SendEmail || licenses.Count == 0)
        {
            return;
        }

        foreach (var license in licenses)
        {
            try
            {
                if (!await _licenseService.QueueEmailAsync(license, cancelToken))
                {
                    _db.OrderNotes.Add(order, $"Split3D: upgraded key email to {license.Email} was not sent (message template inactive or no email account).");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Split3D: upgraded key email for license {license.Id} failed.");
                _db.OrderNotes.Add(order, $"Split3D: upgraded key email to {license.Email} failed: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync(cancelToken);
    }

    /// <summary>
    /// The hidden catalog product that carries an upgrade through cart and checkout. Its price comes from the
    /// customer's pending upgrade (see <see cref="Split3DUpgradePriceCalculator"/>).
    /// </summary>
    private async Task<Product> EnsureUpgradeProductAsync(CancellationToken cancelToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Sku == UpgradeProductSku && !x.Deleted, cancelToken);
        if (product == null)
        {
            var template = await _db.ProductTemplates.FirstOrDefaultAsync(x => x.ViewPath == "Product", cancelToken)
                ?? await _db.ProductTemplates.FirstAsync(cancelToken);

            product = new Product
            {
                ProductType = ProductType.SimpleProduct,
                Sku = UpgradeProductSku,
                Name = UpgradeProductName,
                ShortDescription = "Nâng cấp key đang dùng lên gói lớn hơn. Key giữ nguyên trên các máy đã kích hoạt.",
                ProductTemplateId = template.Id,
                Price = 0,
                AllowCustomerReviews = false
            };

            _db.Products.Add(product);
        }

        product.Published = true;
        product.Visibility = ProductVisibility.Hidden;
        product.ManageInventoryMethod = ManageInventoryMethod.DontManageStock;
        product.OrderMinimumQuantity = 1;
        product.OrderMaximumQuantity = 1;
        product.QuantityStep = 1;
        product.IsShippingEnabled = false;
        product.IsFreeShipping = true;
        product.IsTaxExempt = true;
        product.IsEsd = true;
        product.IsDownload = false;
        product.ShowOnHomePage = false;

        await _db.SaveChangesAsync(cancelToken);

        if ((await _urlService.GetActiveSlugAsync(product.Id, nameof(Product), 0)).IsEmpty())
        {
            await _urlService.SaveSlugAsync(product, "nang-cap-key", product.Name, true);
        }

        return product;
    }

    private static bool IsLonger(DateTime? candidate, DateTime? current)
    {
        if (!current.HasValue)
        {
            return false;
        }

        return !candidate.HasValue || candidate.Value > current.Value;
    }
}
