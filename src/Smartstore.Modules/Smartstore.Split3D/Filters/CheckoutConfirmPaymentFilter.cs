using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Stores;
using Smartstore.Core.Widgets;
using Smartstore.Engine.Modularity;
using Smartstore.Split3D.Models;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Shows the payment method choice on the confirm page, which replaces the separate payment page
/// (see <see cref="CheckoutPaymentFilter"/>). Registered for Checkout/Confirm GET only, see Startup.
/// </summary>
public class CheckoutConfirmPaymentFilter : IAsyncActionFilter
{
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IPaymentService _paymentService;
    private readonly IStoreContext _storeContext;
    private readonly ModuleManager _moduleManager;
    private readonly IWidgetProvider _widgetProvider;

    public CheckoutConfirmPaymentFilter(
        IShoppingCartService shoppingCartService,
        IPaymentService paymentService,
        IStoreContext storeContext,
        ModuleManager moduleManager,
        IWidgetProvider widgetProvider)
    {
        _shoppingCartService = shoppingCartService;
        _paymentService = paymentService;
        _storeContext = storeContext;
        _moduleManager = moduleManager;
        _widgetProvider = widgetProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cart = await _shoppingCartService.GetCartAsync(storeId: _storeContext.CurrentStore.Id);
        var providers = cart.HasItems
            ? await CheckoutPaymentFilter.GetQuickProvidersAsync(_paymentService, cart)
            : [];

        if (providers.Count > 0)
        {
            var selected = cart.Customer.GenericAttributes.SelectedPaymentMethod;
            var model = new CheckoutPaymentChoiceModel();

            // Names only: the full description (bank details) is shown once, on the completed page.
            foreach (var provider in providers)
            {
                var systemName = provider.Metadata.SystemName;
                model.Methods.Add(new()
                {
                    SystemName = systemName,
                    Name = _moduleManager.GetLocalizedFriendlyName(provider.Metadata),
                    Selected = systemName.EqualsNoCase(selected)
                });
            }

            if (!model.Methods.Any(x => x.Selected))
            {
                model.Methods[0].Selected = true;
            }

            _widgetProvider.RegisterWidget("checkout_confirm_before_summary",
                new PartialViewWidget("_StudioPaymentChoice", model, "Smartstore.Split3D"));
        }

        await next();
    }
}
