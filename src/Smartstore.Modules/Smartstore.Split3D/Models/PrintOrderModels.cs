#nullable enable

namespace Smartstore.Split3D.Models;

/// <summary>
/// One weighed model of a print job, as shown in the order summary.
/// </summary>
public class PrintOrderModelLine
{
    public string Name { get; set; } = string.Empty;
    public int Grams { get; set; }
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? Fill { get; set; }
    public bool Manual { get; set; }

    public int TotalGrams => Grams * Math.Max(Quantity, 1);
}

/// <summary>
/// The print job the customer is about to pay for.
/// </summary>
public class PrintOrderSummaryModel
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Technology { get; set; }
    public string? Material { get; set; }
    public string? Fill { get; set; }
    public int TotalGrams { get; set; }
    public int Pieces { get; set; }
    public int ModelCount { get; set; }
    public decimal PricePerGram { get; set; }
    public string? TierLabel { get; set; }
    public decimal PriceEstimate { get; set; }
    public int DepositPercent { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal Outstanding { get; set; }
    public string? FileName { get; set; }
    public string? FileSize { get; set; }
    public bool FromQuote { get; set; }
    public List<PrintOrderModelLine> Models { get; set; } = [];
}

/// <summary>
/// Delivery details of a print job. The checkout of this shop asks for no address, so this form is the
/// only place the customer enters one.
/// </summary>
[LocalizedDisplay("Plugins.Split3D.PrintOrder.Fields.")]
public class PrintOrderFormModel : ModelBase
{
    public int Id { get; set; }

    [LocalizedDisplay("*RecipientName")]
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [LocalizedDisplay("*Phone")]
    [Required, StringLength(50)]
    public string Phone { get; set; } = string.Empty;

    [LocalizedDisplay("*Email")]
    [StringLength(255), EmailAddress]
    public string? Email { get; set; }

    [LocalizedDisplay("*DeliveryMethod")]
    public int DeliveryMethodId { get; set; } = (int)PrintDeliveryMethod.Shipping;

    [LocalizedDisplay("*AddressLine")]
    [StringLength(400)]
    public string? AddressLine { get; set; }

    [LocalizedDisplay("*City")]
    [StringLength(150)]
    public string? City { get; set; }

    [LocalizedDisplay("*DesiredOn")]
    public DateTime? DesiredOn { get; set; }

    [LocalizedDisplay("*Note")]
    public string? Note { get; set; }

    public bool IsPickup => DeliveryMethodId == (int)PrintDeliveryMethod.Pickup;
}

public class PrintOrderPageModel : ModelBase
{
    public StudioContactModel Contact { get; set; } = new();
    public PrintOrderSummaryModel Job { get; set; } = new();
    public PrintOrderFormModel Form { get; set; } = new();
    public bool AllowPickup { get; set; }
    public string? DepositNote { get; set; }
    public string? PriceNote { get; set; }

    /// <summary>
    /// Whether the customer still has to sign in before the checkout accepts the order.
    /// </summary>
    public bool RequiresLogin { get; set; }

    public string? LoginUrl { get; set; }
    public string? CalculatorUrl { get; set; }
}
