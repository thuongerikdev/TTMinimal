using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Core.Stores;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// The "Công cụ 3D" page: every active tool with its packages (duration × devices) and a "buy now" button that puts
/// exactly one package into the cart and opens the checkout. Product pages of packages redirect here.
/// </summary>
[Route("cong-cu-3d")]
public class ToolsController : PublicController
{
    private const int ThumbnailSize = MediaSettings.ThumbnailSizeLg;

    private readonly SmartDbContext _db;
    private readonly IShoppingCartService _cartService;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IMediaService _mediaService;
    private readonly IAclService _aclService;
    private readonly IStoreMappingService _storeMappingService;
    private readonly Split3DSettings _settings;

    public ToolsController(
        SmartDbContext db,
        IShoppingCartService cartService,
        IPriceCalculationService priceCalculationService,
        IMediaService mediaService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        Split3DSettings settings)
    {
        _db = db;
        _cartService = cartService;
        _priceCalculationService = priceCalculationService;
        _mediaService = mediaService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _settings = settings;
    }

    [HttpGet("", Name = StudioStorefrontSetup.ToolsRouteName)]
    public async Task<IActionResult> Index(int? goi)
    {
        var model = new ToolsPageModel { SelectedProductId = goi };
        var addons = await _db.Split3DAddons().AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToListAsync();

        var mappings = await _db.Split3DAddonProducts().AsNoTracking().ToListAsync();
        var productIds = mappings.Select(x => x.ProductId).Distinct().ToArray();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.Published && !x.Deleted)
            .ToDictionaryAsync(x => x.Id);

        var options = _priceCalculationService.CreateDefaultOptions(true);

        foreach (var addon in addons)
        {
            var tool = new ToolCardModel
            {
                AddonId = addon.Id,
                Name = addon.Name,
                Version = addon.Version,
                Description = addon.Description
            };

            foreach (var mapping in mappings.Where(x => x.AddonId == addon.Id))
            {
                if (!products.TryGetValue(mapping.ProductId, out var product)
                    || !await _aclService.AuthorizeAsync(product)
                    || !await _storeMappingService.AuthorizeAsync(product))
                {
                    continue;
                }

                var price = await _priceCalculationService.CalculatePriceAsync(new PriceCalculationContext(product, options));
                var devices = mapping.MaxDevices ?? _settings.DefaultMaxDevices;

                tool.Packages.Add(new ToolPackageModel
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Duration = mapping.KeyType == Split3DPlans.Custom && mapping.Days.HasValue ? $"{mapping.Days} ngày" : mapping.KeyType,
                    Devices = Math.Max(devices, 1),
                    Price = price.FinalPrice.ToString(),
                    PriceValue = price.FinalPrice.Amount,
                    IsLifetime = mapping.KeyType == Split3DPlans.Lifetime,
                    DisplayOrder = product.DisplayOrder,
                    ImageUrl = product.MainPictureId > 0 ? await _mediaService.GetUrlAsync(product.MainPictureId, ThumbnailSize, null, false) : null
                });

                if (tool.Description.IsEmpty() && product.ShortDescription.HasValue())
                {
                    tool.Description = "<p>" + System.Net.WebUtility.HtmlEncode(product.ShortDescription) + "</p>";
                }
            }

            if (tool.Packages.Count == 0)
            {
                continue;
            }

            // Fewest devices first, then by price: "1 máy · 3 tháng" … "5 máy · vĩnh viễn".
            tool.Packages = tool.Packages.OrderBy(x => x.Devices).ThenBy(x => x.PriceValue).ToList();
            tool.DeviceOptions = tool.Packages.Select(x => x.Devices).Distinct().ToList();
            model.Tools.Add(tool);
        }

        return View(model);
    }

    /// <summary>
    /// Buy now: one package in the cart (a package already there is kept, not doubled), then checkout.
    /// </summary>
    [HttpPost("mua")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(int productId)
    {
        var isPackage = await _db.Split3DAddonProducts().AnyAsync(x => x.ProductId == productId);
        var product = isPackage ? await _db.Products.FindByIdAsync(productId, false) : null;
        if (product == null || !product.Published || product.Deleted)
        {
            return NotFound();
        }

        var customer = Services.WorkContext.CurrentCustomer;
        var storeId = Services.StoreContext.CurrentStore.Id;
        var cart = await _cartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);

        if (!cart.Items.Any(x => x.Item.ProductId == product.Id))
        {
            var ctx = new AddToCartContext
            {
                Customer = customer,
                Product = product,
                CartType = ShoppingCartType.ShoppingCart,
                StoreId = storeId,
                Quantity = 1,
                AutomaticallyAddRequiredProducts = true
            };

            if (!await _cartService.AddToCartAsync(ctx))
            {
                NotifyError(string.Join(" ", ctx.Warnings));
                return RedirectToRoute(StudioStorefrontSetup.ToolsRouteName, new { goi = product.Id });
            }
        }

        return RedirectToRoute("Checkout");
    }
}
