using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Saves the delivery block of the confirm page (address and shipping method), see <see cref="StudioDeliveryService"/>.
/// </summary>
[Route("studio/checkout")]
public class StudioCheckoutController : PublicController
{
    private readonly IShoppingCartService _shoppingCartService;
    private readonly StudioDeliveryService _deliveryService;

    public StudioCheckoutController(IShoppingCartService shoppingCartService, StudioDeliveryService deliveryService)
    {
        _shoppingCartService = shoppingCartService;
        _deliveryService = deliveryService;
    }

    [HttpPost("delivery")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDelivery(CheckoutDeliveryModel model)
    {
        var customer = Services.WorkContext.CurrentCustomer;
        var cart = await _shoppingCartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, Services.StoreContext.CurrentStore.Id);

        if (!cart.HasItems)
        {
            return Json(new { success = false, message = T("ShoppingCart.CartIsEmpty").Value });
        }

        // Called by script: the block sits inside the confirm form, which cannot contain a form of its own.
        var error = await _deliveryService.SaveAsync(cart, model);

        return Json(new { success = error == null, message = error });
    }
}
