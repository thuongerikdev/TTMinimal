#nullable enable

namespace Smartstore.Split3D.Models;

/// <summary>
/// A print or design request the studio enters itself, e.g. after a phone call or a Zalo chat.
/// </summary>
[LocalizedDisplay("Plugins.Split3D.PrintQuote.Fields.")]
public class PrintQuoteCreateModel : ModelBase
{
    [LocalizedDisplay("*Name")]
    [Required]
    public string? Name { get; set; }

    [LocalizedDisplay("*Phone")]
    [Required]
    public string? Phone { get; set; }

    [LocalizedDisplay("*Email")]
    public string? Email { get; set; }

    /// <summary>
    /// Design request (page "Thiết kế") instead of a print request.
    /// </summary>
    public bool IsDesign { get; set; }

    [LocalizedDisplay("*Technology")]
    public string? Technology { get; set; }

    [LocalizedDisplay("*Material")]
    public string? Material { get; set; }

    [LocalizedDisplay("*Quantity")]
    [Range(1, 9999)]
    public int Quantity { get; set; } = 1;

    [LocalizedDisplay("*EstimatedGrams")]
    [Range(0, 1_000_000)]
    public int? EstimatedGrams { get; set; }

    [LocalizedDisplay("*NeedsDesign")]
    public bool NeedsDesign { get; set; }

    [LocalizedDisplay("*Note")]
    public string? Note { get; set; }

    [LocalizedDisplay("*FileLink")]
    public string? FileLink { get; set; }

    [LocalizedDisplay("*Status")]
    public int StatusId { get; set; }

    [LocalizedDisplay("*QuotedPrice")]
    public decimal? QuotedPrice { get; set; }

    [LocalizedDisplay("*AdminNote")]
    public string? AdminNote { get; set; }
}

/// <summary>
/// A print job the studio enters itself. Priced from the price list unless a price is given.
/// </summary>
[LocalizedDisplay("Plugins.Split3D.PrintJob.Fields.")]
public class PrintJobCreateModel : ModelBase
{
    [LocalizedDisplay("*RecipientName")]
    [Required]
    public string? RecipientName { get; set; }

    [LocalizedDisplay("*Phone")]
    [Required]
    public string? Phone { get; set; }

    [LocalizedDisplay("*Email")]
    public string? Email { get; set; }

    [LocalizedDisplay("*Technology")]
    public string? Technology { get; set; }

    [LocalizedDisplay("*Material")]
    public string? Material { get; set; }

    [LocalizedDisplay("*Fill")]
    public string? Fill { get; set; }

    [LocalizedDisplay("*TotalGrams")]
    [Range(0, 1_000_000)]
    public int TotalGrams { get; set; }

    [LocalizedDisplay("*Pieces")]
    [Range(1, 9999)]
    public int Pieces { get; set; } = 1;

    [LocalizedDisplay("*FinalPrice")]
    [Range(0, 1_000_000_000)]
    public decimal? Price { get; set; }

    [LocalizedDisplay("*DepositPercent")]
    [Range(1, 100)]
    public int DepositPercent { get; set; }

    [LocalizedDisplay("*DeliveryMethod")]
    public int DeliveryMethodId { get; set; }

    [LocalizedDisplay("*AddressLine")]
    public string? AddressLine { get; set; }

    [LocalizedDisplay("*City")]
    public string? City { get; set; }

    [LocalizedDisplay("*DesiredOn")]
    public DateTime? DesiredOn { get; set; }

    [LocalizedDisplay("*FileLink")]
    public string? FileLink { get; set; }

    [LocalizedDisplay("*Note")]
    public string? Note { get; set; }

    [LocalizedDisplay("*AdminNote")]
    public string? AdminNote { get; set; }
}

/// <summary>
/// An order the studio places for a customer in the admin area (phone, Zalo, walk-in).
/// </summary>
[LocalizedDisplay("Plugins.Split3D.OrderCreate.Fields.")]
public class StudioOrderCreateModel : ModelBase
{
    [LocalizedDisplay("*FullName")]
    [Required]
    public string? FullName { get; set; }

    [LocalizedDisplay("*Phone")]
    public string? Phone { get; set; }

    [LocalizedDisplay("*Email")]
    [EmailAddress]
    public string? Email { get; set; }

    [LocalizedDisplay("*AddressLine")]
    public string? AddressLine { get; set; }

    [LocalizedDisplay("*City")]
    public string? City { get; set; }

    [LocalizedDisplay("*PaymentMethod")]
    public string? PaymentMethod { get; set; }

    [LocalizedDisplay("*IsPaid")]
    public bool IsPaid { get; set; }

    [LocalizedDisplay("*ShippingFee")]
    [Range(0, 1_000_000_000)]
    public decimal? ShippingFee { get; set; }

    [LocalizedDisplay("*Note")]
    public string? Note { get; set; }

    [LocalizedDisplay("*Lines")]
    public List<StudioOrderLineModel> Lines { get; set; } = [];
}

public class StudioOrderLineModel
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Unit price including tax; <c>null</c> takes the product price.
    /// </summary>
    public decimal? UnitPrice { get; set; }
}

/// <summary>
/// A product offered in the order line picker.
/// </summary>
public record StudioOrderProductOption(int Id, string Name, string? Sku, decimal Price, bool HasAttributes);
