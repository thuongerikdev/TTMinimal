using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Data;
using Smartstore.Core.Stores;
using Smartstore.Core.Widgets;
using Smartstore.Split3D.Models;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Shows the next steps (bank transfer instructions or an online payment button) and a link to "My keys"
/// on the order completed page (registered for Checkout/Completed only, see Startup).
/// </summary>
public class CheckoutCompletedFilter : IAsyncActionFilter
{
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly Lazy<SmartDbContext> _db;
    private readonly Lazy<IPaymentService> _paymentService;
    private readonly Lazy<BankQrService> _bankQrService;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;
    private readonly Split3DSettings _settings;

    public CheckoutCompletedFilter(
        Lazy<IWidgetProvider> widgetProvider,
        Lazy<SmartDbContext> db,
        Lazy<IPaymentService> paymentService,
        Lazy<BankQrService> bankQrService,
        IWorkContext workContext,
        IStoreContext storeContext,
        Split3DSettings settings)
    {
        _widgetProvider = widgetProvider;
        _db = db;
        _paymentService = paymentService;
        _bankQrService = bankQrService;
        _workContext = workContext;
        _storeContext = storeContext;
        _settings = settings;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Same order as the one shown by CheckoutController.Completed.
        var order = await _db.Value.Orders
            .AsNoTracking()
            .ApplyStandardFilter(_workContext.CurrentCustomer.Id, _storeContext.CurrentStore.Id)
            .FirstOrDefaultAsync();

        var model = new CheckoutCompletedModel
        {
            Settings = _settings,
            IsBankTransfer = true
        };

        if (order != null)
        {
            model.OrderId = order.Id;
            model.IsPaid = order.PaymentStatus == PaymentStatus.Paid;
            model.IsBankTransfer = order.PaymentMethodSystemName.EqualsNoCase(Split3DStorefrontSetup.PrepaymentSystemName);
            model.CanPayOnline = !model.IsPaid && !model.IsBankTransfer && await _paymentService.Value.CanRePostProcessPaymentAsync(order);

            if (model.IsBankTransfer && !model.IsPaid)
            {
                model.BankQrSvg = _bankQrService.Value.GenerateSvg(_settings, order);
            }

            // Print jobs: the next steps are "we check your file and confirm", not "you get a key".
            var jobs = await _db.Value.PrintOrders()
                .AsNoTracking()
                .Where(x => x.OrderId == order.Id)
                .ToListAsync();

            if (jobs.Count > 0)
            {
                var printProductId = await _db.Value.Products
                    .Where(x => x.Sku == PrintOrderService.PrintProductSku && !x.Deleted)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync();
                var productIds = await _db.Value.OrderItems
                    .Where(x => x.OrderId == order.Id)
                    .Select(x => x.ProductId)
                    .ToListAsync();

                model.PrintJobCodes = jobs.Select(x => x.Code).Where(x => x.HasValue()).ToList();
                model.PrintOnly = productIds.Count > 0 && productIds.All(x => x == printProductId);
                model.Outstanding = jobs.Sum(x => x.Outstanding);
            }
        }

        _widgetProvider.Value.RegisterWidget("checkout_completed_top",
            new PartialViewWidget("_Split3DCheckoutCompleted", model, "Smartstore.Split3D"));

        await next();
    }
}
