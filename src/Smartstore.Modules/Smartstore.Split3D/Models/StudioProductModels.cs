#nullable enable

namespace Smartstore.Split3D.Models;

/// <summary>
/// The grouped product overview that replaces the flat admin product grid (see <c>StudioProductsController</c>).
/// </summary>
public class StudioProductListModel
{
    public List<StudioProductGroupModel> Groups { get; set; } = [];

    public int TotalCount { get; set; }

    public int PublishedCount { get; set; }

    public int HiddenCount { get; set; }

    /// <summary>
    /// Products with something to fix (no picture, no category, out of stock, no price).
    /// </summary>
    public int AttentionCount { get; set; }

    public bool CanEdit { get; set; }
}

/// <summary>
/// One section of the overview: a business line (tool keys), a shop category or a technical group.
/// </summary>
public class StudioProductGroupModel
{
    public string Key { get; set; } = default!;

    public string Title { get; set; } = default!;

    /// <summary>
    /// Small text above the title, e.g. the parent categories ("Bán hàng ›").
    /// </summary>
    public string? Eyebrow { get; set; }

    public string? Description { get; set; }

    public string Icon { get; set; } = "bi bi-box";

    /// <summary>
    /// Color of the group: mint, yellow, lilac, pink, sky, gray or warn.
    /// </summary>
    public string Tone { get; set; } = "mint";

    public bool Collapsed { get; set; }

    public List<StudioProductLinkModel> Links { get; set; } = [];

    public List<StudioProductRowModel> Products { get; set; } = [];
}

public class StudioProductLinkModel
{
    public string Text { get; set; } = default!;

    public string Url { get; set; } = default!;

    public string Icon { get; set; } = "bi bi-box-arrow-up-right";

    public bool External { get; set; }
}

public class StudioProductRowModel
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Sku { get; set; }

    public string? ThumbUrl { get; set; }

    public string PriceText { get; set; } = default!;

    public string StockText { get; set; } = default!;

    /// <summary>
    /// ok, low, out or none.
    /// </summary>
    public string StockTone { get; set; } = "none";

    public bool Published { get; set; }

    /// <summary>
    /// Product used automatically by the shop (print orders, key upgrades): no publish switch.
    /// </summary>
    public bool IsSystem { get; set; }

    public string EditUrl { get; set; } = default!;

    public string? ShopUrl { get; set; }

    public string UpdatedText { get; set; } = default!;

    public List<string> Badges { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Lower-cased name and SKU without diacritics, for the client-side search.
    /// </summary>
    public string SearchText { get; set; } = default!;
}
