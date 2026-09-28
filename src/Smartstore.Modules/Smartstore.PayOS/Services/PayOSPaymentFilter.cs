using Smartstore.Core;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Configuration;
using Smartstore.PayOS.Configuration;
using Smartstore.PayOS.Providers;

namespace Smartstore.PayOS.Services;

/// <summary>
/// Hides PayOS if it has not been configured yet or if the customer does not pay in VND.
/// </summary>
public partial class PayOSPaymentFilter : IPaymentMethodFilter
{
    private readonly IWorkContext _workContext;
    private readonly Lazy<ISettingFactory> _settingFactory;

    public PayOSPaymentFilter(IWorkContext workContext, Lazy<ISettingFactory> settingFactory)
    {
        _workContext = workContext;
        _settingFactory = settingFactory;
    }

    public async Task<bool> IsExcludedAsync(PaymentFilterRequest request)
    {
        if (!request.PaymentProvider.Metadata.SystemName.EqualsNoCase(PayOSProvider.SystemName))
        {
            return false;
        }

        if (!_workContext.WorkingCurrency.CurrencyCode.EqualsNoCase("VND"))
        {
            return true;
        }

        var settings = await _settingFactory.Value.LoadSettingsAsync<PayOSSettings>(request.StoreId);
        return !settings.HasCredentials();
    }
}
