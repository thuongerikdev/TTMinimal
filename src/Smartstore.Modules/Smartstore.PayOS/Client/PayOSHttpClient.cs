using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Smartstore.PayOS.Configuration;

namespace Smartstore.PayOS.Client;

/// <summary>
/// Minimal client for the PayOS merchant API (https://payos.vn/docs/api/).
/// </summary>
public class PayOSHttpClient
{
    public const string BaseUrl = "https://api-merchant.payos.vn";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;

    public PayOSHttpClient(HttpClient client)
    {
        _client = client;
        _client.BaseAddress ??= new Uri(BaseUrl);
    }

    public Task<PayOSPaymentLink> CreatePaymentLinkAsync(PayOSSettings settings, PayOSCreatePaymentRequest request, CancellationToken cancelToken = default)
    {
        Guard.NotNull(request);

        request.Signature = PayOSSignature.ForPaymentRequest(request, settings.ChecksumKey);

        return SendAsync<PayOSPaymentLink>(settings, HttpMethod.Post, "v2/payment-requests", request, cancelToken);
    }

    public Task<PayOSPaymentInfo> GetPaymentInfoAsync(PayOSSettings settings, long orderCode, CancellationToken cancelToken = default)
        => SendAsync<PayOSPaymentInfo>(settings, HttpMethod.Get, $"v2/payment-requests/{orderCode}", null, cancelToken);

    public Task<PayOSPaymentInfo> CancelPaymentLinkAsync(PayOSSettings settings, long orderCode, string reason, CancellationToken cancelToken = default)
        => SendAsync<PayOSPaymentInfo>(settings, HttpMethod.Post, $"v2/payment-requests/{orderCode}/cancel", new { cancellationReason = reason }, cancelToken);

    /// <summary>
    /// Registers <paramref name="webhookUrl"/> for the payment channel. PayOS sends a test message to the URL
    /// and only accepts it if the endpoint responds with a success status code.
    /// </summary>
    public Task ConfirmWebhookAsync(PayOSSettings settings, string webhookUrl, CancellationToken cancelToken = default)
    {
        Guard.NotEmpty(webhookUrl);

        return SendAsync<JsonElement>(settings, HttpMethod.Post, "confirm-webhook", new { webhookUrl }, cancelToken);
    }

    private async Task<T> SendAsync<T>(PayOSSettings settings, HttpMethod method, string path, object body, CancellationToken cancelToken)
    {
        Guard.NotNull(settings);

        if (!settings.HasCredentials())
        {
            throw new PayOSException(null, "Client ID, API key and checksum key are required.");
        }

        using var message = new HttpRequestMessage(method, path);
        message.Headers.Add("x-client-id", settings.ClientId);
        message.Headers.Add("x-api-key", settings.ApiKey);

        if (body != null)
        {
            message.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        using var response = await _client.SendAsync(message, cancelToken);
        var json = await response.Content.ReadAsStringAsync(cancelToken);

        PayOSResponse<T> result = null;
        try
        {
            result = JsonSerializer.Deserialize<PayOSResponse<T>>(json, JsonOptions);
        }
        catch (JsonException)
        {
        }

        if (!response.IsSuccessStatusCode || result == null || result.Code != "00")
        {
            throw new PayOSException(result?.Code, result?.Desc ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return result.Data;
    }
}
