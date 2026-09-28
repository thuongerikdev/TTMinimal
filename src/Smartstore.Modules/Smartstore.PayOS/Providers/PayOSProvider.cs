using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Configuration;
using Smartstore.Core.Stores;
using Smartstore.Core.Widgets;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.PayOS.Configuration;
using Smartstore.PayOS.Controllers;
using Smartstore.PayOS.Services;

namespace Smartstore.PayOS.Providers;

/// <summary>
/// VietQR bank transfer via PayOS. The customer is redirected to the PayOS checkout page after the order
/// has been placed. The order is marked as paid once PayOS confirms the transfer (webhook or return page).
/// </summary>
[SystemName("Payments.PayOS")]
[FriendlyName("PayOS (VietQR)")]
[Order(1)]
[PaymentMethod(PaymentMethodType.Redirection)]
public class PayOSProvider : PaymentMethodBase, IConfigurable
{
    private readonly IStoreContext _storeContext;
    private readonly ISettingFactory _settingFactory;
    private readonly PayOSService _payOSService;

    public PayOSProvider(IStoreContext storeContext, ISettingFactory settingFactory, PayOSService payOSService)
    {
        _storeContext = storeContext;
        _settingFactory = settingFactory;
        _payOSService = payOSService;
    }

    public static string SystemName => "Payments.PayOS";

    public RouteInfo GetConfigurationRoute()
        => new(nameof(PayOSAdminController.Configure), "PayOSAdmin", new { area = "Admin" });

    public override Widget GetPaymentInfoWidget()
        => null;

    public override async Task<(decimal FixedFeeOrPercentage, bool UsePercentage)> GetPaymentFeeInfoAsync(ShoppingCart cart)
    {
        var settings = await _settingFactory.LoadSettingsAsync<PayOSSettings>(_storeContext.CurrentStore.Id);
        return (settings.AdditionalFee, settings.AdditionalFeePercentage);
    }

    public override Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        // Nothing is paid yet. The customer pays on the PayOS page after the order has been placed.
        return Task.FromResult(new ProcessPaymentResult
        {
            NewPaymentStatus = PaymentStatus.Pending
        });
    }

    public override async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        var order = postProcessPaymentRequest.Order;
        if (order.PaymentStatus != PaymentStatus.Pending)
        {
            return;
        }

        postProcessPaymentRequest.RedirectUrl = await _payOSService.CreatePaymentLinkAsync(order);
    }

    public override Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        Guard.NotNull(order);

        return Task.FromResult(
            !order.Deleted &&
            order.PaymentStatus == PaymentStatus.Pending &&
            order.OrderStatus != OrderStatus.Cancelled);
    }
}
