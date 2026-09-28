using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Smartstore.PayOS.Client;

/// <summary>
/// HMAC-SHA256 signatures as specified by PayOS: key/value pairs sorted by key, joined with '&amp;'.
/// </summary>
public static class PayOSSignature
{
    public static string ForPaymentRequest(PayOSCreatePaymentRequest request, string checksumKey)
    {
        Guard.NotNull(request);

        var data = $"amount={request.Amount}&cancelUrl={request.CancelUrl}&description={request.Description}&orderCode={request.OrderCode}&returnUrl={request.ReturnUrl}";
        return Compute(data, checksumKey);
    }

    public static string ForData(JsonElement data, string checksumKey)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Signed PayOS data must be a JSON object.", nameof(data));
        }

        var pairs = data.EnumerateObject()
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.Name + "=" + ToSignatureValue(x.Value));

        return Compute(string.Join('&', pairs), checksumKey);
    }

    public static bool Verify(JsonElement data, string signature, string checksumKey)
    {
        if (signature.IsEmpty() || checksumKey.IsEmpty() || data.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(ForData(data, checksumKey));
        var actual = Encoding.ASCII.GetBytes(signature.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string ToSignatureValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return string.Empty;
            case JsonValueKind.String:
                var str = value.GetString();
                // PayOS treats the literal strings "null" and "undefined" as empty values.
                return str is "null" or "undefined" ? string.Empty : str;
            default:
                return value.GetRawText();
        }
    }

    private static string Compute(string data, string checksumKey)
    {
        Guard.NotEmpty(checksumKey);

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(checksumKey), Encoding.UTF8.GetBytes(data));
        return Convert.ToHexStringLower(hash);
    }
}
