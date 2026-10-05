using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Stores;
using Smartstore.Engine.Modularity;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Two-step checkout (cart > confirm): the separate payment page is skipped. A payment method that needs no input
/// (bank transfer, PayOS redirect) is preselected and the customer can switch it on the confirm page
/// (see <see cref="CheckoutConfirmPaymentFilter"/>). Payment details are shown once, on the completed page.
/// Registered for Checkout/PaymentMethod GET only, see Startup.
/// </summary>
public class CheckoutPaymentFilter : IAsyncActionFilter
{
    // Same key as CheckoutController.ErrorMessageKey: a payment error must stay visible on the payment page.
    const string CheckoutErrorMessageKey = "CheckoutErrorMessage";

    private readonly IShoppingCartService _shoppingCartService;
    private readonly IPaymentService _paymentService;
    private readonly IStoreContext _storeContext;
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public CheckoutPaymentFilter(
        IShoppingCartService shoppingCartService,
        IPaymentService paymentService,
        IStoreContext storeContext,
        ITempDataDictionaryFactory tempDataFactory)
    {
        _shoppingCartService = shoppingCartService;
        _paymentService = paymentService;
        _storeContext = storeContext;
        _tempDataFactory = tempDataFactory;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tempData = _tempDataFactory.GetTempData(context.HttpContext);
        if (tempData.Peek(CheckoutErrorMessageKey) is string msg && msg.HasValue())
        {
            await next();
            return;
        }

        var cart = await _shoppingCartService.GetCartAsync(storeId: _storeContext.CurrentStore.Id);
        var providers = await GetQuickProvidersAsync(_paymentService, cart);
        if (!cart.HasItems || providers.Count == 0)
        {
            await next();
            return;
        }

        var ga = cart.Customer.GenericAttributes;
        var selected = FindProvider(providers, ga.SelectedPaymentMethod)
            ?? FindProvider(providers, ga.PreferredPaymentMethod)
            ?? providers[0];

        if (!selected.Metadata.SystemName.EqualsNoCase(ga.SelectedPaymentMethod))
        {
            ga.SelectedPaymentMethod = selected.Metadata.SystemName;
            await ga.SaveChangesAsync();
        }

        context.Result = new RedirectToActionResult("Confirm", "Checkout", new { area = string.Empty });
    }

    /// <summary>
    /// Gets the active payment methods that can be chosen without a payment page (no input, no selection page).
    /// </summary>
    internal static async Task<List<Provider<IPaymentMethod>>> GetQuickProvidersAsync(IPaymentService paymentService, ShoppingCart cart)
    {
        var providers = await paymentService.LoadActivePaymentProvidersAsync(cart, cart.StoreId, PaymentMethodType.Standard | PaymentMethodType.Redirection);

        if (cart.ContainsRecurringItem())
        {
            providers = providers.Where(x => x.Value.RecurringPaymentType > RecurringPaymentType.NotSupported);
        }

        return providers
            .Where(x => !x.Value.RequiresInteraction && !x.Value.RequiresPaymentSelection)
            .ToList();
    }

    internal static Provider<IPaymentMethod> FindProvider(List<Provider<IPaymentMethod>> providers, string systemName)
        => systemName.HasValue() ? providers.FirstOrDefault(x => x.Metadata.SystemName.EqualsNoCase(systemName)) : null;
}
