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
}

public class PrintQuoteListModel : ModelBase
{
    [LocalizedDisplay("Plugins.Split3D.PrintQuote.SearchTerm")]
    public string SearchTerm { get; set; }

    [LocalizedDisplay("Plugins.Split3D.PrintQuote.Fields.Status")]
    public int? SearchStatusId { get; set; }

    public int NewCount { get; set; }
    public int OpenCount { get; set; }
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

    public int LayoutVersion { get; set; }
    public int CurrentLayoutVersion { get; set; }
    public List<PrintTechnology> ParsedPrices { get; set; } = [];
}
