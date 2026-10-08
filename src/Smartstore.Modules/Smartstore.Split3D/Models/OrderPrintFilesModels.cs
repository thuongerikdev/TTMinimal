#nullable enable

namespace Smartstore.Split3D.Models;

/// <summary>
/// The "File in 3D" card of the admin order page (see <see cref="Components.OrderPrintFilesViewComponent"/>).
/// </summary>
public class OrderPrintFilesModel
{
    public string OrderNumber { get; set; } = string.Empty;

    public List<OrderPrintFileItem> Items { get; } = [];

    /// <summary>Order number, shop fonts and the design specs for studio-export.js.</summary>
    public string ConfigJson { get; set; } = "{}";
}

public class OrderPrintFileItem
{
    /// <summary>Position among the lines of the card (1-based), also used in the file names.</summary>
    public int No { get; set; }

    public string? ProductName { get; set; }

    /// <summary>Main text of the design (name on the plate).</summary>
    public string? Text { get; set; }

    public int Quantity { get; set; }

    /// <summary>Design code as shown in the cart line, e.g. "TT3D-ABCDE23456"; null for lines ordered without one.</summary>
    public string? Code { get; set; }

    /// <summary>False when the line has no code or its design was not found.</summary>
    public bool HasSpec { get; set; }

    public string? Kind { get; set; }
}
