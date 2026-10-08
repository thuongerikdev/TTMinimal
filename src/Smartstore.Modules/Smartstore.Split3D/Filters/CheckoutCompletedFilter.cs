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
/// Shows the next steps (bank transfer instructions or an online payment button) and a link to the order
/// or to "My keys" on the order completed page (registered for Checkout/Completed only, see Startup).
/// </summary>
public class CheckoutCompletedFilter : IAsyncActionFilter
{
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly Lazy<SmartDbContext> _db;
    private readonly Lazy<IPaymentService> _paymentService;
    private readonly Lazy<BankQrService> _bankQrService;
    private readonly Lazy<StudioOrderClassifier> _orderClassifier;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;
    private readonly Split3DSettings _settings;

    public CheckoutCompletedFilter(
        Lazy<IWidgetProvider> widgetProvider,
        Lazy<SmartDbContext> db,
        Lazy<IPaymentService> paymentService,
        Lazy<BankQrService> bankQrService,
        Lazy<StudioOrderClassifier> orderClassifier,
        IWorkContext workContext,
        IStoreContext storeContext,
        Split3DSettings settings)
    {
        _widgetProvider = widgetProvider;
        _db = db;
        _paymentService = paymentService;
        _bankQrService = bankQrService;
        _orderClassifier = orderClassifier;
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

            // Each kind of content has its own next steps: keys are issued, print jobs confirmed, goods prepared and shipped.
            model.Content = await _orderClassifier.Value.GetContentAsync(order.Id);

            var jobs = await _db.Value.PrintOrders()
                .AsNoTracking()
                .Where(x => x.OrderId == order.Id && x.KindId == (int)PrintJobKind.File)
                .ToListAsync();

            if (jobs.Count > 0)
            {
                model.PrintJobCodes = jobs.Select(x => x.Code).Where(x => x.HasValue()).ToList();
                model.Outstanding = jobs.Sum(x => x.Outstanding);
            }
        }

        _widgetProvider.Value.RegisterWidget("checkout_completed_top",
            new PartialViewWidget("_Split3DCheckoutCompleted", model, "Smartstore.Split3D"));

        await next();
    }
}
