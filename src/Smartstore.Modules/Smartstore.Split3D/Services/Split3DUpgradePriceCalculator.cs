using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Prices the hidden key-upgrade product with the customer's pending upgrade (package price minus what the key cost).
/// Queries the database directly: depending on <see cref="Split3DUpgradeService"/> would create a cycle through
/// <see cref="IPriceCalculationService"/>.
/// </summary>
[CalculatorUsage(CalculatorTargets.Product, CalculatorOrdering.Early)]
public class Split3DUpgradePriceCalculator : IPriceCalculator
{
    private readonly SmartDbContext _db;

    public Split3DUpgradePriceCalculator(SmartDbContext db)
    {
        _db = db;
    }

    public async Task CalculateAsync(CalculatorContext context, CalculatorDelegate next)
    {
        if (context.Product?.Sku == Split3DUpgradeService.UpgradeProductSku && context.Options.Customer != null)
        {
            var customerId = context.Options.Customer.Id;
            var price = await _db.Split3DLicenseUpgrades()
                .Where(x => x.CustomerId == customerId && x.StatusId == (int)Split3DUpgradeStatus.Pending)
                .OrderByDescending(x => x.Id)
                .Select(x => (decimal?)x.Price)
                .FirstOrDefaultAsync();

            if (price.HasValue)
            {
                context.FinalPrice = price.Value;
                context.RegularPrice = price.Value;
            }
        }

        await next(context);
    }
}
