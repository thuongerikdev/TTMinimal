using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Configuration;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Stores;
using Smartstore.PayOS.Client;
using Smartstore.PayOS.Configuration;
using Smartstore.PayOS.Providers;
using Smartstore.Threading;

namespace Smartstore.PayOS.Services;

public partial class PayOSService
{
    /// <summary>
    /// A PayOS order code must be unique per payment link, so every (re)payment attempt of an order
    /// gets its own code: <c>orderId * OrderCodeFactor + attempt</c>.
    /// </summary>
    public const long OrderCodeFactor = 1000;

    public const string ReturnPath = "payos/return";
    public const string CancelPath = "payos/cancel";
    public const string WebhookPath = "payos/webhook";

    private readonly SmartDbContext _db;
    private readonly ISettingFactory _settingFactory;
    private readonly IStoreContext _storeContext;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly PayOSHttpClient _client;

    public PayOSService(
        SmartDbContext db,
        ISettingFactory settingFactory,
        IStoreContext storeContext,
        IOrderProcessingService orderProcessingService,
        PayOSHttpClient client)
    {
        _db = db;
        _settingFactory = settingFactory;
        _storeContext = storeContext;
        _orderProcessingService = orderProcessingService;
        _client = client;
    }

    public Localizer T { get; set; } = NullLocalizer.Instance;
    public ILogger Logger { get; set; } = NullLogger.Instance;

    public static int GetOrderId(long orderCode)
        => (int)(orderCode / OrderCodeFactor);

    public static long? GetLastOrderCode(Order order)
        => long.TryParse(order.AuthorizationTransactionId, out var code) && GetOrderId(code) == order.Id ? code : null;

    /// <summary>
    /// Gets the amount to be paid in VND. PayOS only accepts integer VND amounts.
    /// </summary>
    public static long GetAmount(Order order)
    {
        if (!order.CustomerCurrencyCode.EqualsNoCase("VND"))
        {
            throw new PaymentException($"PayOS only supports VND. Order {order.Id} uses {order.CustomerCurrencyCode}.");
        }

        return (long)Math.Round(order.OrderTotal * order.CurrencyRate, 0, MidpointRounding.AwayFromZero);
    }

    public Task<PayOSSettings> LoadSettingsAsync(int storeId)
        => _settingFactory.LoadSettingsAsync<PayOSSettings>(storeId);

    public async Task<Order> FindOrderAsync(long orderCode)
    {
        var orderId = GetOrderId(orderCode);
        if (orderId <= 0)
        {
            return null;
        }

        var order = await _db.Orders.FindByIdAsync(orderId);
        return order != null && order.PaymentMethodSystemName == PayOSProvider.SystemName ? order : null;
    }

    /// <summary>
    /// Creates a new payment link for <paramref name="order"/>.
    /// </summary>
    /// <returns>The PayOS checkout URL, or <c>null</c> if the order turned out to be paid already.</returns>
    public async Task<string> CreatePaymentLinkAsync(Order order)
    {
        Guard.NotNull(order);

        var settings = await LoadSettingsAsync(order.StoreId);
        if (!settings.HasCredentials())
        {
            throw new PaymentException(T("Plugins.Smartstore.PayOS.NotConfigured"));
        }

        var lastOrderCode = GetLastOrderCode(order);
        if (lastOrderCode.HasValue)
        {
            // A previous link may already have been paid (webhook not received yet) or may still be open.
            // Never let the customer pay twice.
            if (await SyncPaymentStatusAsync(order, lastOrderCode.Value, settings, "repay") == PayOSStatus.Paid)
            {
                return null;
            }

            try
            {
                await _client.CancelPaymentLinkAsync(settings, lastOrderCode.Value, "Replaced by a new payment link");
            }
            catch (PayOSException ex)
            {
                // Expired or cancelled links cannot be cancelled again.
                Logger.Debug(ex, "Could not cancel previous PayOS link {0}.", lastOrderCode.Value);
            }
        }

        var attempt = lastOrderCode.HasValue ? lastOrderCode.Value % OrderCodeFactor + 1 : 1;
        if (attempt >= OrderCodeFactor)
        {
            throw new PaymentException($"Too many PayOS payment attempts for order {order.Id}.");
        }

        var orderCode = order.Id * OrderCodeFactor + attempt;
        var store = _storeContext.GetStoreById(order.StoreId) ?? _storeContext.CurrentStore;

        await _db.LoadReferenceAsync(order, x => x.BillingAddress);
        var address = order.BillingAddress;

        var request = new PayOSCreatePaymentRequest
        {
            OrderCode = orderCode,
            Amount = GetAmount(order),
            Description = CreateDescription(settings, order),
            BuyerName = address?.GetFullName().NullEmpty(),
            BuyerEmail = address?.Email.NullEmpty(),
            BuyerPhone = address?.PhoneNumber.NullEmpty(),
            ReturnUrl = store.GetAbsoluteUrl(ReturnPath),
            CancelUrl = store.GetAbsoluteUrl(CancelPath),
            ExpiredAt = settings.PaymentLinkLifetimeMinutes > 0
                ? DateTimeOffset.UtcNow.AddMinutes(settings.PaymentLinkLifetimeMinutes).ToUnixTimeSeconds()
                : null
        };

        PayOSPaymentLink link;
        try
        {
            link = await _client.CreatePaymentLinkAsync(settings, request);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create PayOS payment link for order {0}.", order.Id);
            _db.OrderNotes.Add(order, T("Plugins.Smartstore.PayOS.LinkCreationFailed", ex.Message));
            await _db.SaveChangesAsync();
            throw;
        }

        order.AuthorizationTransactionId = orderCode.ToString();
        order.AuthorizationTransactionCode = link.PaymentLinkId;
        order.AuthorizationTransactionResult = link.Status;

        _db.OrderNotes.Add(order, T("Plugins.Smartstore.PayOS.LinkCreated", orderCode, request.Amount.ToString("N0"), request.Description));
        await _db.SaveChangesAsync();

        return link.CheckoutUrl;
    }

    /// <summary>
    /// Fetches the authoritative payment status from the PayOS API and marks the order as paid if the full amount was received.
    /// Never trusts data sent by the client or by a webhook.
    /// </summary>
    /// <returns>The PayOS status of the payment link.</returns>
    public async Task<string> SyncPaymentStatusAsync(Order order, long orderCode, PayOSSettings settings, string source)
    {
        Guard.NotNull(order);

        if (GetOrderId(orderCode) != order.Id)
        {
            throw new ArgumentException($"PayOS order code {orderCode} does not belong to order {order.Id}.", nameof(orderCode));
        }

        var info = await _client.GetPaymentInfoAsync(settings, orderCode);
        if (!info.IsPaid)
        {
            return info.Status;
        }

        // The return page and the webhook usually arrive at the same time. Serialize them and re-read
        // the order so that it is marked as paid (and OrderPaidEvent is published) only once.
        await using var _ = await AsyncLock.KeyedAsync($"payos:order:{order.Id}", TimeSpan.FromSeconds(30));
        await _db.Entry(order).ReloadAsync();

        if (!order.CanMarkOrderAsPaid())
        {
            // Already paid (e.g. webhook and return page raced) or cancelled meanwhile.
            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                _db.OrderNotes.Add(order, T("Plugins.Smartstore.PayOS.PaidButCancelled", orderCode, info.AmountPaid.ToString("N0")));
                order.HasNewPaymentNotification = true;
                await _db.SaveChangesAsync();
            }

            return info.Status;
        }

        var expected = GetAmount(order);
        var reference = info.Transactions?.LastOrDefault()?.Reference;

        if (info.AmountPaid < expected)
        {
            _db.OrderNotes.Add(order, T("Plugins.Smartstore.PayOS.AmountMismatch", orderCode, info.AmountPaid.ToString("N0"), expected.ToString("N0")));
            order.HasNewPaymentNotification = true;
            await _db.SaveChangesAsync();

            return info.Status;
        }

        order.CaptureTransactionId = reference.NullEmpty() ?? orderCode.ToString();
        order.CaptureTransactionResult = info.Status;
        order.HasNewPaymentNotification = true;

        _db.OrderNotes.Add(order, T("Plugins.Smartstore.PayOS.PaymentReceived", info.AmountPaid.ToString("N0"), orderCode, reference.NaIfEmpty(), source));

        // Commits all pending changes.
        await _orderProcessingService.MarkOrderAsPaidAsync(order);

        return info.Status;
    }

    private static string CreateDescription(PayOSSettings settings, Order order)
    {
        // Bank transfer descriptions must be plain ASCII. Banks not linked via PayOS allow only 9 characters.
        var prefix = Regex.Replace(settings.DescriptionPrefix.EmptyNull(), "[^A-Za-z0-9]", string.Empty);
        var description = (prefix + order.Id).ToUpperInvariant();

        return description.Length > 25 ? description[^25..] : description;
    }
}
