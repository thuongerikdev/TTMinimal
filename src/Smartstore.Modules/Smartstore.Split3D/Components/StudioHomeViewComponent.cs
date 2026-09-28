using Microsoft.AspNetCore.Mvc;
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
    const int MaxProducts = 8;
    const int ThumbnailSize = 600;

    private readonly SmartDbContext _db;
    private readonly IMediaService _mediaService;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IAclService _aclService;
    private readonly IStoreMappingService _storeMappingService;
    private readonly StudioSettings _settings;

    public StudioHomeViewComponent(
        SmartDbContext db,
        IMediaService mediaService,
        IPriceCalculationService priceCalculationService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        StudioSettings settings)
    {
        _db = db;
        _mediaService = mediaService;
        _priceCalculationService = priceCalculationService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _settings = settings;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var customer = Services.WorkContext.CurrentCustomer;
        var storeId = Services.StoreContext.CurrentStore.Id;

        var addonCategory = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == Split3DStorefrontContent.CategoryName && !x.Deleted);
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
            AddonCategoryUrl = addonCategory != null ? Url.RouteUrl("Category", new { SeName = await addonCategory.GetActiveSlugAsync() }) : null
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

        model.Products = await GetProductsAsync(addonProductIds, storeId);
        model.Addons = await GetAddonsAsync();

        return View(model);
    }

    private async Task<List<StudioProductCard>> GetProductsAsync(List<int> addonProductIds, int storeId)
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

        if (products.Count == 0)
        {
            return [];
        }

        var productIds = products.Select(x => x.Id).ToArray();
        var variantProductIds = await _db.ProductVariantAttributes
            .Where(x => productIds.Contains(x.ProductId))
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync();
        var productCategories = (await _db.ProductCategories
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.ProductId, x.CategoryId, x.Category.Name })
            .ToListAsync())
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
                HasVariants = variantProductIds.Contains(product.Id)
            });
        }

        return cards;
    }

    private async Task<List<StudioAddonCard>> GetAddonsAsync()
    {
        var addons = await _db.Split3DAddons()
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var mappings = await _db.Split3DAddonProducts().AsNoTracking().ToListAsync();
        var productIds = mappings.Select(x => x.ProductId).Distinct().ToArray();
        var products = (await _db.Products
            .AsNoTracking()
            .ApplyStandardFilter(false)
            .Where(x => productIds.Contains(x.Id))
            .ToListAsync())
            .ToDictionary(x => x.Id);

        var cards = new List<StudioAddonCard>();

        foreach (var addon in addons)
        {
            var addonProducts = mappings
                .Where(x => x.AddonId == addon.Id)
                .Select(x => products.Get(x.ProductId))
                .Where(x => x != null)
                .OrderBy(x => x.Price)
                .ToList();

            if (addonProducts.Count == 0)
            {
                continue;
            }

            var cheapest = addonProducts[0];
            var featured = addonProducts.FirstOrDefault(x => x.Sku == "S3D-1Y") ?? cheapest;
            var pictured = featured.MainPictureId > 0 ? featured : addonProducts.FirstOrDefault(x => x.MainPictureId > 0);

            cards.Add(new StudioAddonCard
            {
                Name = addon.Name,
                Version = addon.Version,
                Description = addon.Description.HasValue()
                    ? addon.Description.RemoveHtml().Truncate(220, "…")
                    : Split3DStorefrontContent.ShortDescription,
                Url = Url.RouteUrl("Product", new { SeName = await featured.GetActiveSlugAsync() }),
                ImageUrl = pictured != null ? await _mediaService.GetUrlAsync(pictured.MainPictureId, ThumbnailSize, null, false) : null,
                PriceFrom = PrintPriceList.FormatPrice(cheapest.Price),
                PlanCount = addonProducts.Count
            });
        }

        return cards;
    }
}
