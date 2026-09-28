using Smartstore.Core.Configuration;

namespace Smartstore.PayOS.Configuration;

public class PayOSSettings : ISettings
{
    /// <summary>
    /// The client ID of the PayOS payment channel.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// The API key of the PayOS payment channel.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// The checksum key used to sign requests and to verify webhook messages.
    /// </summary>
    public string ChecksumKey { get; set; }

    /// <summary>
    /// Prefix of the transfer description, followed by the order ID (e.g. "DH1234").
    /// </summary>
    public string DescriptionPrefix { get; set; } = "DH";

    /// <summary>
    /// Lifetime of a payment link in minutes. 0 means no expiry.
    /// </summary>
    public int PaymentLinkLifetimeMinutes { get; set; } = 30;

    /// <summary>
    /// Specifies the additional handling fee charged to the customer when using this method.
    /// </summary>
    public decimal AdditionalFee { get; set; }

    /// <summary>
    /// Specifies whether the additional fee should be a percentage value based on the current cart.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    public bool HasCredentials()
        => ClientId.HasValue() && ApiKey.HasValue() && ChecksumKey.HasValue();
}
