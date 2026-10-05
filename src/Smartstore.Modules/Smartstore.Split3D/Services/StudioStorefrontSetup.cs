using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core;
using Smartstore.Core.Catalog;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Checkout.Shipping;
using Smartstore.Core.Common.Configuration;
using Smartstore.Core.Content.Menus;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Seo;
using Smartstore.Core.Theming;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Turns the Split3D addon shop into the TT Minimal studio storefront: studio theme, main menu entry
/// for the printing service, trimmed registration and address forms, shipping for physical products.
/// Runs once per <see cref="CurrentVersion"/>, see <see cref="StudioSettings.LayoutVersion"/>.
/// </summary>
public class StudioStorefrontSetup
{
    public const int CurrentVersion = 4;
    public const string ThemeName = "TTMinimal";
    public const string PrintServiceRouteName = "TTPrintService";
    public const string DesignServiceRouteName = "TTDesignService";
    public const string ToolsRouteName = "TTTools";

    // Admin menu items the studio needs: product attributes for variants (color, size) and shipping methods.
    private static readonly string[] _requiredAdminMenuItems =
        ["attributes-header", "attributes", "shipping-header", "shipping-methods", "shipping-providers"];

    private readonly SmartDbContext _db;
    private readonly ICommonServices _services;
    private readonly IThemeRegistry _themeRegistry;
    private readonly StudioSettings _studioSettings;
    private readonly Split3DSettings _split3DSettings;
    private readonly IUrlService _urlService;

    public StudioStorefrontSetup(
        SmartDbContext db,
        ICommonServices services,
        IThemeRegistry themeRegistry,
        StudioSettings studioSettings,
        Split3DSettings split3DSettings,
        IUrlService urlService)
    {
        _db = db;
        _services = services;
        _themeRegistry = themeRegistry;
        _studioSettings = studioSettings;
        _split3DSettings = split3DSettings;
        _urlService = urlService;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    public bool IsUpToDate => _studioSettings.LayoutVersion >= CurrentVersion;

    /// <summary>
    /// Applies the studio layout if it has not been applied in its current version yet.
    /// </summary>
    /// <param name="force">Apply again even if the current version was applied already.</param>
    /// <returns><c>true</c> if the layout was applied.</returns>
    public async Task<bool> ApplyAsync(bool force = false, CancellationToken cancelToken = default)
    {
        if (IsUpToDate && !force)
        {
            return false;
        }

        var themeApplied = await ApplyThemeAndSeoAsync(cancelToken);
        await ApplyFormsAsync();
        await ApplyCatalogAsync();
        await ApplyToolsCategoryAsync(cancelToken);
        await StudioCustomProducts.ApplyAsync(_db, _urlService, cancelToken);
        await ApplyMainMenuAsync(cancelToken);
        await ApplyShippingAsync(cancelToken);
        await ApplyCheckoutAsync(cancelToken);
        await ApplyAdminMenuAsync();

        // Without the theme files the layout is incomplete: leave the version unset so the next start tries again.
        if (themeApplied)
        {
            _studioSettings.LayoutVersion = CurrentVersion;
            await _services.SettingFactory.SaveSettingsAsync(_studioSettings);
        }

        await _services.Cache.ClearAsync();

        Logger.Info($"TT Minimal storefront layout {CurrentVersion} applied{(themeApplied ? string.Empty : " without theme")}.");

        return true;
    }

    private async Task<bool> ApplyThemeAndSeoAsync(CancellationToken cancelToken)
    {
        var brand = _studioSettings.BrandName.NullEmpty() ?? "TT Minimal";
        var hasTheme = _themeRegistry.ContainsTheme(ThemeName);

        if (hasTheme)
        {
            // Theme settings are often stored per store (theme configuration in the admin area saves them that way).
            var storeIds = await _db.Settings
                .Where(x => x.Name == "ThemeSettings.DefaultTheme" && x.StoreId > 0)
                .Select(x => x.StoreId)
                .Distinct()
                .ToListAsync(cancelToken);

            foreach (var storeId in storeIds.Prepend(0))
            {
                var themeSettings = await _services.SettingFactory.LoadSettingsAsync<ThemeSettings>(storeId);
                themeSettings.DefaultTheme = ThemeName;
                themeSettings.AllowCustomerToSelectTheme = false;
                await _services.SettingFactory.SaveSettingsAsync(themeSettings, storeId);
            }
        }
        else
        {
            Logger.Warn($"Theme '{ThemeName}' not found. Copy Themes/{ThemeName} to the web application and apply the layout again.");
        }

        foreach (var store in await _db.Stores.ToListAsync(cancelToken))
        {
            if (store.Name.IsEmpty() || store.Name == "Your store name" || store.Name == Split3DStorefrontContent.StoreName)
            {
                store.Name = brand;
            }
        }

        await _db.SaveChangesAsync(cancelToken);

        var seoSettings = await _services.SettingFactory.LoadSettingsAsync<SeoSettings>();
        seoSettings.MetaTitle = brand;
        seoSettings.PageTitleSeparator = " | ";
        seoSettings.PageTitleSeoAdjustment = PageTitleSeoAdjustment.StorenameAfterPagename;
        await _services.SettingFactory.SaveSettingsAsync(seoSettings);

        var homePageSettings = await _services.SettingFactory.LoadSettingsAsync<HomePageSettings>();
        homePageSettings.MetaTitle = "In 3D theo yêu cầu, sản phẩm in 3D & addon Blender";
        homePageSettings.MetaDescription = $"{brand} – studio in 3D tại Hà Nội: in FDM và Resin theo yêu cầu, sản phẩm in 3D tự thiết kế, addon Blender Split3D Print cho người làm 3D.";
        homePageSettings.MetaKeywords = "in 3d, in 3d hà nội, in resin, in fdm, dịch vụ in 3d, thiết kế 3d, addon blender, split3d";
        await _services.SettingFactory.SaveSettingsAsync(homePageSettings);

        var socialSettings = await _services.SettingFactory.LoadSettingsAsync<SocialSettings>();
        socialSettings.FacebookLink = _studioSettings.FacebookUrl.NullEmpty() ?? string.Empty;
        socialSettings.TwitterLink = RemovePlaceholder(socialSettings.TwitterLink);
        socialSettings.YoutubeLink = RemovePlaceholder(socialSettings.YoutubeLink);
        socialSettings.InstagramLink = RemovePlaceholder(socialSettings.InstagramLink);
        socialSettings.TikTokLink = RemovePlaceholder(socialSettings.TikTokLink);
        await _services.SettingFactory.SaveSettingsAsync(socialSettings);

        return hasTheme;

        static string RemovePlaceholder(string link) => link == "#" ? string.Empty : link;
    }

    private async Task ApplyFormsAsync()
    {
        // Registration: name, email, password and an optional phone number. No company, address, newsletter or username.
        var customerSettings = await _services.SettingFactory.LoadSettingsAsync<CustomerSettings>();
        customerSettings.CustomerLoginType = CustomerLoginType.Email;
        customerSettings.CheckUsernameAvailabilityEnabled = false;
        customerSettings.GenderEnabled = false;
        customerSettings.TitleEnabled = false;
        customerSettings.DateOfBirthEnabled = false;
        customerSettings.CompanyEnabled = false;
        customerSettings.CompanyRequired = false;
        customerSettings.StreetAddressEnabled = false;
        customerSettings.StreetAddressRequired = false;
        customerSettings.StreetAddress2Enabled = false;
        customerSettings.ZipPostalCodeEnabled = false;
        customerSettings.CityEnabled = false;
        customerSettings.CountryEnabled = false;
        customerSettings.StateProvinceEnabled = false;
        customerSettings.PhoneEnabled = true;
        customerSettings.PhoneRequired = false;
        customerSettings.FaxEnabled = false;
        customerSettings.NewsletterEnabled = false;
        customerSettings.HideNewsletterBlock = true;
        await _services.SettingFactory.SaveSettingsAsync(customerSettings);

        // Checkout addresses: enough to ship printed products within Viet Nam.
        var addressSettings = await _services.SettingFactory.LoadSettingsAsync<AddressSettings>();
        addressSettings.CompanyEnabled = false;
        addressSettings.CompanyRequired = false;
        addressSettings.StreetAddressEnabled = true;
        addressSettings.StreetAddressRequired = true;
        addressSettings.StreetAddress2Enabled = false;
        addressSettings.ZipPostalCodeEnabled = false;
        addressSettings.ZipPostalCodeRequired = false;
        addressSettings.CityEnabled = true;
        addressSettings.CityRequired = true;
        addressSettings.CountryEnabled = true;
        addressSettings.CountryRequired = true;
        addressSettings.StateProvinceEnabled = false;
        addressSettings.StateProvinceRequired = false;
        addressSettings.PhoneEnabled = true;
        addressSettings.PhoneRequired = true;
        addressSettings.FaxEnabled = false;
        addressSettings.FaxRequired = false;
        await _services.SettingFactory.SaveSettingsAsync(addressSettings);
    }

    private async Task ApplyCatalogAsync()
    {
        var catalogSettings = await _services.SettingFactory.LoadSettingsAsync<CatalogSettings>();
        catalogSettings.CompareProductsEnabled = false;
        catalogSettings.RecentlyAddedProductsEnabled = false;
        catalogSettings.EmailAFriendEnabled = false;
        catalogSettings.ShowManufacturerInGridStyleLists = false;
        catalogSettings.ShowManufacturersOnHomepage = false;
        catalogSettings.ShowBestsellersOnHomepage = false;
        catalogSettings.ShowPopularProductTagsOnHomepage = false;
        await _services.SettingFactory.SaveSettingsAsync(catalogSettings);
    }

    /// <summary>
    /// Renames the addon category "Addon Blender" to "Công cụ 3D" (version 3). The URL slug stays the same.
    /// </summary>
    private async Task ApplyToolsCategoryAsync(CancellationToken cancelToken)
    {
        var categories = await _db.Categories
            .Where(x => Split3DStorefrontContent.CategoryNames.Contains(x.Name) && x.Name != Split3DStorefrontContent.CategoryName && !x.Deleted)
            .ToListAsync(cancelToken);

        foreach (var category in categories)
        {
            category.Name = Split3DStorefrontContent.CategoryName;
        }

        await _db.SaveChangesAsync(cancelToken);
    }

    private async Task ApplyMainMenuAsync(CancellationToken cancelToken)
    {
        var menu = await _db.Menus
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.SystemName == "Main", cancelToken);

        if (menu == null)
        {
            return;
        }

        var model = $"{{\"routename\":\"{PrintServiceRouteName}\"}}";
        if (menu.Items.Any(x => x.ProviderName == "route" && x.Model == model))
        {
            return;
        }

        var minOrder = menu.Items.Where(x => x.ParentItemId == 0).Select(x => x.DisplayOrder).DefaultIfEmpty(0).Min();

        menu.Items.Add(new MenuItemEntity
        {
            ProviderName = "route",
            Model = model,
            Title = "In 3D theo yêu cầu",
            Icon = "cube",
            Published = true,
            DisplayOrder = minOrder - 1
        });

        await _db.SaveChangesAsync(cancelToken);
    }

    private async Task ApplyShippingAsync(CancellationToken cancelToken)
    {
        var methods = await _db.ShippingMethods.ToListAsync(cancelToken);

        // Only the install defaults are touched; methods the shop owner created or renamed stay as they are.
        var pickup = methods.FirstOrDefault(x => x.Name == "In-Store Pickup");
        if (pickup != null)
        {
            pickup.Name = "Nhận tại xưởng";
            pickup.Description = _studioSettings.Address;
            pickup.DisplayOrder = 1;
        }

        var ground = methods.FirstOrDefault(x => x.Name == "By Ground");
        if (ground != null)
        {
            ground.Name = "Giao hàng tận nơi";
            ground.Description = "Giao qua đơn vị vận chuyển, thường 2–4 ngày.";
            ground.DisplayOrder = 0;
        }

        var free = methods.FirstOrDefault(x => x.Name == "Free shipping" && x.IgnoreCharges);
        if (free != null)
        {
            _db.ShippingMethods.Remove(free);
        }

        await _db.SaveChangesAsync(cancelToken);

        if (ground != null)
        {
            var key = $"ShippingRateComputationMethod.FixedRate.Rate.ShippingMethodId{ground.Id}";
            if (await _services.Settings.GetSettingByKeyAsync<decimal>(key) == 0)
            {
                await _services.Settings.ApplySettingAsync(key, 30000m);
            }
        }

        await _db.SaveChangesAsync(cancelToken);
    }

    /// <summary>
    /// Skips the payment page when bank transfer is the only payment method (version 4), so checkout is
    /// cart > confirm and the bank details appear once, on the completed page.
    /// </summary>
    private async Task ApplyCheckoutAsync(CancellationToken cancelToken)
    {
        var storeIds = await _db.Settings
            .Where(x => x.Name == "PaymentSettings.SkipPaymentSelectionIfSingleOption" && x.StoreId > 0)
            .Select(x => x.StoreId)
            .Distinct()
            .ToListAsync(cancelToken);

        foreach (var storeId in storeIds.Prepend(0))
        {
            var paymentSettings = await _services.SettingFactory.LoadSettingsAsync<PaymentSettings>(storeId);
            paymentSettings.SkipPaymentSelectionIfSingleOption = true;
            await _services.SettingFactory.SaveSettingsAsync(paymentSettings, storeId);
        }
    }

    private async Task ApplyAdminMenuAsync()
    {
        var hidden = (_split3DSettings.HiddenAdminMenuItems ?? string.Empty)
            .Split([',', ';', '\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (hidden.RemoveAll(x => _requiredAdminMenuItems.Contains(x, StringComparer.OrdinalIgnoreCase)) > 0)
        {
            _split3DSettings.HiddenAdminMenuItems = string.Join(", ", hidden);
            await _services.SettingFactory.SaveSettingsAsync(_split3DSettings);
        }
    }
}
