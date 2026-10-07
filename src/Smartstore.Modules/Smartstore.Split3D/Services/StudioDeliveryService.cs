#nullable enable

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Shipping;
using Smartstore.Core.Common;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Delivery address and shipping method of the two-step checkout, entered on the confirm page
/// (see <see cref="StudioCheckoutFactory"/>). Stores them exactly where Smartstore's own address and shipping steps
/// would (customer shipping address, selected shipping option), so the order gets address, method and charge.
/// </summary>
public class StudioDeliveryService
{
    private readonly SmartDbContext _db;
    private readonly IShippingService _shippingService;
    private readonly StudioSettings _studioSettings;

    public StudioDeliveryService(SmartDbContext db, IShippingService shippingService, StudioSettings studioSettings)
    {
        _db = db;
        _shippingService = shippingService;
        _studioSettings = studioSettings;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// A shipping method where the customer picks the order up at the workshop, so no street address is needed.
    /// </summary>
    public static bool IsPickup(string? methodName)
        => methodName.HasValue()
            && (methodName!.Contains("Nhận tại", StringComparison.OrdinalIgnoreCase)
                || methodName.Contains("Pickup", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the shipping options available for the cart. Empty if no shipping provider is active.
    /// </summary>
    public async Task<List<ShippingOption>> GetOptionsAsync(ShoppingCart cart, Address? address)
    {
        Guard.NotNull(cart);

        try
        {
            var response = await _shippingService.GetShippingOptionsAsync(cart, address, null, cart.StoreId);
            return response.ShippingOptions.ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Studio checkout: shipping options could not be loaded.");
            return [];
        }
    }

    /// <summary>
    /// Gets a value indicating whether the cart has everything the order needs to be shipped.
    /// </summary>
    public static bool IsComplete(ShoppingCart cart)
        => !cart.IsShippingRequired
            || (cart.Customer.ShippingAddressId > 0 && cart.Customer.GenericAttributes.SelectedShippingOption != null);

    public async Task<CheckoutDeliveryModel> PrepareModelAsync(ShoppingCart cart)
    {
        Guard.NotNull(cart);

        var customer = cart.Customer;
        await _db.LoadReferenceAsync(customer, x => x.ShippingAddress);

        // Prefill from the last delivery address, else from the billing address or the account.
        var source = customer.ShippingAddress ?? customer.BillingAddress;
        var selected = customer.GenericAttributes.SelectedShippingOption
            ?? customer.GenericAttributes.PreferredShippingOption;

        var model = new CheckoutDeliveryModel
        {
            FullName = source != null
                ? $"{source.FirstName} {source.LastName}".Trim()
                : customer.FullName,
            Phone = source?.PhoneNumber.NullEmpty() ?? customer.GenericAttributes.Phone,
            Address1 = source?.Address1,
            City = source?.City,
            IsComplete = IsComplete(cart),
            StudioAddress = _studioSettings.Address
        };

        if (IsPickup(model.Address1) || model.Address1 == PickupAddressLine)
        {
            model.Address1 = null;
            model.City = null;
        }

        foreach (var option in await GetOptionsAsync(cart, customer.ShippingAddress))
        {
            model.Methods.Add(new CheckoutDeliveryMethodModel
            {
                Id = option.ShippingMethodId,
                Name = option.Name,
                Description = option.Description,
                Rate = option.Rate,
                IsPickup = IsPickup(option.Name),
                Selected = option.ShippingMethodId == selected?.ShippingMethodId
            });
        }

        if (model.Methods.Count > 0 && !model.Methods.Any(x => x.Selected))
        {
            model.Methods[0].Selected = true;
        }

        return model;
    }

    /// <summary>
    /// Saves the delivery address as the customer's shipping address and selects the shipping method.
    /// </summary>
    /// <returns>An error message, or <c>null</c> on success.</returns>
    public async Task<string?> SaveAsync(ShoppingCart cart, CheckoutDeliveryModel input)
    {
        Guard.NotNull(cart);
        Guard.NotNull(input);

        var customer = cart.Customer;
        var options = await GetOptionsAsync(cart, customer.ShippingAddress);
        var option = options.FirstOrDefault(x => x.ShippingMethodId == input.ShippingMethodId);
        if (option == null)
        {
            return "Vui lòng chọn cách nhận hàng.";
        }

        var pickup = IsPickup(option.Name);
        var fullName = input.FullName?.Trim();
        var phone = input.Phone?.Trim();
        if (fullName.IsEmpty() || phone.IsEmpty())
        {
            return "Vui lòng nhập họ tên và số điện thoại người nhận.";
        }

        if (!pickup && (input.Address1.IsEmpty() || input.City.IsEmpty()))
        {
            return "Vui lòng nhập địa chỉ và tỉnh / thành phố giao hàng.";
        }

        // Vietnamese names: family and middle names first, the given name last ("Nguyễn Văn" + "An").
        var space = fullName!.LastIndexOf(' ');
        var firstName = space > 0 ? fullName[..space].Trim() : fullName;
        var lastName = space > 0 ? fullName[(space + 1)..].Trim() : string.Empty;

        var countryId = await _db.Countries
            .Where(x => x.TwoLetterIsoCode == "VN")
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        await _db.LoadCollectionAsync(customer, x => x.Addresses);
        await _db.LoadReferenceAsync(customer, x => x.ShippingAddress);

        // Edit the current delivery address in place: orders keep their own copy, so old orders are not affected.
        var address = customer.ShippingAddress;
        if (address == null)
        {
            address = new Address { CreatedOnUtc = DateTime.UtcNow };
            customer.Addresses.Add(address);
        }

        address.FirstName = firstName;
        address.LastName = lastName;
        address.PhoneNumber = phone;
        address.Email = customer.Email;
        address.Address1 = pickup ? PickupAddressLine : input.Address1!.Trim();
        address.City = pickup ? null : input.City!.Trim();
        address.CountryId = countryId ?? address.CountryId;

        await _db.SaveChangesAsync();

        // Saved separately: changing the shipping address resets the selected shipping option (CustomerHook).
        customer.ShippingAddress = address;
        await _db.SaveChangesAsync();

        // Rates may depend on the address: take the option computed for the saved address.
        option = (await GetOptionsAsync(cart, address)).FirstOrDefault(x => x.ShippingMethodId == option.ShippingMethodId) ?? option;

        var ga = customer.GenericAttributes;
        ga.SelectedShippingOption = option;
        ga.PreferredShippingOption = option;
        ga.DefaultShippingAddressId = address.Id;
        await ga.SaveChangesAsync();

        return null;
    }

    /// <summary>
    /// Street line of the shipping address when the order is picked up at the workshop.
    /// </summary>
    public const string PickupAddressLine = "Nhận tại xưởng";
}
