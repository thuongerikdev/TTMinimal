using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Data;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// The "Công cụ 3D" page: every active tool with its packages (duration × devices) and a "buy now" button that puts
/// exactly one package into the cart and opens the checkout. Product pages of packages redirect here.
/// </summary>
[Route("cong-cu-3d")]
public class ToolsController : PublicController
{
    private readonly SmartDbContext _db;
    private readonly IShoppingCartService _cartService;
    private readonly StudioToolsCatalog _catalog;

    public ToolsController(SmartDbContext db, IShoppingCartService cartService, StudioToolsCatalog catalog)
    {
        _db = db;
        _cartService = cartService;
        _catalog = catalog;
    }

    [HttpGet("", Name = StudioStorefrontSetup.ToolsRouteName)]
    public async Task<IActionResult> Index(int? goi)
    {
        var model = new ToolsPageModel { SelectedProductId = goi };
        model.Tools.AddRange(await _catalog.GetToolsAsync());
        model.ComingSoon.AddRange(await _catalog.GetComingSoonAsync());

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
