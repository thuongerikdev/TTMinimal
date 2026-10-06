#nullable enable

using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Prices a print job line with the deposit of the job its cart line refers to (<see cref="PrintOrder.Code"/> in the
/// line attributes). The amount never comes from the request, so a crafted add-to-cart cannot name its own price.
/// Queries the database directly: depending on <see cref="PrintOrderService"/> would create a cycle through
/// <see cref="IPriceCalculationService"/>.
/// </summary>
[CalculatorUsage(CalculatorTargets.Product, CalculatorOrdering.Early)]
public class PrintOrderPriceCalculator : IPriceCalculator
{
    private readonly SmartDbContext _db;

    public PrintOrderPriceCalculator(SmartDbContext db)
    {
        _db = db;
    }

    public async Task CalculateAsync(CalculatorContext context, CalculatorDelegate next)
    {
        if (context.Product?.Sku == PrintOrderService.PrintProductSku)
        {
            var code = PrintOrderService.ReadJobCode(context.CartItem?.Item?.RawAttributes);
            if (code != null)
            {
                var deposit = await _db.PrintOrders()
                    .Where(x => x.Code == code)
                    .Select(x => (decimal?)x.DepositAmount)
                    .FirstOrDefaultAsync();

                if (deposit.HasValue)
                {
                    context.FinalPrice = deposit.Value;
                    context.RegularPrice = deposit.Value;
                }
            }
        }

        await next(context);
    }
}
