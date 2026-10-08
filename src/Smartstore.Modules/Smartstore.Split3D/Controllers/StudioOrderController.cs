#nullable enable

using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Checkout.Shipping;
using Smartstore.Core.Checkout.Tax;
using Smartstore.Core.Common;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Logging;
using Smartstore.Core.Security;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Places an order in the admin area for a customer who ordered by phone, Zalo or in the studio. Smartstore itself
/// only creates orders through the storefront checkout; this one writes the order directly, then hands over to the
/// core order page, where further products (also with attributes) can be added.
/// </summary>
public class StudioOrderController : AdminController
{
    private readonly SmartDbContext _db;
    private readonly ICustomerService _customerService;
    private readonly IPaymentService _paymentService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly ModuleManager _moduleManager;

    public StudioOrderController(
        SmartDbContext db,
        ICustomerService customerService,
        IPaymentService paymentService,
        IOrderProcessingService orderProcessingService,
        ModuleManager moduleManager)
    {
        _db = db;
        _customerService = customerService;
        _paymentService = paymentService;
        _orderProcessingService = orderProcessingService;
        _moduleManager = moduleManager;
    }

    [Permission(Permissions.Order.Create)]
    public async Task<IActionResult> Create()
    {
        var model = new StudioOrderCreateModel
        {
            PaymentMethod = Split3DStorefrontSetup.PrepaymentSystemName
        };

        await PrepareViewAsync();

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Order.Create)]
    public async Task<IActionResult> Create(StudioOrderCreateModel model)
    {
        var lines = (model.Lines ?? [])
            .Where(x => x.ProductId > 0 && x.Quantity > 0)
            .ToList();

        var productIds = lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id) && !x.Deleted)
            .ToDictionaryAsync(x => x.Id);

        lines = lines.Where(x => products.ContainsKey(x.ProductId)).ToList();
        if (lines.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Lines), T("Plugins.Split3D.OrderCreate.NoLines"));
        }

        if (!ModelState.IsValid)
        {
            model.Lines = lines;
            await PrepareViewAsync();
            return View(model);
        }

        var now = DateTime.UtcNow;
        var email = model.Email?.Trim().NullEmpty();
        var (customer, isNewCustomer) = await FindOrCreateCustomerAsync(email);
        var requiresShipping = lines.Any(x => products[x.ProductId].IsShippingEnabled);
        var countryId = await _db.Countries
            .Where(x => x.TwoLetterIsoCode == "VN")
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        var name = model.FullName!.Trim();
        var index = name.LastIndexOf(' ');

        Address CreateAddress() => new()
        {
            FirstName = index > 0 ? name[..index] : name,
            LastName = index > 0 ? name[(index + 1)..] : string.Empty,
            Email = email,
            PhoneNumber = model.Phone?.Trim().NullEmpty(),
            Address1 = model.AddressLine?.Trim().NullEmpty() ?? "Nhận tại xưởng",
            City = model.City?.Trim().NullEmpty(),
            CountryId = countryId,
            CreatedOnUtc = now
        };

        var order = new Order
        {
            OrderGuid = Guid.NewGuid(),
            StoreId = Services.StoreContext.CurrentStore.Id,
            CustomerId = customer.Id,
            BillingAddress = CreateAddress(),
            ShippingAddress = requiresShipping ? CreateAddress() : null,
            PaymentMethodSystemName = model.PaymentMethod.NullEmpty() ?? Split3DStorefrontSetup.PrepaymentSystemName,
            CustomerCurrencyCode = Services.CurrencyService.PrimaryCurrency.CurrencyCode,
            CurrencyRate = 1m,
            CustomerLanguageId = Services.WorkContext.WorkingLanguage.Id,
            CustomerTaxDisplayType = TaxDisplayType.IncludingTax,
            CustomerIp = string.Empty,
            CustomerOrderComment = model.Note?.Trim().NullEmpty(),
            OrderStatus = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            ShippingStatus = requiresShipping ? ShippingStatus.NotYetShipped : ShippingStatus.ShippingNotRequired,
            ShippingMethod = requiresShipping ? T("Plugins.Split3D.OrderCreate.ShippingMethod").Value : null,
            CreatedOnUtc = now,
            UpdatedOnUtc = now
        };

        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var quantity = Math.Clamp(line.Quantity, 1, 99999);
            var unitPrice = decimal.Round(line.UnitPrice >= 0 ? line.UnitPrice.Value : product.Price, 0);

            order.OrderItems.Add(new OrderItem
            {
                OrderItemGuid = Guid.NewGuid(),
                ProductId = product.Id,
                Sku = product.Sku,
                Quantity = quantity,
                UnitPriceInclTax = unitPrice,
                UnitPriceExclTax = unitPrice,
                PriceInclTax = unitPrice * quantity,
                PriceExclTax = unitPrice * quantity,
                ProductCost = product.ProductCost,
                DeliveryTimeId = product.DeliveryTimeId
            });
        }

        var subtotal = order.OrderItems.Sum(x => x.PriceInclTax);
        var shippingFee = decimal.Round(Math.Max(model.ShippingFee ?? 0, 0), 0);

        order.OrderSubtotalInclTax = order.OrderSubtotalExclTax = subtotal;
        order.OrderShippingInclTax = order.OrderShippingExclTax = shippingFee;
        order.OrderTotal = subtotal + shippingFee;

        _db.Orders.Add(order);
        ApplyCustomerData(customer, isNewCustomer, email, name, index, CreateAddress);
        await _db.SaveChangesAsync();

        var user = Services.WorkContext.CurrentCustomer;
        _db.OrderNotes.Add(order, T("Plugins.Split3D.OrderCreate.CreatedNote", user.GetFullName().NullEmpty() ?? user.Email ?? user.Username ?? "admin"));
        await _db.SaveChangesAsync();

        // Same stock handling as an order from the checkout.
        foreach (var item in order.OrderItems)
        {
            await _orderProcessingService.UpdateOrderDetailsAsync(item, new UpdateOrderDetailsContext
            {
                OldQuantity = 0,
                NewQuantity = item.Quantity,
                AdjustInventory = true
            });
        }

        if (model.IsPaid && order.CanMarkOrderAsPaid())
        {
            // Publishes OrderPaidEvent: key products get their license, print jobs are marked as paid.
            await _orderProcessingService.MarkOrderAsPaidAsync(order);
        }

        Services.ActivityLogger.LogActivity(KnownActivityLogTypes.EditOrder, T("ActivityLog.EditOrder"), order.GetOrderNumber());
        NotifySuccess(T("Plugins.Split3D.OrderCreate.Created", order.GetOrderNumber()));

        return RedirectToAction("Edit", "Order", new { id = order.Id, area = "Admin" });
    }

    /// <summary>
    /// The registered customer with this email, otherwise a new guest account that only carries the order.
    /// </summary>
    private async Task<(Customer Customer, bool IsNew)> FindOrCreateCustomerAsync(string? email)
    {
        if (email != null)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Email == email && !x.Deleted);
            if (customer != null)
            {
                return (customer, false);
            }
        }

        return (await _customerService.CreateGuestCustomerAsync(), true);
    }

    /// <summary>
    /// Gives the customer the name, email and address entered in the form, so that customer and order lists show who
    /// it is: a new account gets all of it, an existing one only what it lacks (data the customer entered is kept).
    /// </summary>
    private static void ApplyCustomerData(Customer customer, bool isNew, string? email, string name, int index, Func<Address> createAddress)
    {
        if (customer.FirstName.IsEmpty() && customer.LastName.IsEmpty() && name.HasValue())
        {
            // CustomerHook builds FullName from these on save.
            customer.FirstName = index > 0 ? name[..index] : name;
            customer.LastName = index > 0 ? name[(index + 1)..] : null;
        }

        // No other account uses the email, otherwise FindOrCreateCustomerAsync had returned it.
        if (isNew && email != null && customer.Email.IsEmpty())
        {
            customer.Email = email;
        }

        if (isNew || customer.BillingAddressId is null or 0)
        {
            var address = createAddress();
            customer.Addresses.Add(address);
            customer.BillingAddress = address;
        }
    }

    private async Task PrepareViewAsync()
    {
        var providers = await _paymentService.LoadAllPaymentProvidersAsync(false);

        ViewBag.PaymentMethods = providers
            .Select(x => (x.Metadata.SystemName, _moduleManager.GetLocalizedFriendlyName(x.Metadata).NullEmpty() ?? x.Metadata.SystemName))
            .OrderBy(x => x.Item2)
            .ToList();

        ViewBag.Products = await _db.Products
            .AsNoTracking()
            .Where(x => !x.Deleted && x.ProductTypeId != (int)ProductType.BundledProduct)
            .OrderBy(x => x.Name)
            .Take(2000)
            .Select(x => new StudioOrderProductOption(x.Id, x.Name, x.Sku, x.Price, x.ProductVariantAttributes.Any(a => a.IsRequired)))
            .ToListAsync();
    }
}
