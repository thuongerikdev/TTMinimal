using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

public enum PrintQuoteStatus
{
    New = 0,
    Quoted = 10,
    Printing = 20,
    Completed = 30,
    Cancelled = 40
}

/// <summary>
/// A 3D printing quote request sent from the "In 3D" page, optionally with a model file.
/// </summary>
[Table("PrintQuoteRequest")]
[Index(nameof(StatusId))]
[Index(nameof(CreatedOnUtc))]
public class PrintQuoteRequest : BaseEntity
{
    public DateTime CreatedOnUtc { get; set; }

    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Id of the registered customer who sent the request, 0 for guests.
    /// </summary>
    public int CustomerId { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; }

    [Required, StringLength(50)]
    public string Phone { get; set; }

    [StringLength(255)]
    public string Email { get; set; }

    /// <summary>
    /// Printing technology as named in the price list, e.g. "FDM".
    /// </summary>
    [StringLength(100)]
    public string Technology { get; set; }

    /// <summary>
    /// Material, color and finish wishes.
    /// </summary>
    [StringLength(400)]
    public string Material { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Model weight estimated by the customer, in grams.
    /// </summary>
    public int? EstimatedGrams { get; set; }

    /// <summary>
    /// The customer also needs 3D design or file repair.
    /// </summary>
    public bool NeedsDesign { get; set; }

    [MaxLength]
    public string Note { get; set; }

    /// <summary>
    /// Link to a model file hosted elsewhere (Google Drive, ...), for files too large to upload.
    /// </summary>
    [StringLength(1000)]
    public string FileLink { get; set; }

    /// <summary>
    /// Original name of the uploaded model file.
    /// </summary>
    [StringLength(400)]
    public string FileName { get; set; }

    /// <summary>
    /// Path of the uploaded file relative to the tenant root.
    /// </summary>
    [StringLength(500)]
    public string FilePath { get; set; }

    public long FileSize { get; set; }

    public int StatusId { get; set; }

    [NotMapped]
    public PrintQuoteStatus Status
    {
        get => (PrintQuoteStatus)StatusId;
        set => StatusId = (int)value;
    }

    public decimal? QuotedPrice { get; set; }

    [MaxLength]
    public string AdminNote { get; set; }

    [StringLength(100)]
    public string IpAddress { get; set; }
}
