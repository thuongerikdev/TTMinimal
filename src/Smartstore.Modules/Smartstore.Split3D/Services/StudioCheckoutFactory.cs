using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Orders.Handlers;

namespace Smartstore.Split3D.Services;

/// <summary>
/// The two-step checkout (cart > confirm, "terminal with payment") has no address or shipping step, so Smartstore
/// would treat every cart as not shippable: no shipping address, method or charge on the order. The studio sells
/// physical products, so carts with shippable items still require shipping; the delivery block on the confirm page
/// (<see cref="Filters.CheckoutDeliveryFilter"/>) collects address and method instead of the separate steps.
/// Carts without shippable items (addon keys) are not affected.
/// </summary>
public class StudioCheckoutFactory : CheckoutFactory
{
    public StudioCheckoutFactory(
        IEnumerable<Lazy<ICheckoutHandler, CheckoutHandlerMetadata>> handlers,
        ShoppingCartSettings shoppingCartSettings)
        : base(handlers, shoppingCartSettings)
    {
    }

    public override CheckoutRequirements GetRequirements()
    {
        var requirements = base.GetRequirements();

        return requirements.HasFlag(CheckoutRequirements.Payment)
            ? requirements | CheckoutRequirements.Shipping
            : requirements;
    }
}
