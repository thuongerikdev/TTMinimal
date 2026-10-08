#nullable enable

namespace Smartstore.Split3D.Models;

/// <summary>
/// A print job in the admin area.
/// </summary>
[LocalizedDisplay("Plugins.Split3D.PrintJob.Fields.")]
public class PrintJobModel : EntityModelBase
{
    [LocalizedDisplay("*Code")]
    public string? Code { get; set; }

    [LocalizedDisplay("*CreatedOn")]
    public DateTime CreatedOn { get; set; }

    [LocalizedDisplay("*Status")]
    public int StatusId { get; set; }

    public string? StatusName { get; set; }
    public string? StatusBadge { get; set; }

    [LocalizedDisplay("*Kind")]
    public int KindId { get; set; }

    public string? KindName { get; set; }

    /// <summary>
    /// Whether the job carries the products of an ordinary order instead of a print from files.
    /// </summary>
    public bool IsGoods { get; set; }

    [LocalizedDisplay("*RecipientName")]
    public string? RecipientName { get; set; }

    [LocalizedDisplay("*Phone")]
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
    public int TotalGrams { get; set; }

    [LocalizedDisplay("*Pieces")]
    public int Pieces { get; set; }

    public int ModelCount { get; set; }

    [LocalizedDisplay("*PricePerGram")]
    public decimal PricePerGram { get; set; }

    [LocalizedDisplay("*PriceEstimate")]
    public decimal PriceEstimate { get; set; }

    [LocalizedDisplay("*DepositAmount")]
    public decimal DepositAmount { get; set; }

    public int DepositPercent { get; set; }

    [LocalizedDisplay("*FinalPrice")]
    public decimal? FinalPrice { get; set; }

    [LocalizedDisplay("*ShippingFee")]
    public decimal? ShippingFee { get; set; }

    [LocalizedDisplay("*Outstanding")]
    public decimal Outstanding { get; set; }

    [LocalizedDisplay("*DeliveryMethod")]
    public int DeliveryMethodId { get; set; }

    public string? DeliveryMethodName { get; set; }

    [LocalizedDisplay("*Address")]
    public string? Address { get; set; }

    [LocalizedDisplay("*DesiredOn")]
    public DateTime? DesiredOn { get; set; }

    [LocalizedDisplay("*Note")]
    public string? Note { get; set; }

    [LocalizedDisplay("*AdminNote")]
    public string? AdminNote { get; set; }

    [LocalizedDisplay("*Order")]
    public int OrderId { get; set; }

    public string? OrderNumber { get; set; }
    public string? OrderUrl { get; set; }
    public string? PaymentStatusName { get; set; }
    public bool IsPaid { get; set; }

    [LocalizedDisplay("*File")]
    public string? FileName { get; set; }

    public string? FileSize { get; set; }
    public bool HasFile { get; set; }
    public string? FileLink { get; set; }
    public string? DownloadUrl { get; set; }
    public string? EditUrl { get; set; }

    /// <summary>
    /// Payment link of a job created from a quote request, so the studio can send it to the customer.
    /// </summary>
    public string? PayUrl { get; set; }

    public int QuoteRequestId { get; set; }
    public string? QuoteUrl { get; set; }
    public int CustomerId { get; set; }

    public DateTime? PaidOn { get; set; }
    public DateTime? ConfirmedOn { get; set; }
    public DateTime? CompletedOn { get; set; }

    public List<PrintOrderModelLine> Models { get; set; } = [];
}

public class PrintJobListModel : ModelBase
{
    [LocalizedDisplay("Plugins.Split3D.PrintQuote.SearchTerm")]
    public string? SearchTerm { get; set; }

    [LocalizedDisplay("Plugins.Split3D.PrintJob.Fields.Status")]
    public int? SearchStatusId { get; set; }

    [LocalizedDisplay("Plugins.Split3D.PrintJob.Fields.Kind")]
    public int? SearchKindId { get; set; }

    /// <summary>
    /// Jobs that are paid and wait for the studio to confirm them.
    /// </summary>
    public int ToConfirmCount { get; set; }

    /// <summary>
    /// Jobs in production (confirmed, printing, ready).
    /// </summary>
    public int OpenCount { get; set; }
}
