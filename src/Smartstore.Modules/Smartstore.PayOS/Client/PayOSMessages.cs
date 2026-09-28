using System.Text.Json;
using System.Text.Json.Serialization;

namespace Smartstore.PayOS.Client;

/// <summary>
/// Envelope of every PayOS API response. <see cref="Code"/> "00" means success.
/// </summary>
public class PayOSResponse<T>
{
    public string Code { get; set; }
    public string Desc { get; set; }
    public T Data { get; set; }
    public string Signature { get; set; }
}

public class PayOSCreatePaymentRequest
{
    public long OrderCode { get; set; }
    public long Amount { get; set; }
    public string Description { get; set; }
    public string BuyerName { get; set; }
    public string BuyerEmail { get; set; }
    public string BuyerPhone { get; set; }
    public string CancelUrl { get; set; }
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Expiry as Unix timestamp in seconds.
    /// </summary>
    public long? ExpiredAt { get; set; }

    public string Signature { get; set; }
}

public class PayOSPaymentLink
{
    public string PaymentLinkId { get; set; }
    public long OrderCode { get; set; }
    public long Amount { get; set; }
    public string Status { get; set; }
    public string CheckoutUrl { get; set; }
    public string QrCode { get; set; }
}

public class PayOSPaymentInfo
{
    public string Id { get; set; }
    public long OrderCode { get; set; }
    public long Amount { get; set; }
    public long AmountPaid { get; set; }
    public long AmountRemaining { get; set; }

    /// <summary>
    /// PENDING, PROCESSING, PAID, CANCELLED or EXPIRED.
    /// </summary>
    public string Status { get; set; }

    public string CancellationReason { get; set; }
    public List<PayOSTransaction> Transactions { get; set; } = [];

    [JsonIgnore]
    public bool IsPaid => Status == PayOSStatus.Paid;
}

public class PayOSTransaction
{
    public string Reference { get; set; }
    public long Amount { get; set; }
    public string AccountNumber { get; set; }
    public string Description { get; set; }
    public string TransactionDateTime { get; set; }
    public string CounterAccountName { get; set; }
    public string CounterAccountNumber { get; set; }
}

/// <summary>
/// Body of a webhook message sent by PayOS. <see cref="Data"/> is kept raw because the signature
/// is computed over its exact key/value pairs.
/// </summary>
public class PayOSWebhookMessage
{
    public string Code { get; set; }
    public string Desc { get; set; }
    public bool Success { get; set; }
    public JsonElement Data { get; set; }
    public string Signature { get; set; }
}

public static class PayOSStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Paid = "PAID";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
}

public class PayOSException : Exception
{
    public PayOSException(string code, string message)
        : base(code.HasValue() ? $"PayOS error {code}: {message}" : $"PayOS error: {message}")
    {
        ErrorCode = code;
    }

    public string ErrorCode { get; }
}
