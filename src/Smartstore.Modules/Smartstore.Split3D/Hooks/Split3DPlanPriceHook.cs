using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Data;
using Smartstore.Data.Hooks;

namespace Smartstore.Split3D.Hooks;

/// <summary>
/// Refreshes the home page price table when the price or SKU of a plan product is edited in the catalog.
/// </summary>
internal class Split3DPlanPriceHook : AsyncDbSaveHook<SmartDbContext, Product>
{
    private static readonly HashSet<string> _planSkus = new(
        Split3DStorefrontContent.Plans.Select(x => x.Sku),
        StringComparer.OrdinalIgnoreCase);

    private readonly Lazy<Split3DStorefrontSetup> _setup;
    private bool _planChanged;

    public Split3DPlanPriceHook(Lazy<Split3DStorefrontSetup> setup)
    {
        _setup = setup;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    protected override Task<HookResult> OnUpdatingAsync(Product entity, IHookedEntity entry, CancellationToken cancelToken)
    {
        if (entity.Sku != null
            && _planSkus.Contains(entity.Sku)
            && (entry.IsPropertyModified(nameof(Product.Price)) || entry.IsPropertyModified(nameof(Product.Sku))))
        {
            _planChanged = true;
        }

        return Task.FromResult(HookResult.Ok);
    }

    public override async Task OnAfterSaveCompletedAsync(IEnumerable<IHookedEntity> entries, CancellationToken cancelToken)
    {
        if (!_planChanged)
        {
            return;
        }

        _planChanged = false;

        try
        {
            await _setup.Value.RefreshContentAsync(cancelToken);
        }
        catch (Exception ex)
        {
            // Never fail the product save because of the home page.
            Logger.Error(ex, "Split3D: home page refresh after a plan price change failed.");
        }
    }
}
