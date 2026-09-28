#nullable enable

using System.Globalization;

namespace Smartstore.Split3D.Services;

/// <summary>
/// A price tier: <see cref="PricePerGram"/> applies to a print job of at least <see cref="MinGrams"/>.
/// </summary>
public sealed record PrintPriceTier(int MinGrams, decimal PricePerGram)
{
    /// <summary>
    /// Upper bound (exclusive) of this tier, or <c>null</c> for the last tier.
    /// </summary>
    public int? MaxGrams { get; init; }

    /// <summary>
    /// Weight label as on the printed price list, e.g. "Dưới 1 kg" or "Từ 500 g".
    /// </summary>
    public string Label => MinGrams == 0 && MaxGrams.HasValue
        ? "Dưới " + PrintPriceList.FormatWeight(MaxGrams.Value)
        : "Từ " + PrintPriceList.FormatWeight(MinGrams);
}

/// <summary>
/// A printing technology with its materials and price tiers (ascending by weight).
/// </summary>
public sealed class PrintTechnology
{
    public required string Name { get; init; }
    public required string Materials { get; init; }
    public List<PrintPriceTier> Tiers { get; } = [];

    /// <summary>
    /// A URL/HTML friendly key, e.g. "resin-like-abs".
    /// </summary>
    public string Key => Name.ToLowerInvariant().Replace(' ', '-');

    public decimal LowestPrice => Tiers.Count > 0 ? Tiers.Min(x => x.PricePerGram) : 0;

    public decimal HighestPrice => Tiers.Count > 0 ? Tiers.Max(x => x.PricePerGram) : 0;

    /// <summary>
    /// Short customer-facing description, derived from the technology name.
    /// </summary>
    public string Description
    {
        get
        {
            if (Name.Contains("FDM", StringComparison.OrdinalIgnoreCase))
            {
                return "In sợi nhựa bền, giá tốt. Hợp mô hình lớn, chi tiết kỹ thuật, đồ gia dụng và vỏ hộp.";
            }

            if (Name.Contains("ABS", StringComparison.OrdinalIgnoreCase))
            {
                return "Resin dẻo dai, chịu va đập tốt hơn. Hợp khớp lắp ráp, chi tiết mỏng cần độ bền.";
            }

            if (Name.Contains("Resin", StringComparison.OrdinalIgnoreCase))
            {
                return "Bề mặt mịn, chi tiết sắc nét. Hợp figure, mô hình nhỏ, mẫu trang sức.";
            }

            return Materials;
        }
    }

    /// <summary>
    /// Gets the tier that applies to a job of <paramref name="grams"/>.
    /// </summary>
    public PrintPriceTier? GetTier(decimal grams)
        => Tiers.LastOrDefault(x => x.MinGrams <= grams) ?? Tiers.FirstOrDefault();
}

/// <summary>
/// Parses the 3D printing price list stored in <see cref="StudioSettings.PrintPriceTable"/>.
/// One tier per line: <c>Technology | Materials | From grams | Price per gram</c>. Empty lines and lines starting with <c>#</c> are ignored.
/// </summary>
public static class PrintPriceList
{
    private static readonly CultureInfo _vi = CultureInfo.GetCultureInfo("vi-VN");

    public static List<PrintTechnology> Parse(string? table)
    {
        var result = new List<PrintTechnology>();

        foreach (var rawLine in (table.NullEmpty() ?? StudioSettings.DefaultPrintPriceTable).ReadLines(true, true))
        {
            if (rawLine.StartsWith('#'))
            {
                continue;
            }

            var parts = rawLine.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 4
                || parts[0].IsEmpty()
                || !int.TryParse(parts[2].Replace(".", string.Empty).Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minGrams)
                || !decimal.TryParse(parts[3].Replace(".", string.Empty).Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                continue;
            }

            var technology = result.FirstOrDefault(x => x.Name.EqualsNoCase(parts[0]));
            if (technology == null)
            {
                technology = new PrintTechnology { Name = parts[0], Materials = parts[1] };
                result.Add(technology);
            }

            technology.Tiers.Add(new PrintPriceTier(Math.Max(minGrams, 0), price));
        }

        foreach (var technology in result)
        {
            var tiers = technology.Tiers.OrderBy(x => x.MinGrams).ToList();
            technology.Tiers.Clear();

            for (var i = 0; i < tiers.Count; i++)
            {
                technology.Tiers.Add(tiers[i] with { MaxGrams = i + 1 < tiers.Count ? tiers[i + 1].MinGrams : null });
            }
        }

        return result;
    }

    public static string FormatWeight(int grams)
        => grams >= 1000
            ? (grams / 1000m).ToString("0.##", _vi) + " kg"
            : grams.ToString("#,##0", _vi) + " g";

    public static string FormatPrice(decimal price)
        => price.ToString("#,##0", _vi) + "đ";
}
