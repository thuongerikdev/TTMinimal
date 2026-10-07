namespace Smartstore.Split3D.Models;

[LocalizedDisplay("Plugins.Split3D.PrintQuote.Fields.")]
public class PrintQuoteModel : EntityModelBase
{
    [LocalizedDisplay("*CreatedOn")]
    public DateTime CreatedOn { get; set; }

    [LocalizedDisplay("*Name")]
    public string Name { get; set; }

    [LocalizedDisplay("*Phone")]
    public string Phone { get; set; }

    [LocalizedDisplay("*Email")]
    public string Email { get; set; }

    [LocalizedDisplay("*Technology")]
    public string Technology { get; set; }

    [LocalizedDisplay("*Material")]
    public string Material { get; set; }

    [LocalizedDisplay("*Quantity")]
    public int Quantity { get; set; }

    [LocalizedDisplay("*EstimatedGrams")]
    public int? EstimatedGrams { get; set; }

    [LocalizedDisplay("*EstimatedPrice")]
    public string EstimatedPrice { get; set; }

    [LocalizedDisplay("*NeedsDesign")]
    public bool NeedsDesign { get; set; }

    [LocalizedDisplay("*Note")]
    public string Note { get; set; }

    [LocalizedDisplay("*FileLink")]
    public string FileLink { get; set; }

    [LocalizedDisplay("*File")]
    public string FileName { get; set; }

    public string FileSize { get; set; }
    public bool HasFile { get; set; }

    [LocalizedDisplay("*Status")]
    public int StatusId { get; set; }

    public string StatusName { get; set; }
    public string StatusBadge { get; set; }

    [LocalizedDisplay("*QuotedPrice")]
    public decimal? QuotedPrice { get; set; }

    [LocalizedDisplay("*AdminNote")]
    public string AdminNote { get; set; }

    public int CustomerId { get; set; }
    public string IpAddress { get; set; }
    public string EditUrl { get; set; }
    public string DownloadUrl { get; set; }

    [LocalizedDisplay("*ContactCount")]
    public int ContactCount { get; set; }

    [LocalizedDisplay("*LastContactOn")]
    public DateTime? LastContactOn { get; set; }

    [LocalizedDisplay("*FollowUpOn")]
    public DateTime? FollowUpOn { get; set; }

    /// <summary>
    /// The follow-up date has passed, so the request needs attention.
    /// </summary>
    public bool IsDue { get; set; }

    /// <summary>
    /// The print job created from this request, 0 if there is none.
    /// </summary>
    [LocalizedDisplay("*PrintOrder")]
    public int PrintOrderId { get; set; }

    public string PrintOrderCode { get; set; }
    public string PrintOrderUrl { get; set; }
    public string PrintOrderStatusName { get; set; }

    /// <summary>
    /// Payment link the customer uses to accept the quote.
    /// </summary>
    public string PayUrl { get; set; }

    /// <summary>
    /// Ready-made message for Zalo, SMS, Messenger or email, built from the studio template.
    /// </summary>
    public string Message { get; set; }

    public string TelUrl { get; set; }
    public string ZaloUrl { get; set; }
    public string SmsUrl { get; set; }
    public string MailUrl { get; set; }
    public string MessengerUrl { get; set; }

    public List<PrintQuoteContactModel> ContactLog { get; set; } = [];
}

/// <summary>
/// One entry of the contact log of a quote request.
/// </summary>
public class PrintQuoteContactModel : EntityModelBase
{
    public DateTime CreatedOn { get; set; }
    public int ChannelId { get; set; }
    public string ChannelName { get; set; }
    public string ChannelIcon { get; set; }
    public bool IsIncoming { get; set; }
    public string Message { get; set; }
    public string UserName { get; set; }
}

/// <summary>
/// What the studio enters when logging a contact by hand, or when turning a request into a print job.
/// </summary>
[LocalizedDisplay("Plugins.Split3D.PrintQuote.Fields.")]
public class PrintQuoteActionModel : ModelBase
{
    public int Id { get; set; }

    [LocalizedDisplay("*Channel")]
    public int ChannelId { get; set; }

    public bool IsIncoming { get; set; }

    [LocalizedDisplay("*ContactMessage")]
    public string Message { get; set; }

    /// <summary>
    /// Days until the next follow-up, 0 clears the date, null leaves it alone.
    /// </summary>
    public int? FollowUpDays { get; set; }

    [LocalizedDisplay("*QuotedPrice")]
    public decimal Price { get; set; }

    [LocalizedDisplay("*DepositPercent")]
    public int DepositPercent { get; set; }
}

public class PrintQuoteListModel : ModelBase
{
    [LocalizedDisplay("Plugins.Split3D.PrintQuote.SearchTerm")]
    public string SearchTerm { get; set; }

    [LocalizedDisplay("Plugins.Split3D.PrintQuote.Fields.Status")]
    public int? SearchStatusId { get; set; }

    public int NewCount { get; set; }
    public int OpenCount { get; set; }

    /// <summary>
    /// New requests plus those whose follow-up date has passed.
    /// </summary>
    public int DueCount { get; set; }
}

[LocalizedDisplay("Plugins.Split3D.Studio.Fields.")]
public class StudioConfigurationModel : ModelBase
{
    [LocalizedDisplay("*BrandName")]
    [Required]
    public string BrandName { get; set; }

    [LocalizedDisplay("*Tagline")]
    public string Tagline { get; set; }

    [LocalizedDisplay("*Address")]
    public string Address { get; set; }

    [LocalizedDisplay("*Phone1")]
    public string Phone1 { get; set; }

    [LocalizedDisplay("*Phone2")]
    public string Phone2 { get; set; }

    [LocalizedDisplay("*ZaloPhone")]
    public string ZaloPhone { get; set; }

    [LocalizedDisplay("*FacebookUrl")]
    public string FacebookUrl { get; set; }

    [LocalizedDisplay("*Email")]
    public string Email { get; set; }

    [LocalizedDisplay("*MapUrl")]
    public string MapUrl { get; set; }

    [LocalizedDisplay("*PrintPriceTable")]
    public string PrintPriceTable { get; set; }

    [LocalizedDisplay("*PrintPriceNote")]
    public string PrintPriceNote { get; set; }

    [LocalizedDisplay("*FdmWeightFactor")]
    [Range(10, 500)]
    public int FdmWeightFactor { get; set; }

    [LocalizedDisplay("*ResinWeightFactor")]
    [Range(10, 500)]
    public int ResinWeightFactor { get; set; }

    [LocalizedDisplay("*QuoteNotifyEmail")]
    public string QuoteNotifyEmail { get; set; }

    [LocalizedDisplay("*QuoteMaxFileSizeMb")]
    [Range(1, 500)]
    public int QuoteMaxFileSizeMb { get; set; }

    [LocalizedDisplay("*DepositPercent")]
    [Range(1, 100)]
    public int DepositPercent { get; set; }

    [LocalizedDisplay("*AllowPickup")]
    public bool AllowPickup { get; set; }

    [LocalizedDisplay("*DepositNote")]
    public string DepositNote { get; set; }

    [LocalizedDisplay("*NameplateMinLength")]
    [Range(1, 200)]
    public decimal NameplateMinLength { get; set; }

    [LocalizedDisplay("*NameplateMaxLength")]
    [Range(1, 200)]
    public decimal NameplateMaxLength { get; set; }

    [LocalizedDisplay("*NameplateBaseLength")]
    [Range(1, 200)]
    public decimal NameplateBaseLength { get; set; }

    [LocalizedDisplay("*NameplatePercentPerCm")]
    [Range(0, 100)]
    public decimal NameplatePercentPerCm { get; set; }

    [LocalizedDisplay("*QuoteMessageTemplate")]
    public string QuoteMessageTemplate { get; set; }

    public int LayoutVersion { get; set; }
    public int CurrentLayoutVersion { get; set; }
    public List<PrintTechnology> ParsedPrices { get; set; } = [];
}
