#nullable enable

using Smartstore.Core.Catalog.Pricing;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Prices a name plate by its length: the calculated price × <see cref="StudioCustomProducts.LengthFactor"/> of the
/// length in the <see cref="StudioCustomProducts.LengthAttributeName"/> attribute (base length when none is given).
/// The length is clamped to the range of <see cref="StudioSettings"/>, so a crafted request cannot buy a long plate
/// at a short price. Rounded to whole thousands.
/// </summary>
[CalculatorUsage(CalculatorTargets.Product, CalculatorOrdering.Late)]
public class NameplateLengthPriceCalculator : IPriceCalculator
{
    private readonly StudioSettings _settings;

    public NameplateLengthPriceCalculator(StudioSettings settings)
    {
        _settings = settings;
    }

    public async Task CalculateAsync(CalculatorContext context, CalculatorDelegate next)
    {
        await next(context);

        var product = context.Product;
        if (product?.Sku == null
            || !StudioCustomProducts.DesignProducts.TryGetValue(product.Sku, out var design)
            || !design.Length)
        {
            return;
        }

        var attributes = await context.Options.BatchContext.Attributes.GetOrLoadAsync(product.Id);
        var lengthAttribute = attributes.FirstOrDefault(x => x.ProductAttribute?.Name == StudioCustomProducts.LengthAttributeName);
        if (lengthAttribute == null)
        {
            return;
        }

        var raw = context.SelectedAttributes
            .Where(x => x.ProductId == product.Id)
            // GetAttributeValues returns null (not empty) when the attribute is not in the selection.
            .SelectMany(x => x.Selection?.GetAttributeValues(lengthAttribute.Id) ?? [])
            .Select(x => x?.ToString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var factor = StudioCustomProducts.LengthFactor(StudioCustomProducts.ParseLength(raw, design.Kind, _settings), design.Kind, _settings);
        if (factor == 1m)
        {
            return;
        }

        context.FinalPrice = Round(context.FinalPrice * factor);
        context.RegularPrice = Round(context.RegularPrice * factor);
    }

    private static decimal Round(decimal price)
        => price >= 1000m ? Math.Round(price / 1000m, MidpointRounding.AwayFromZero) * 1000m : Math.Round(price, 2);
}
