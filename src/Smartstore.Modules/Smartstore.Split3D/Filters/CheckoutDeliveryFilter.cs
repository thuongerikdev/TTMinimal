using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Logging;
using Smartstore.Core.Stores;
using Smartstore.Core.Widgets;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Delivery block on the confirm page of the two-step checkout, for carts with physical products
/// (see <see cref="StudioCheckoutFactory"/>): GET shows the address and shipping method form, POST (place order)
/// is sent back to the confirm page while they are missing. Registered for Checkout/Confirm only, see Startup.
/// </summary>
public class CheckoutDeliveryFilter : IAsyncActionFilter
{
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IStoreContext _storeContext;
    private readonly Lazy<StudioDeliveryService> _deliveryService;
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly Lazy<INotifier> _notifier;

    public CheckoutDeliveryFilter(
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        Lazy<StudioDeliveryService> deliveryService,
        Lazy<IWidgetProvider> widgetProvider,
        Lazy<INotifier> notifier)
    {
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _deliveryService = deliveryService;
        _widgetProvider = widgetProvider;
        _notifier = notifier;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cart = await _shoppingCartService.GetCartAsync(storeId: _storeContext.CurrentStore.Id);
        if (!cart.HasItems || !cart.IsShippingRequired)
        {
            await next();
            return;
        }

        if (HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            if (!StudioDeliveryService.IsComplete(cart))
            {
                _notifier.Value.Error("Vui lòng nhập thông tin nhận hàng và bấm \"Lưu thông tin nhận hàng\" trước khi đặt hàng.");
                context.Result = new RedirectResult(
                    ((SmartController)context.Controller).Url.Action("Confirm", "Checkout", new { area = string.Empty }) + "#studio-delivery");
                return;
            }

            await next();
            return;
        }

        var model = await _deliveryService.Value.PrepareModelAsync(cart);
        _widgetProvider.Value.RegisterWidget("checkout_confirm_before_summary",
            new PartialViewWidget("_StudioDelivery", model, "Smartstore.Split3D"));

        await next();
    }
}
