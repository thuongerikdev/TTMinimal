using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Identity;
using Smartstore.Core.Widgets;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Lists the customer's addresses at the bottom of the customer info page, which replaces the separate
/// "Addresses" tab of the account menu (hidden in <see cref="AdminMenuEvents"/>). Registered for Customer/Info only, see Startup.
/// </summary>
public class CustomerInfoAddressesFilter : IAsyncActionFilter
{
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly Lazy<IAddressService> _addressService;
    private readonly IWorkContext _workContext;

    public CustomerInfoAddressesFilter(Lazy<IWidgetProvider> widgetProvider, Lazy<IAddressService> addressService, IWorkContext workContext)
    {
        _widgetProvider = widgetProvider;
        _addressService = addressService;
        _workContext = workContext;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var customer = _workContext.CurrentCustomer;
        if (customer.IsRegistered())
        {
            var model = new CustomerAddressListModel();
            foreach (var address in customer.Addresses)
            {
                model.Addresses.Add(new CustomerAddressItem
                {
                    Id = address.Id,
                    Name = $"{address.FirstName} {address.LastName}".Trim().NullEmpty() ?? address.Company,
                    Phone = address.PhoneNumber,
                    FormattedAddress = await _addressService.Value.FormatAddressAsync(address, true),
                    IsDefault = address.Id == customer.BillingAddressId || address.Id == customer.ShippingAddressId
                });
            }

            _widgetProvider.Value.RegisterWidget("customer_info_bottom",
                new PartialViewWidget("_Split3DCustomerAddresses", model, "Smartstore.Split3D"));
        }

        await next();
    }
}
