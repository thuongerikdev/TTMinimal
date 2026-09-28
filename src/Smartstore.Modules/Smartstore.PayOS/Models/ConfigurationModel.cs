using System.ComponentModel.DataAnnotations;

namespace Smartstore.PayOS.Models;

[LocalizedDisplay("Plugins.Smartstore.PayOS.")]
public class ConfigurationModel : ModelBase
{
    [LocalizedDisplay("*ClientId")]
    public string ClientId { get; set; }

    [LocalizedDisplay("*ApiKey")]
    public string ApiKey { get; set; }

    [LocalizedDisplay("*ChecksumKey")]
    public string ChecksumKey { get; set; }

    [LocalizedDisplay("*WebhookUrl")]
    public string WebhookUrl { get; set; }

    [LocalizedDisplay("*DescriptionPrefix")]
    [RegularExpression("^[A-Za-z0-9]{0,6}$")]
    public string DescriptionPrefix { get; set; }

    [LocalizedDisplay("*PaymentLinkLifetimeMinutes")]
    [Range(0, 43200)]
    public int PaymentLinkLifetimeMinutes { get; set; }

    [LocalizedDisplay("*AdditionalFee")]
    public decimal AdditionalFee { get; set; }

    [LocalizedDisplay("*AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
}
