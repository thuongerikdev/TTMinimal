#nullable enable

using System.Globalization;
using System.Text.RegularExpressions;

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
/// A printing material (e.g. PLA) with its density and price tiers (ascending by weight).
/// </summary>
public sealed class PrintMaterial
{
    public required string Name { get; init; }

    /// <summary>
    /// Density in g/cm³, used to weigh uploaded models.
    /// </summary>
    public decimal Density { get; set; }

    public List<PrintPriceTier> Tiers { get; } = [];

    public decimal LowestPrice => Tiers.Count > 0 ? Tiers.Min(x => x.PricePerGram) : 0;

    /// <summary>
    /// Gets the tier that applies to a job of <paramref name="grams"/>.
    /// </summary>
    public PrintPriceTier? GetTier(decimal grams)
        => Tiers.LastOrDefault(x => x.MinGrams <= grams) ?? Tiers.FirstOrDefault();
}

/// <summary>
/// A printing technology (FDM, Resin) with its materials.
/// </summary>
public sealed class PrintTechnology
{
    public required string Name { get; init; }
    public List<PrintMaterial> Materials { get; } = [];

    /// <summary>
    /// A URL/HTML friendly key, e.g. "resin".
    /// </summary>
    public string Key => Name.ToLowerInvariant().Replace(' ', '-');

    /// <summary>
    /// Resin (SLA, DLP, MSLA) prints are solid; FDM prints have walls and infill.
    /// </summary>
    public bool IsResin
        => Name.Contains("Resin", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("SLA", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("DLP", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Material names, comma separated.
    /// </summary>
    public string MaterialNames => string.Join(", ", Materials.Select(x => x.Name));

    public decimal LowestPrice => Materials.Count > 0 ? Materials.Min(x => x.LowestPrice) : 0;

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

            if (IsResin)
            {
                return "Bề mặt mịn, chi tiết sắc nét. Hợp figure, mô hình nhỏ, mẫu trang sức.";
            }

            return MaterialNames;
        }
    }

    /// <summary>
    /// Finds the material mentioned in <paramref name="text"/> (e.g. "PLA trắng"), or the first material.
    /// </summary>
    public PrintMaterial? FindMaterial(string? text)
        => (text.HasValue()
            ? Materials
                .OrderByDescending(x => x.Name.Length)
                .FirstOrDefault(x => text!.Contains(x.Name, StringComparison.OrdinalIgnoreCase))
            : null)
        ?? Materials.FirstOrDefault();
}

/// <summary>
/// Parses the 3D printing price list stored in <see cref="StudioSettings.PrintPriceTable"/>.
/// One tier per line: <c>Technology | Materials | From grams | Price per gram</c>. Several materials sharing
/// the same prices are separated by commas; a density in g/cm³ may follow in parentheses, e.g. <c>PLA (1.24)</c>.
/// Empty lines and lines starting with <c>#</c> are ignored.
/// </summary>
public static partial class PrintPriceList
{
    private static readonly CultureInfo _vi = CultureInfo.GetCultureInfo("vi-VN");

    // Typical densities in g/cm³. Longer names first, so "PETG" wins over "PET".
    private static readonly (string Name, decimal Density)[] _densities =
    [
        ("PETG", 1.27m), ("PLA", 1.24m), ("ABS", 1.05m), ("ASA", 1.07m), ("TPU", 1.21m),
        ("NYLON", 1.14m), ("PA", 1.14m), ("PC", 1.20m), ("HIPS", 1.04m), ("PVA", 1.23m)
    ];

    [GeneratedRegex(@"^(?<name>[^(]+?)\s*(?:\((?<density>[\d.,]+)\s*\))?$")]
    private static partial Regex MaterialRegex();

    /// <summary>
    /// Returns the effective price table: the default one for an empty or unchanged legacy table.
    /// </summary>
    public static string Normalize(string? table)
    {
        if (table.IsEmpty())
        {
            return StudioSettings.DefaultPrintPriceTable;
        }

        return table!.Replace("\r", string.Empty).Trim() == StudioSettings.LegacyPrintPriceTable
            ? StudioSettings.DefaultPrintPriceTable
            : table;
    }

    public static List<PrintTechnology> Parse(string? table)
    {
        var result = new List<PrintTechnology>();

        foreach (var rawLine in Normalize(table).ReadLines(true, true))
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
                technology = new PrintTechnology { Name = parts[0] };
                result.Add(technology);
            }

            foreach (var (name, density) in ParseMaterials(parts[1]))
            {
                var material = technology.Materials.FirstOrDefault(x => x.Name.EqualsNoCase(name));
                if (material == null)
                {
                    material = new PrintMaterial { Name = name };
                    technology.Materials.Add(material);
                }

                if (density > 0)
                {
                    material.Density = density;
                }

                material.Tiers.Add(new PrintPriceTier(Math.Max(minGrams, 0), price));
            }
        }

        foreach (var technology in result)
        {
            foreach (var material in technology.Materials)
            {
                if (material.Density <= 0)
                {
                    material.Density = GuessDensity(material.Name, technology.IsResin);
                }

                var tiers = material.Tiers.OrderBy(x => x.MinGrams).ToList();
                material.Tiers.Clear();

                for (var i = 0; i < tiers.Count; i++)
                {
                    material.Tiers.Add(tiers[i] with { MaxGrams = i + 1 < tiers.Count ? tiers[i + 1].MinGrams : null });
                }
            }
        }

        return result;
    }

    private static IEnumerable<(string Name, decimal Density)> ParseMaterials(string column)
    {
        // Split at commas outside parentheses: "PLA (1,24), PETG" -> "PLA (1,24)", "PETG".
        var items = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < column.Length; i++)
        {
            if (column[i] == '(') depth++;
            else if (column[i] == ')') depth = Math.Max(0, depth - 1);
            else if (column[i] == ',' && depth == 0)
            {
                items.Add(column[start..i]);
                start = i + 1;
            }
        }
        items.Add(column[start..]);

        var any = false;
        foreach (var item in items)
        {
            var match = MaterialRegex().Match(item.Trim());
            if (!match.Success || match.Groups["name"].Value.IsEmpty())
            {
                continue;
            }

            decimal.TryParse(match.Groups["density"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var density);
            any = true;

            yield return (match.Groups["name"].Value.Trim(), density);
        }

        if (!any)
        {
            yield return ("Tiêu chuẩn", 0);
        }
    }

    private static decimal GuessDensity(string material, bool resin)
    {
        if (resin)
        {
            return 1.15m;
        }

        var words = material.ToUpperInvariant().Split([' ', '-', '+', '/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var (name, density) in _densities)
        {
            if (words.Contains(name) || material.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                return density;
            }
        }

        return 1.24m;
    }

    public static string FormatWeight(int grams)
        => grams >= 1000
            ? (grams / 1000m).ToString("0.##", _vi) + " kg"
            : grams.ToString("#,##0", _vi) + " g";

    public static string FormatPrice(decimal price)
        => price.ToString("#,##0", _vi) + "đ";
}
