using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Where an order containing Split3D plans stands from the customer's point of view.
/// </summary>
public enum Split3DOrderState
{
    AwaitingPayment,
    AwaitingKey,
    Issued,
    Cancelled
}

public class Split3DOrderInfo
{
    public Order Order { get; init; }
    public List<OrderItem> Items { get; init; } = [];
    public List<Split3DLicense> Licenses { get; init; } = [];

    /// <summary>
    /// Addon names by addon id, for display.
    /// </summary>
    public Dictionary<int, string> AddonNames { get; init; } = [];

    /// <summary>
    /// Addon granted by each order item (by order item id).
    /// </summary>
    public Dictionary<int, Split3DAddon> ItemAddons { get; init; } = [];

    /// <summary>
    /// Number of keys the order is entitled to (sum of quantities of plan products).
    /// </summary>
    public int ExpectedKeys { get; init; }

    /// <summary>
    /// Key upgrades bought with the order (see <see cref="Split3DUpgradeService"/>).
    /// </summary>
    public List<Split3DLicenseUpgrade> Upgrades { get; init; } = [];

    /// <summary>
    /// Keys of <see cref="Upgrades"/> by license record id, for display.
    /// </summary>
    public Dictionary<int, Split3DLicense> UpgradedLicenses { get; init; } = [];

    public Split3DOrderState State { get; init; }
}

/// <summary>
/// Loads orders that contain Split3D plan products together with their issued keys.
/// </summary>
public class Split3DOrderQuery
{
    private readonly SmartDbContext _db;
    private readonly Split3DLicenseService _licenseService;

    public Split3DOrderQuery(SmartDbContext db, Split3DLicenseService licenseService)
    {
        _db = db;
        _licenseService = licenseService;
    }

    /// <param name="customerId">Restrict to one customer, or <c>null</c> for all.</param>
    /// <param name="openOnly">Only orders that still need action (awaiting payment or key).</param>
    public async Task<List<Split3DOrderInfo>> GetOrdersAsync(int? customerId, bool openOnly, int take = 200, CancellationToken cancelToken = default)
    {
        var plans = await _licenseService.GetProductPlansAsync(cancelToken);
        var upgradeProductId = await _db.Products
            .Where(x => x.Sku == Split3DUpgradeService.UpgradeProductSku && !x.Deleted)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancelToken);

        var productIds = plans.Keys.ToArray();
        var orderProductIds = upgradeProductId > 0 ? [.. productIds, upgradeProductId] : productIds;
        if (orderProductIds.Length == 0)
        {
            return [];
        }

        var query = _db.Orders
            .AsNoTracking()
            .Include(x => x.OrderItems)
                .ThenInclude(x => x.Product)
            .Where(x => !x.Deleted && x.OrderItems.Any(i => orderProductIds.Contains(i.ProductId)));

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (openOnly)
        {
            var cancelled = (int)OrderStatus.Cancelled;
            query = query.Where(x => x.OrderStatusId != cancelled);
        }

        var orders = await query
            .OrderByDescending(x => x.CreatedOnUtc)
            .Take(take)
            .ToListAsync(cancelToken);

        var orderIds = orders.Select(x => x.Id).ToArray();
        var licenses = orderIds.Length > 0
            ? await _db.Split3DLicenses().AsNoTracking().Where(x => orderIds.Contains(x.OrderId)).ToListAsync(cancelToken)
            : [];

        var upgrades = orderIds.Length > 0 && upgradeProductId > 0
            ? await _db.Split3DLicenseUpgrades().AsNoTracking()
                .Where(x => orderIds.Contains(x.OrderId) && x.StatusId != (int)Split3DUpgradeStatus.Cancelled)
                .ToListAsync(cancelToken)
            : [];
        var upgradedIds = upgrades.Select(x => x.Split3DLicenseId).Distinct().ToArray();
        var upgradedLicenses = upgradedIds.Length > 0
            ? await _db.Split3DLicenses().AsNoTracking().Where(x => upgradedIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancelToken)
            : [];

        var addonNames = plans.Values.Select(x => x.Addon).DistinctBy(x => x.Id).ToDictionary(x => x.Id, x => x.Name);
        var result = orders
            .Select(order =>
            {
                var items = order.OrderItems.Where(x => orderProductIds.Contains(x.ProductId)).ToList();
                var orderLicenses = licenses.Where(x => x.OrderId == order.Id).OrderBy(x => x.IssuedOnUtc).ToList();
                var orderUpgrades = upgrades.Where(x => x.OrderId == order.Id).ToList();
                var expected = items.Where(x => productIds.Contains(x.ProductId)).Sum(x => x.Quantity);
                var done = orderLicenses.Count + orderUpgrades.Count(x => x.Status == Split3DUpgradeStatus.Applied);

                return new Split3DOrderInfo
                {
                    AddonNames = addonNames,
                    ItemAddons = items.Where(x => plans.ContainsKey(x.ProductId)).ToDictionary(x => x.Id, x => plans[x.ProductId].Addon),
                    Order = order,
                    Items = items,
                    Licenses = orderLicenses,
                    ExpectedKeys = expected,
                    Upgrades = orderUpgrades,
                    UpgradedLicenses = upgradedLicenses,
                    State = GetState(order, done, expected + orderUpgrades.Count)
                };
            })
            .ToList();

        return openOnly
            ? result.Where(x => x.State is Split3DOrderState.AwaitingPayment or Split3DOrderState.AwaitingKey).ToList()
            : result;
    }

    /// <param name="issuedKeys">Issued keys plus applied upgrades.</param>
    /// <param name="expectedKeys">Keys plus upgrades the order is entitled to.</param>
    public static Split3DOrderState GetState(Order order, int issuedKeys, int expectedKeys)
    {
        if (order.OrderStatus == OrderStatus.Cancelled
            || order.PaymentStatus is PaymentStatus.Refunded or PaymentStatus.Voided)
        {
            return Split3DOrderState.Cancelled;
        }

        if (expectedKeys > 0 && issuedKeys >= expectedKeys)
        {
            return Split3DOrderState.Issued;
        }

        return order.PaymentStatus == PaymentStatus.Paid
            ? Split3DOrderState.AwaitingKey
            : Split3DOrderState.AwaitingPayment;
    }
}
