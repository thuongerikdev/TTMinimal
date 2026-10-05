using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Data;
using Smartstore.PayOS.Configuration;
using Smartstore.PayOS.Providers;
using Smartstore.PayOS.Services;
using Smartstore.Scheduling;

namespace Smartstore.PayOS.Tasks;

/// <summary>
/// Fallback for missed webhooks (server restart, webhook not confirmed, customer closed the PayOS page):
/// periodically asks the PayOS API about pending PayOS orders and marks paid ones as paid.
/// </summary>
public partial class PayOSSyncTask : ITask
{
    /// <summary>
    /// Orders older than this are not checked anymore. PayOS links expire long before.
    /// </summary>
    public static readonly TimeSpan MaxOrderAge = TimeSpan.FromDays(7);

    private readonly SmartDbContext _db;
    private readonly PayOSService _payOSService;

    public PayOSSyncTask(SmartDbContext db, PayOSService payOSService)
    {
        _db = db;
        _payOSService = payOSService;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    public async Task Run(TaskExecutionContext ctx, CancellationToken cancelToken = default)
    {
        var minDate = DateTime.UtcNow - MaxOrderAge;

        var orders = await _db.Orders
            .Where(x => x.PaymentMethodSystemName == PayOSProvider.SystemName
                && x.PaymentStatusId == (int)PaymentStatus.Pending
                && x.OrderStatusId != (int)OrderStatus.Cancelled
                && !x.Deleted
                && x.AuthorizationTransactionId != null
                && x.CreatedOnUtc >= minDate)
            .OrderBy(x => x.Id)
            .ToListAsync(cancelToken);

        if (orders.Count == 0)
        {
            return;
        }

        var settingsByStore = new Dictionary<int, PayOSSettings>();
        var numPaid = 0;

        foreach (var order in orders)
        {
            if (cancelToken.IsCancellationRequested)
            {
                break;
            }

            var orderCode = PayOSService.GetLastOrderCode(order);
            if (!orderCode.HasValue)
            {
                continue;
            }

            if (!settingsByStore.TryGetValue(order.StoreId, out var settings))
            {
                settings = await _payOSService.LoadSettingsAsync(order.StoreId);
                settingsByStore[order.StoreId] = settings;
            }

            if (!settings.HasCredentials())
            {
                continue;
            }

            try
            {
                await _payOSService.SyncPaymentStatusAsync(order, orderCode.Value, settings, "task");

                if (order.PaymentStatus == PaymentStatus.Paid)
                {
                    numPaid++;
                }
            }
            catch (Exception ex)
            {
                // One failing order must not block the others.
                Logger.Error(ex, "PayOS sync failed for order {0} (order code {1}).", order.Id, orderCode.Value);
            }
        }

        if (numPaid > 0)
        {
            Logger.Info("PayOS sync marked {0} of {1} pending orders as paid.", numPaid, orders.Count);
        }
    }
}
