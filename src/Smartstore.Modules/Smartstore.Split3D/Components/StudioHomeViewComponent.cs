using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Security;
using Smartstore.Core.Seo;
using Smartstore.Core.Stores;
using Smartstore.Web.Components;

namespace Smartstore.Split3D.Components;

/// <summary>
/// The studio home page: printing service, own products by category and Blender addons.
/// Invoked by the TTMinimal theme's Home/Index view.
/// </summary>
public class StudioHomeViewComponent : SmartViewComponent
{
    const int MaxProducts = 24;

    // Cards each category of the collection gets at least (when it has that many), so its filter is never empty.
    const int CardsPerCategory = 8;
    const int ThumbnailSize = MediaSettings.ThumbnailSizeLg;

    private readonly SmartDbContext _db;
    private readonly IMediaService _mediaService;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IAclService _aclService;
    private readonly IStoreMappingService _storeMappingService;
    private readonly StudioSettings _settings;
    private readonly StudioToolsCatalog _toolsCatalog;

    public StudioHomeViewComponent(
        SmartDbContext db,
        IMediaService mediaService,
        IPriceCalculationService priceCalculationService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        StudioSettings settings,
        StudioToolsCatalog toolsCatalog)
    {
        _db = db;
        _mediaService = mediaService;
        _priceCalculationService = priceCalculationService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _settings = settings;
        _toolsCatalog = toolsCatalog;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var customer = Services.WorkContext.CurrentCustomer;
        var storeId = Services.StoreContext.CurrentStore.Id;

        var addonCategory = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => Split3DStorefrontContent.CategoryNames.Contains(x.Name) && !x.Deleted);
        var addonProductIds = await _db.Split3DAddonProducts()
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync();

        var model = new StudioHomeModel
        {
            Contact = StudioContactModel.Create(_settings),
            Technologies = PrintPriceList.Parse(_settings.PrintPriceTable),
            PrintPriceNote = _settings.PrintPriceNote,
            PrintServiceUrl = Url.RouteUrl(StudioStorefrontSetup.PrintServiceRouteName),
            ToolsUrl = Url.RouteUrl(StudioStorefrontSetup.ToolsRouteName)
        };

        // Own product categories: every published top-level category except the addon category.
        var categories = await _db.Categories
            .AsNoTracking()
            .ApplyStandardFilter(false, customer.GetRoleIds(), storeId)
            .Where(x => (x.ParentId == null || x.ParentId == 0) && x.Id != (addonCategory != null ? addonCategory.Id : 0))
            .ToListAsync();

        var categoryIds = categories.Select(x => x.Id).ToArray();
        var productCounts = await _db.ProductCategories
            .AsNoTracking()
            .Where(x => categoryIds.Contains(x.CategoryId) && x.Product.Published && !x.Product.Deleted && x.Product.Visibility != ProductVisibility.Hidden)
            .GroupBy(x => x.CategoryId)
            .Select(x => new { CategoryId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        foreach (var category in categories)
        {
            model.Categories.Add(new StudioCategoryCard
            {
                Id = category.Id,
                Name = category.GetLocalized(x => x.Name),
                Url = Url.RouteUrl("Category", new { SeName = await category.GetActiveSlugAsync() }),
                ImageUrl = category.MediaFileId > 0 ? await _mediaService.GetUrlAsync(category.MediaFileId, ThumbnailSize, null, false) : null,
                ProductCount = productCounts.Get(category.Id)
            });
        }

        var serviceCategories = await GetServiceCategoriesAsync(customer.GetRoleIds(), storeId, addonCategory?.Id ?? 0);
        model.Products = await GetProductsAsync(addonProductIds, storeId, serviceCategories.Select(x => x.Id).ToArray());
        model.ServiceCategories = serviceCategories;
        model.Tools.Tools.AddRange(await _toolsCatalog.GetToolsAsync());
        model.Tools.ComingSoon.AddRange(await _toolsCatalog.GetComingSoonAsync());

        return View(model);
    }

    private async Task<List<StudioProductCard>> GetProductsAsync(List<int> addonProductIds, int storeId, int[] categoryIds)
    {
        var baseQuery = _db.Products
            .AsNoTracking()
            .ApplyStandardFilter(false)
            .Where(x => !addonProductIds.Contains(x.Id));

        // Products marked "show on home page" first, the newest ones otherwise.
        var products = await baseQuery
            .Where(x => x.ShowOnHomePage)
            .OrderBy(x => x.HomePageDisplayOrder)
            .Take(MaxProducts * 2)
            .ToListAsync();

        if (products.Count == 0)
        {
            products = await baseQuery
                .OrderByDescending(x => x.CreatedOnUtc)
                .Take(MaxProducts * 2)
                .ToListAsync();
        }

        products = await products
            .WhereAwait(async x => await _aclService.AuthorizeAsync(x) && await _storeMappingService.AuthorizeAsync(x))
            .Take(MaxProducts)
            .ToListAsync();

        // Every category of the column gets some cards too, also when none of its products is marked for the home
        // page (a new category shows up by itself).
        foreach (var categoryId in categoryIds)
        {
            var shown = products.Select(x => x.Id).ToArray();
            var have = await _db.ProductCategories.CountAsync(x => x.CategoryId == categoryId && shown.Contains(x.ProductId));
            if (have >= CardsPerCategory)
            {
                continue;
            }

            var more = await baseQuery
                .Where(x => !shown.Contains(x.Id) && _db.ProductCategories.Any(pc => pc.ProductId == x.Id && pc.CategoryId == categoryId))
                .OrderBy(x => x.DisplayOrder)
                .ThenByDescending(x => x.CreatedOnUtc)
                .Take(CardsPerCategory - have)
                .ToListAsync();

            products.AddRange(await more
                .WhereAwait(async x => await _aclService.AuthorizeAsync(x) && await _storeMappingService.AuthorizeAsync(x))
                .ToListAsync());
        }

        if (products.Count == 0)
        {
            return [];
        }

        var productIds = products.Select(x => x.Id).ToArray();
        var textControlTypes = new[] { (int)AttributeControlType.TextBox, (int)AttributeControlType.MultilineTextbox, (int)AttributeControlType.FileUpload };
        var customProductIds = await _db.ProductVariantAttributes
            .Where(x => productIds.Contains(x.ProductId) && textControlTypes.Contains(x.AttributeControlTypeId))
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync();
        var variantProductIds = await _db.ProductVariantAttributes
            .Where(x => productIds.Contains(x.ProductId))
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync();
        var productCategoryList = await _db.ProductCategories
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.ProductId, x.CategoryId, x.Category.Name })
            .ToListAsync();
        var productCategories = productCategoryList
            .DistinctBy(x => x.ProductId)
            .ToDictionary(x => x.ProductId);

        var options = _priceCalculationService.CreateDefaultOptions(true);
        var cards = new List<StudioProductCard>();

        foreach (var product in products)
        {
            var price = await _priceCalculationService.CalculatePriceAsync(new PriceCalculationContext(product, options));
            var regular = price.RegularPrice;

            cards.Add(new StudioProductCard
            {
                Name = product.GetLocalized(x => x.Name),
                Url = Url.RouteUrl("Product", new { SeName = await product.GetActiveSlugAsync() }),
                ImageUrl = product.MainPictureId > 0 ? await _mediaService.GetUrlAsync(product.MainPictureId, ThumbnailSize, null, false) : null,
                Price = product.CallForPrice ? T("Products.CallForPrice") : price.FinalPrice.ToString(),
                OldPrice = !product.CallForPrice && regular.HasValue && regular.Value > price.FinalPrice ? regular.Value.ToString() : null,
                PriceFrom = price.HasPriceRange,
                CategoryName = productCategories.Get(product.Id)?.Name,
                CategoryId = productCategories.Get(product.Id)?.CategoryId ?? 0,
                CategoryIds = productCategoryList.Where(x => x.ProductId == product.Id).Select(x => x.CategoryId).Distinct().ToArray(),
                HasVariants = variantProductIds.Contains(product.Id),
                IsCustom = customProductIds.Contains(product.Id)
            });
        }

        return cards;
    }

    /// <summary>
    /// The collection's category column: every published category (also sub categories such as "Chậu hoa") that has
    /// visible products of its own, except the addon category (the tools have their own section). A new category
    /// appears as soon as it has a product.
    /// </summary>
    private async Task<List<StudioCategoryCard>> GetServiceCategoriesAsync(int[] roleIds, int storeId, int addonCategoryId)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .ApplyStandardFilter(false, roleIds, storeId)
            .Where(x => x.Id != addonCategoryId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var categoryIds = categories.Select(x => x.Id).ToArray();
        var counts = await _db.ProductCategories
            .AsNoTracking()
            .Where(x => categoryIds.Contains(x.CategoryId) && x.Product.Published && !x.Product.Deleted && x.Product.Visibility != ProductVisibility.Hidden)
            .GroupBy(x => x.CategoryId)
            .Select(x => new { CategoryId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        categories = categories.Where(x => counts.Get(x.Id) > 0).ToList();

        var cards = new List<StudioCategoryCard>();

        foreach (var category in categories)
        {
            cards.Add(new StudioCategoryCard
            {
                Id = category.Id,
                Name = category.GetLocalized(x => x.Name),
                Url = Url.RouteUrl("Category", new { SeName = await category.GetActiveSlugAsync() }),
                ProductCount = counts.Get(category.Id)
            });
        }

        return cards;
    }
}
