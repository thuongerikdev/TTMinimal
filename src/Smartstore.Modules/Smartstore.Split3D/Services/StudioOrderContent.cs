#nullable enable

using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Services;

/// <summary>
/// What an order contains. Each kind has its own fulfilment: keys are issued automatically,
/// print jobs are confirmed by the studio, goods are prepared and shipped (or picked up).
/// </summary>
[Flags]
public enum StudioOrderContent
{
    None = 0,

    /// <summary>
    /// Addon keys (license plan products) or key upgrades.
    /// </summary>
    Keys = 1,

    /// <summary>
    /// 3D print jobs (hidden product <see cref="PrintOrderService.PrintProductSku"/>).
    /// </summary>
    Print = 2,

    /// <summary>
    /// Any other product: printed products, custom name plates, pots, ...
    /// </summary>
    Goods = 4
}

/// <summary>
/// Sorts the items of an order into <see cref="StudioOrderContent"/>, so that key-specific steps, texts and emails
/// are only used for orders that actually contain keys.
/// </summary>
public class StudioOrderClassifier
{
    private readonly SmartDbContext _db;
    private readonly Split3DLicenseService _licenseService;

    public StudioOrderClassifier(SmartDbContext db, Split3DLicenseService licenseService)
    {
        _db = db;
        _licenseService = licenseService;
    }

    /// <summary>
    /// Gets the content of an order.
    /// </summary>
    public async Task<StudioOrderContent> GetContentAsync(int orderId, CancellationToken cancelToken = default)
    {
        var productIds = await _db.OrderItems
            .Where(x => x.OrderId == orderId)
            .Select(x => x.ProductId)
            .ToListAsync(cancelToken);

        return await ClassifyAsync(productIds, cancelToken);
    }

    /// <summary>
    /// Gets the items of an order that are <see cref="StudioOrderContent.Goods"/>: physical products the studio
    /// prepares and hands over, i.e. neither keys nor print jobs. Products are included.
    /// </summary>
    public async Task<List<OrderItem>> GetGoodsItemsAsync(int orderId, CancellationToken cancelToken = default)
    {
        var items = await _db.OrderItems
            .Include(x => x.Product)
            .Where(x => x.OrderId == orderId)
            .ToListAsync(cancelToken);

        if (items.Count == 0)
        {
            return items;
        }

        var keyProductIds = (await _licenseService.GetProductPlansAsync(cancelToken)).Keys.ToHashSet();

        return items
            .Where(x => !keyProductIds.Contains(x.ProductId)
                && !(x.Product?.Sku).EqualsNoCase(Split3DUpgradeService.UpgradeProductSku)
                && !(x.Product?.Sku).EqualsNoCase(PrintOrderService.PrintProductSku))
            .ToList();
    }

    /// <summary>
    /// Gets the content of a list of products (e.g. the items of an order).
    /// </summary>
    public async Task<StudioOrderContent> ClassifyAsync(IEnumerable<int> productIds, CancellationToken cancelToken = default)
    {
        Guard.NotNull(productIds);

        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return StudioOrderContent.None;
        }

        var keyProductIds = (await _licenseService.GetProductPlansAsync(cancelToken)).Keys.ToHashSet();
        var skus = await _db.Products
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Sku })
            .ToDictionaryAsync(x => x.Id, x => x.Sku, cancelToken);

        var result = StudioOrderContent.None;
        foreach (var id in ids)
        {
            var sku = skus.GetValueOrDefault(id);
            if (keyProductIds.Contains(id) || sku.EqualsNoCase(Split3DUpgradeService.UpgradeProductSku))
            {
                result |= StudioOrderContent.Keys;
            }
            else if (sku.EqualsNoCase(PrintOrderService.PrintProductSku))
            {
                result |= StudioOrderContent.Print;
            }
            else
            {
                result |= StudioOrderContent.Goods;
            }
        }

        return result;
    }
}
