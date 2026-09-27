using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core;
using Smartstore.Core.Catalog;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Checkout.Tax;
using Smartstore.Core.Common;
using Smartstore.Core.Common.Configuration;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Content.Topics;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Seo;
using Smartstore.Engine.Modularity;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Turns a fresh Smartstore installation into a Vietnamese shop selling Split3D plans:
/// language, currency, products with downloads, payment, checkout settings and home page.
/// Every step is idempotent, so the setup can be run again after changes.
/// </summary>
public class Split3DStorefrontSetup
{
    public const string PrepaymentSystemName = "Payments.Prepayment";
    private const string VietnameseCulture = "vi-VN";

    private readonly SmartDbContext _db;
    private readonly ICommonServices _services;
    private readonly IXmlResourceManager _resourceManager;
    private readonly IModuleCatalog _moduleCatalog;
    private readonly IUrlService _urlService;
    private readonly IMediaService _mediaService;
    private readonly IDownloadService _downloadService;
    private readonly Split3DSettings _settings;

    public Split3DStorefrontSetup(
        SmartDbContext db,
        ICommonServices services,
        IXmlResourceManager resourceManager,
        IModuleCatalog moduleCatalog,
        IUrlService urlService,
        IMediaService mediaService,
        IDownloadService downloadService,
        Split3DSettings settings)
    {
        _db = db;
        _services = services;
        _resourceManager = resourceManager;
        _moduleCatalog = moduleCatalog;
        _urlService = urlService;
        _mediaService = mediaService;
        _downloadService = downloadService;
        _settings = settings;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Runs all setup steps. Returns a human readable log of what was done.
    /// </summary>
    /// <param name="addonFile">The addon ZIP to attach as download, or <c>null</c> to keep existing downloads.</param>
    public async Task<List<string>> RunAsync(Stream addonFile, string addonFileName, CancellationToken cancelToken = default)
    {
        var log = new List<string>();

        await SetupLanguageAsync(log, cancelToken);
        var vnd = await SetupCurrencyAsync(log, cancelToken);
        await SetupStoreAsync(log, cancelToken);
        var category = await SetupCategoryAsync(log, cancelToken);
        var products = await SetupProductsAsync(category, addonFile, addonFileName, log, cancelToken);
        await SetupPaymentAsync(log, cancelToken);
        await SetupCheckoutAsync(log, cancelToken);
        await SetupTopicsAsync(products, vnd, log, cancelToken);

        return log;
    }

    /// <summary>
    /// Returns whether the storefront has been set up already (Vietnamese language and addon category exist).
    /// </summary>
    public async Task<bool> IsSetUpAsync(CancellationToken cancelToken = default)
    {
        return await _db.Languages.AnyAsync(x => x.LanguageCulture == VietnameseCulture, cancelToken)
            && await _db.Categories.AnyAsync(x => x.Name == Split3DStorefrontContent.CategoryName && !x.Deleted, cancelToken);
    }

    /// <summary>
    /// Regenerates the content that depends on the bank details, plan prices and addon version:
    /// the bank transfer description, the home page and the terms, shipping and payment pages.
    /// Store settings and products are left untouched.
    /// </summary>
    public async Task RefreshContentAsync(CancellationToken cancelToken = default)
    {
        var method = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.PaymentMethodSystemName == PrepaymentSystemName, cancelToken);
        if (method != null)
        {
            method.FullDescription = Split3DStorefrontContent.PaymentDescription(_settings.BankName, _settings.BankAccountNumber, _settings.BankAccountHolder);
            await _db.SaveChangesAsync(cancelToken);
        }

        var skus = Split3DStorefrontContent.Plans.Select(x => x.Sku).ToArray();
        var products = (await _db.Products
            .Where(x => skus.Contains(x.Sku) && !x.Deleted)
            .OrderBy(x => x.Id)
            .ToListAsync(cancelToken))
            .DistinctBy(x => x.Sku)
            .ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

        if (products.Count < skus.Length)
        {
            // The home page links every plan product. Without all of them there is nothing sensible to render.
            Logger.Warn("Split3D: home page not refreshed because not all plan products exist.");
            return;
        }

        await SetupTopicsAsync(products, null, [], cancelToken);

        // Topics are rendered as cached widgets.
        await _services.Cache.ClearAsync();
    }

    #region Language

    private async Task SetupLanguageAsync(List<string> log, CancellationToken cancelToken)
    {
        var vi = await _db.Languages.FirstOrDefaultAsync(x => x.LanguageCulture == VietnameseCulture, cancelToken);
        var hasResources = vi != null && await _db.LocaleStringResources.AnyAsync(x => x.LanguageId == vi.Id && x.ResourceName == "ShoppingCart.AddToCart", cancelToken);

        if (!hasResources)
        {
            // Official, fully translated Vietnamese resource set from translate.smartstore.com.
            // DownloadResourceSetAsync creates the language if it does not exist yet.
            var response = await _resourceManager.GetOnlineResourceSetsAsync(cancelToken);
            var set = response?.Resources?.FirstOrDefault(x => x.Language?.Culture?.EqualsNoCase(VietnameseCulture) == true);

            if (set != null && await _resourceManager.DownloadResourceSetAsync(set.Id, response, cancelToken))
            {
                log.Add($"Downloaded Vietnamese language pack ({set.TranslatedCount:N0} resources, {set.TranslatedPercentage:0}%).");
            }
            else
            {
                log.Add("WARNING: Vietnamese language pack could not be downloaded. Storefront texts stay English.");
            }

            vi = await _db.Languages.FirstOrDefaultAsync(x => x.LanguageCulture == VietnameseCulture, cancelToken);
        }

        if (vi == null)
        {
            return;
        }

        // Module resources (e.g. this module's resources.vi-vn.xml) are not part of the core set.
        foreach (var module in _moduleCatalog.GetInstalledModules())
        {
            await _resourceManager.ImportModuleResourcesFromXmlAsync(module, null, false, [vi]);
        }

        var languages = await _db.Languages.ToListAsync(cancelToken);
        var english = languages.FirstOrDefault(x => x.LanguageCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase));

        // The store's default language is the first published one by display order.
        vi.Published = true;
        vi.Name = "Tiếng Việt";
        vi.UniqueSeoCode = "vi";
        vi.FlagImageFileName = "vn.png";
        vi.DisplayOrder = 0;

        var order = 1;
        foreach (var language in languages.Where(x => x.Id != vi.Id).OrderBy(x => x.DisplayOrder))
        {
            language.DisplayOrder = order++;
        }

        await _db.SaveChangesAsync(cancelToken);

        var localizationSettings = await _services.SettingFactory.LoadSettingsAsync<LocalizationSettings>();
        if (english != null)
        {
            // Keep the admin area in English.
            localizationSettings.DefaultAdminLanguageId = english.Id;
        }
        localizationSettings.DetectBrowserUserLanguage = false;
        await _services.SettingFactory.SaveSettingsAsync(localizationSettings);

        log.Add("Vietnamese is now the default storefront language (admin area stays English).");
    }

    #endregion

    #region Currency & store

    private async Task<Currency> SetupCurrencyAsync(List<string> log, CancellationToken cancelToken)
    {
        var vnd = await _db.Currencies.FirstOrDefaultAsync(x => x.CurrencyCode == "VND", cancelToken);
        if (vnd == null)
        {
            vnd = new Currency { CurrencyCode = "VND" };
            _db.Currencies.Add(vnd);
        }

        vnd.Name = "Việt Nam Đồng";
        vnd.Rate = 1;
        vnd.DisplayLocale = VietnameseCulture;
        vnd.CustomFormatting = "#,##0 ₫";
        vnd.RoundNumDecimals = 0;
        vnd.Published = true;
        vnd.DisplayOrder = 0;

        // With exactly one published currency, every visitor gets VND.
        foreach (var other in await _db.Currencies.Where(x => x.CurrencyCode != "VND").ToListAsync(cancelToken))
        {
            other.Published = false;
        }

        await _db.SaveChangesAsync(cancelToken);

        var currencySettings = await _services.SettingFactory.LoadSettingsAsync<CurrencySettings>();
        currencySettings.PrimaryCurrencyId = vnd.Id;
        currencySettings.PrimaryExchangeCurrencyId = vnd.Id;
        currencySettings.AutoUpdateEnabled = false;
        await _services.SettingFactory.SaveSettingsAsync(currencySettings);

        foreach (var store in await _db.Stores.ToListAsync(cancelToken))
        {
            store.DefaultCurrencyId = vnd.Id;
            store.PrimaryExchangeRateCurrencyId = vnd.Id;
        }

        await _db.SaveChangesAsync(cancelToken);

        log.Add("VND is the primary and only published currency.");
        return vnd;
    }

    private async Task SetupStoreAsync(List<string> log, CancellationToken cancelToken)
    {
        foreach (var store in await _db.Stores.ToListAsync(cancelToken))
        {
            if (store.Name.IsEmpty() || store.Name == "Your store name")
            {
                store.Name = Split3DStorefrontContent.StoreName;
                log.Add($"Store name set to \"{store.Name}\".");
            }
        }

        // Put Viet Nam first in address country lists.
        var vietnam = await _db.Countries.FirstOrDefaultAsync(x => x.TwoLetterIsoCode == "VN", cancelToken);
        if (vietnam != null)
        {
            vietnam.Published = true;
            vietnam.AllowsBilling = true;
            vietnam.DisplayOrder = 0;
        }

        await _db.SaveChangesAsync(cancelToken);

        var homePageSettings = await _services.SettingFactory.LoadSettingsAsync<HomePageSettings>();
        homePageSettings.MetaTitle = "Split3D Print – Addon Blender cắt & nối mô hình in 3D";
        homePageSettings.MetaDescription = Split3DStorefrontContent.ShortDescription;
        homePageSettings.MetaKeywords = "split3d, blender addon, in 3d, cắt mô hình, khớp nối, 3d print";
        await _services.SettingFactory.SaveSettingsAsync(homePageSettings);

        var catalogSettings = await _services.SettingFactory.LoadSettingsAsync<CatalogSettings>();
        catalogSettings.ShowManufacturersOnHomepage = false;
        catalogSettings.ShowBestsellersOnHomepage = false;
        catalogSettings.ShowPopularProductTagsOnHomepage = false;
        catalogSettings.LegalInfoInLists = ProductLegalInfo.None;
        catalogSettings.LegalInfoInProductDetail = ProductLegalInfo.None;
        // The category and product descriptions hold the landing page: show them in full instead of behind "Show more".
        catalogSettings.EnableHtmlTextCollapser = false;
        await _services.SettingFactory.SaveSettingsAsync(catalogSettings);
    }

    #endregion

    #region Catalog

    private async Task<Category> SetupCategoryAsync(List<string> log, CancellationToken cancelToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(x => x.Name == Split3DStorefrontContent.CategoryName && !x.Deleted, cancelToken);
        if (category == null)
        {
            var template = await _db.CategoryTemplates.FirstOrDefaultAsync(x => x.ViewPath == "CategoryTemplate.ProductsInGridOrLines", cancelToken)
                ?? await _db.CategoryTemplates.FirstAsync(cancelToken);

            category = new Category
            {
                Name = Split3DStorefrontContent.CategoryName,
                Description = "<p>" + Split3DStorefrontContent.ShortDescription + "</p>",
                CategoryTemplateId = template.Id,
                Published = true,
                DisplayOrder = 1,
                ShowOnHomePage = false
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync(cancelToken);
            log.Add($"Created category \"{category.Name}\".");
        }

        await _urlService.SaveSlugAsync(category, Split3DStorefrontContent.CategorySlug, category.Name, true);
        await _db.SaveChangesAsync(cancelToken);

        return category;
    }

    private async Task<Dictionary<string, Product>> SetupProductsAsync(
        Category category,
        Stream addonFile,
        string addonFileName,
        List<string> log,
        CancellationToken cancelToken)
    {
        var addon = await EnsureDefaultAddonAsync(cancelToken);
        var module = _moduleCatalog.GetModuleByAssembly(GetType().Assembly);
        var addonBytes = addonFile != null ? await ReadAllBytesAsync(addonFile, cancelToken) : null;
        if (addonBytes != null)
        {
            (addonBytes, var package) = PrepareAddonFile(addonBytes, log);
            if (package != null)
            {
                addon.Version = package.Version;
                await _db.SaveChangesAsync(cancelToken);
            }
        }

        var specs = new List<Split3DPlanProductSpec>();
        foreach (var plan in Split3DStorefrontContent.Plans)
        {
            var image = module?.WebRoot?.GetFileInfo("setup/" + plan.ImageFile);
            specs.Add(new Split3DPlanProductSpec
            {
                Plan = plan.Plan,
                Sku = plan.Sku,
                Name = plan.Name,
                Slug = plan.Slug,
                Price = plan.Price,
                ShortDescription = Split3DStorefrontContent.ShortDescription,
                FullDescription = Split3DStorefrontContent.ProductFullDescription(plan, addon.Version),
                ImageFileName = plan.ImageFile,
                ImageBytes = image != null && image.Exists ? await ReadFileInfoAsync(image, cancelToken) : null,
                ShowOnHomePage = false
            });
        }

        var products = await CreatePlanProductsAsync(addon, specs, category, addonBytes, addonFileName, log, cancelToken);

        // Keys are issued by the shop owner after confirming the bank transfer (Key Split3D > pending orders).
        _settings.AutoIssueEnabled = false;
        await _services.SettingFactory.SaveSettingsAsync(_settings);
        log.Add("Products mapped to license plans. Keys are issued from the pending orders panel after payment is confirmed.");

        if (addonBytes == null && !await _db.Downloads.AnyAsync(x => x.EntityName == nameof(Product) && x.EntityId == products["S3D-1Y"].Id, cancelToken))
        {
            log.Add("WARNING: no addon file uploaded yet. Upload split3d_print.zip under Addons & plans > Upload new version.");
        }

        return products;
    }

    /// <summary>
    /// Gets the addon of the original Split3D Print product, creating it if necessary.
    /// </summary>
    public async Task<Split3DAddon> EnsureDefaultAddonAsync(CancellationToken cancelToken = default)
    {
        var code = _settings.ProductCode.NullEmpty() ?? Migrations.Addons.DefaultProductCode;
        var addon = await _db.Split3DAddons().FirstOrDefaultAsync(x => x.ProductCode == code, cancelToken);
        if (addon == null)
        {
            addon = new Split3DAddon { Name = Migrations.Addons.DefaultAddonName, ProductCode = code, Active = true };
            _db.Split3DAddons().Add(addon);
        }

        // The version of an uploaded extension package wins; the built-in value is only a fallback.
        addon.Version = addon.Version.NullEmpty() ?? Split3DStorefrontContent.AddonVersion;
        await _db.SaveChangesAsync(cancelToken);

        return addon;
    }

    /// <summary>
    /// Gets the storefront category for addons, creating it if necessary.
    /// </summary>
    public Task<Category> GetAddonCategoryAsync(CancellationToken cancelToken = default)
        => SetupCategoryAsync([], cancelToken);

    /// <summary>
    /// Creates or updates one catalog product per plan for <paramref name="addon"/>, maps each product to the
    /// addon and plan, and attaches the addon file as download. Prices are only set when a product is created.
    /// </summary>
    /// <returns>Products by SKU.</returns>
    public async Task<Dictionary<string, Product>> CreatePlanProductsAsync(
        Split3DAddon addon,
        IEnumerable<Split3DPlanProductSpec> specs,
        Category category,
        byte[] addonBytes,
        string addonFileName,
        List<string> log,
        CancellationToken cancelToken = default)
    {
        Guard.NotNull(addon);
        Guard.NotNull(specs);

        var template = await _db.ProductTemplates.FirstOrDefaultAsync(x => x.ViewPath == "Product", cancelToken)
            ?? await _db.ProductTemplates.FirstAsync(cancelToken);

        var result = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        var displayOrder = await _db.ProductCategories.Where(x => x.CategoryId == category.Id).CountAsync(cancelToken);

        foreach (var spec in specs)
        {
            var product = await _db.Products.FirstOrDefaultAsync(x => x.Sku == spec.Sku && !x.Deleted, cancelToken);
            var isNew = product == null;

            if (isNew)
            {
                product = new Product
                {
                    ProductType = ProductType.SimpleProduct,
                    Sku = spec.Sku,
                    ProductTemplateId = template.Id,
                    // Prices are only set on creation so later admin changes survive a re-run.
                    Price = spec.Price,
                    StockQuantity = 10000,
                    QuantityStep = 1,
                    AllowCustomerReviews = false,
                    DisplayOrder = ++displayOrder
                };

                _db.Products.Add(product);
            }

            product.Name = spec.Name;
            product.ShortDescription = spec.ShortDescription;
            product.FullDescription = spec.FullDescription;
            product.MetaTitle = spec.Name;
            product.MetaDescription = spec.ShortDescription;
            product.Visibility = ProductVisibility.Full;
            product.Published = true;
            product.ManageInventoryMethod = ManageInventoryMethod.DontManageStock;
            product.OrderMinimumQuantity = 1;
            product.OrderMaximumQuantity = 10;
            product.IsShippingEnabled = false;
            product.IsFreeShipping = true;
            product.IsTaxExempt = true;
            product.IsEsd = true;
            product.IsDownload = true;
            product.DownloadActivationType = DownloadActivationType.WhenOrderIsPaid;
            product.UnlimitedDownloads = true;
            product.HasUserAgreement = false;
            product.ShowOnHomePage = spec.ShowOnHomePage;

            await _db.SaveChangesAsync(cancelToken);
            await _urlService.SaveSlugAsync(product, spec.Slug, spec.Name, true);

            if (!await _db.ProductCategories.AnyAsync(x => x.ProductId == product.Id && x.CategoryId == category.Id, cancelToken))
            {
                _db.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = category.Id, DisplayOrder = product.DisplayOrder });
            }

            var mapping = await _db.Split3DAddonProducts().FirstOrDefaultAsync(x => x.ProductId == product.Id, cancelToken);
            if (mapping == null)
            {
                mapping = new Split3DAddonProduct { ProductId = product.Id };
                _db.Split3DAddonProducts().Add(mapping);
            }

            mapping.AddonId = addon.Id;
            mapping.KeyType = spec.Plan;
            mapping.Days = spec.Plan == Split3DPlans.Custom ? spec.Days : null;

            await _db.SaveChangesAsync(cancelToken);

            if (spec.ImageBytes?.Length > 0)
            {
                await EnsurePictureAsync(product, spec.ImageBytes, spec.ImageFileName, cancelToken);
            }

            if (addonBytes?.Length > 0)
            {
                await EnsureDownloadAsync(product, addon.Version, addonBytes, addonFileName, cancelToken);
            }

            result[spec.Sku] = product;
            log.Add($"{(isNew ? "Created" : "Updated")} product \"{spec.Name}\" ({spec.Sku}) for addon {addon.Name}.");
        }

        return result;
    }

    /// <summary>
    /// The address the addon uses to reach this shop: <see cref="Split3DSettings.ActivationServerUrl"/> or the store URL.
    /// </summary>
    public string GetActivationServerUrl()
        => (_settings.ActivationServerUrl.NullEmpty() ?? _services.StoreContext.CurrentStore.GetBaseUrl()).Trim().TrimEnd('/');

    /// <summary>
    /// Prepares an uploaded addon zip: embeds this shop as SERVER_URL and reads the extension manifest.
    /// </summary>
    /// <exception cref="ArgumentException">The zip or its manifest is invalid.</exception>
    public (byte[] Bytes, Split3DPackageInfo Package) PrepareAddonFile(byte[] addonBytes, List<string> log)
    {
        Guard.NotNull(addonBytes);

        var serverUrl = GetActivationServerUrl();
        var bytes = Split3DAddonPackage.SetServerUrl(addonBytes, serverUrl, out var patched);
        var package = Split3DAddonPackage.Read(bytes);

        log?.Add(patched
            ? $"Addon file: activation server set to {serverUrl}."
            : "WARNING: the addon file has no online.py with SERVER_URL; online activation is not configured in it.");
        log?.Add(package != null
            ? $"Addon file: Blender extension \"{package.Id}\" {package.Version} (installs over older versions, updates from the shop repository)."
            : "WARNING: the addon file has no blender_manifest.toml (legacy add-on): it cannot be updated from inside Blender.");

        return (bytes, package);
    }

    /// <summary>
    /// Attaches a new addon file version to every product mapped to <paramref name="addon"/>.
    /// Customers who bought the addon can then download the newest version.
    /// </summary>
    /// <returns>Number of products the file was attached to.</returns>
    public async Task<int> AttachAddonFileAsync(Split3DAddon addon, byte[] addonBytes, string fileName, CancellationToken cancelToken = default)
    {
        Guard.NotNull(addon);
        Guard.NotNull(addonBytes);

        var productIds = await _db.Split3DAddonProducts()
            .Where(x => x.AddonId == addon.Id)
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync(cancelToken);

        var products = await _db.Products.Where(x => productIds.Contains(x.Id) && !x.Deleted).ToListAsync(cancelToken);
        foreach (var product in products)
        {
            product.IsDownload = true;
            product.DownloadActivationType = DownloadActivationType.WhenOrderIsPaid;
            product.UnlimitedDownloads = true;
            await _db.SaveChangesAsync(cancelToken);
            await EnsureDownloadAsync(product, addon.Version, addonBytes, fileName, cancelToken);
        }

        return products.Count;
    }

    private async Task EnsurePictureAsync(Product product, byte[] imageBytes, string fileName, CancellationToken cancelToken)
    {
        if (await _db.ProductMediaFiles.AnyAsync(x => x.ProductId == product.Id, cancelToken))
        {
            return;
        }

        var path = _mediaService.CombinePaths(SystemAlbumProvider.Catalog, fileName.NullEmpty() ?? $"product-{product.Id}.png");
        var mediaFile = await _mediaService.GetFileByPathAsync(path);
        if (mediaFile == null)
        {
            using var stream = new MemoryStream(imageBytes);
            mediaFile = await _mediaService.SaveFileAsync(path, stream, false, DuplicateFileHandling.Rename);
        }

        _db.ProductMediaFiles.Add(new ProductMediaFile { ProductId = product.Id, MediaFileId = mediaFile.Id, DisplayOrder = 1 });
        product.MainPictureId = mediaFile.Id;
        await _db.SaveChangesAsync(cancelToken);
    }

    private async Task EnsureDownloadAsync(Product product, string version, byte[] addonBytes, string fileName, CancellationToken cancelToken)
    {
        version = version.NullEmpty() ?? "1.0.0";
        var exists = await _db.Downloads.AnyAsync(x =>
            x.EntityName == nameof(Product) &&
            x.EntityId == product.Id &&
            x.FileVersion == version, cancelToken);

        if (exists)
        {
            return;
        }

        var download = new Download
        {
            DownloadGuid = Guid.NewGuid(),
            UseDownloadUrl = false,
            DownloadUrl = string.Empty,
            IsTransient = false,
            UpdatedOnUtc = DateTime.UtcNow,
            EntityId = product.Id,
            EntityName = nameof(Product),
            FileVersion = version,
            Changelog = $"{product.Name} {version}"
        };

        using var stream = new MemoryStream(addonBytes);
        await _downloadService.InsertDownloadAsync(download, stream, fileName.NullEmpty() ?? "addon.zip");
    }

    public static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancelToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancelToken);
        return buffer.ToArray();
    }

    private static async Task<byte[]> ReadFileInfoAsync(Microsoft.Extensions.FileProviders.IFileInfo file, CancellationToken cancelToken)
    {
        await using var stream = file.CreateReadStream();
        return await ReadAllBytesAsync(stream, cancelToken);
    }

    #endregion

    #region Payment & checkout

    private async Task SetupPaymentAsync(List<string> log, CancellationToken cancelToken)
    {
        var paymentSettings = await _services.SettingFactory.LoadSettingsAsync<PaymentSettings>();
        paymentSettings.ActivePaymentMethodSystemNames = [PrepaymentSystemName];
        paymentSettings.SkipPaymentSelectionIfSingleOption = false;
        await _services.SettingFactory.SaveSettingsAsync(paymentSettings);

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.PaymentMethodSystemName == PrepaymentSystemName, cancelToken);
        if (method == null)
        {
            method = new PaymentMethod { PaymentMethodSystemName = PrepaymentSystemName };
            _db.PaymentMethods.Add(method);
        }

        method.FullDescription = Split3DStorefrontContent.PaymentDescription(_settings.BankName, _settings.BankAccountNumber, _settings.BankAccountHolder);
        await _db.SaveChangesAsync(cancelToken);

        log.Add(_settings.BankAccountNumber.HasValue()
            ? "Bank transfer (Prepayment) is the only payment method."
            : "Bank transfer (Prepayment) is the only payment method. WARNING: bank account details are not configured yet.");
    }

    private async Task SetupCheckoutAsync(List<string> log, CancellationToken cancelToken)
    {
        // Downloads are only available to registered customers.
        var orderSettings = await _services.SettingFactory.LoadSettingsAsync<OrderSettings>();
        orderSettings.AnonymousCheckoutAllowed = false;
        await _services.SettingFactory.SaveSettingsAsync(orderSettings);

        // Digital goods: only name, email and phone are needed in the billing address.
        var addressSettings = await _services.SettingFactory.LoadSettingsAsync<AddressSettings>();
        addressSettings.CompanyEnabled = true;
        addressSettings.CompanyRequired = false;
        addressSettings.StreetAddressEnabled = true;
        addressSettings.StreetAddressRequired = false;
        addressSettings.StreetAddress2Enabled = false;
        addressSettings.ZipPostalCodeEnabled = false;
        addressSettings.ZipPostalCodeRequired = false;
        addressSettings.CityEnabled = true;
        addressSettings.CityRequired = false;
        addressSettings.CountryEnabled = true;
        addressSettings.CountryRequired = true;
        addressSettings.StateProvinceEnabled = false;
        addressSettings.StateProvinceRequired = false;
        addressSettings.PhoneEnabled = true;
        addressSettings.PhoneRequired = true;
        addressSettings.FaxEnabled = false;
        addressSettings.FaxRequired = false;
        await _services.SettingFactory.SaveSettingsAsync(addressSettings);

        var customerSettings = await _services.SettingFactory.LoadSettingsAsync<CustomerSettings>();
        customerSettings.GenderEnabled = false;
        customerSettings.DateOfBirthEnabled = false;
        customerSettings.FirstNameRequired = true;
        customerSettings.LastNameRequired = true;
        customerSettings.HideDownloadableProductsTab = false;
        await _services.SettingFactory.SaveSettingsAsync(customerSettings);

        var cartSettings = await _services.SettingFactory.LoadSettingsAsync<ShoppingCartSettings>();
        cartSettings.ShowEsdRevocationWaiverBox = false;
        // Digital goods: cart > payment > confirm, no address or shipping steps.
        cartSettings.CheckoutProcess = CheckoutProcess.TerminalWithPayment;
        await _services.SettingFactory.SaveSettingsAsync(cartSettings);

        var taxSettings = await _services.SettingFactory.LoadSettingsAsync<TaxSettings>();
        taxSettings.DisplayTaxSuffix = false;
        taxSettings.ShowLegalHintsInFooter = false;
        await _services.SettingFactory.SaveSettingsAsync(taxSettings);

        await _db.SaveChangesAsync(cancelToken);
        log.Add("Checkout simplified for digital goods: cart, payment, confirm (login required, no address or shipping).");
    }

    #endregion

    #region Topics

    private async Task SetupTopicsAsync(Dictionary<string, Product> products, Currency vnd, List<string> log, CancellationToken cancelToken)
    {
        var urls = new Dictionary<string, string>();
        var prices = new Dictionary<string, string>();
        var culture = CultureInfo.GetCultureInfo(VietnameseCulture);

        foreach (var (sku, product) in products)
        {
            var slug = await _urlService.GetActiveSlugAsync(product.Id, nameof(Product), 0);
            urls[sku] = "/" + (slug.NullEmpty() ?? Split3DStorefrontContent.Plans.First(x => x.Sku == sku).Slug);
            prices[sku] = product.Price.ToString("#,##0", culture) + " ₫";
        }

        await UpsertTopicAsync("HomePageText", string.Empty, Split3DStorefrontContent.HomePage(urls, prices, (await EnsureDefaultAddonAsync(cancelToken)).Version), cancelToken, topic =>
        {
            topic.RenderAsWidget = true;
            topic.WidgetWrapContent = false;
            topic.WidgetShowTitle = false;
            topic.WidgetBordered = false;
            topic.EnableProseContainer = false;
        });
        await UpsertTopicAsync("ConditionsOfUse", "Điều khoản sử dụng", Split3DStorefrontContent.ConditionsOfUse, cancelToken);
        await UpsertTopicAsync("ShippingInfo", "Giao hàng", Split3DStorefrontContent.ShippingInfo, cancelToken);
        await UpsertTopicAsync("PaymentInfo", "Thanh toán",
            Split3DStorefrontContent.PaymentDescription(_settings.BankName, _settings.BankAccountNumber, _settings.BankAccountHolder), cancelToken);

        log.Add("Home page, terms, shipping and payment pages updated.");
    }

    private async Task UpsertTopicAsync(string systemName, string title, string body, CancellationToken cancelToken, Action<Topic> configure = null)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(x => x.SystemName == systemName, cancelToken);
        if (topic == null)
        {
            topic = new Topic { SystemName = systemName, IsSystemTopic = true, IsPublished = true, IncludeInSitemap = false };
            _db.Topics.Add(topic);
        }

        topic.Title = title;
        topic.Body = body;
        configure?.Invoke(topic);

        await _db.SaveChangesAsync(cancelToken);

        // Remove localized overrides so the Vietnamese default body is shown in every language.
        var localized = await _db.LocalizedProperties
            .Where(x => x.LocaleKeyGroup == nameof(Topic) && x.EntityId == topic.Id && (x.LocaleKey == nameof(Topic.Body) || x.LocaleKey == nameof(Topic.Title)))
            .ToListAsync(cancelToken);

        if (localized.Count > 0)
        {
            _db.LocalizedProperties.RemoveRange(localized);
            await _db.SaveChangesAsync(cancelToken);
        }
    }

    #endregion
}
