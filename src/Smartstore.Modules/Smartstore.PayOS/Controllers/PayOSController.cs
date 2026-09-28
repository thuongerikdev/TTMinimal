using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.PayOS.Client;
using Smartstore.PayOS.Services;
using Smartstore.Web.Controllers;

namespace Smartstore.PayOS.Controllers;

public class PayOSController : ModuleController
{
    private readonly SmartDbContext _db;
    private readonly PayOSService _payOSService;

    public PayOSController(SmartDbContext db, PayOSService payOSService)
    {
        _db = db;
        _payOSService = payOSService;
    }

    /// <summary>
    /// The customer is redirected here by PayOS after the payment. Query parameters are not trusted,
    /// the status is always fetched from the PayOS API.
    /// </summary>
    [HttpGet, Route(PayOSService.ReturnPath)]
    public async Task<IActionResult> Return(long orderCode)
    {
        var order = await _payOSService.FindOrderAsync(orderCode);
        if (order == null)
        {
            return RedirectToRoute("Homepage");
        }

        try
        {
            var settings = await _payOSService.LoadSettingsAsync(order.StoreId);
            var status = await _payOSService.SyncPaymentStatusAsync(order, orderCode, settings, "return");

            if (status != PayOSStatus.Paid)
            {
                // The transfer can arrive a few seconds later. The webhook will complete the order then.
                NotifyInfo(T("Plugins.Smartstore.PayOS.PaymentProcessing"));
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PayOS return handling failed for order code {0}.", orderCode);
        }

        return await RedirectToOrderAsync(order);
    }

    [HttpGet, Route(PayOSService.CancelPath)]
    public async Task<IActionResult> Cancel(long orderCode)
    {
        var order = await _payOSService.FindOrderAsync(orderCode);
        if (order == null)
        {
            return RedirectToRoute("Homepage");
        }

        NotifyWarning(T("Plugins.Smartstore.PayOS.PaymentCancelled"));

        return await RedirectToOrderAsync(order);
    }

    [HttpPost, Route(PayOSService.WebhookPath), WebhookEndpoint]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        PayOSWebhookMessage message;
        try
        {
            using var reader = new StreamReader(HttpContext.Request.Body, leaveOpen: true);
            var json = await reader.ReadToEndAsync();
            message = JsonSerializer.Deserialize<PayOSWebhookMessage>(json, PayOSHttpClient.JsonOptions);
        }
        catch (JsonException ex)
        {
            Logger.Warn(ex, "Invalid PayOS webhook payload.");
            return BadRequest();
        }

        if (message == null
            || message.Data.ValueKind != JsonValueKind.Object
            || !message.Data.TryGetProperty("orderCode", out var orderCodeProp)
            || !orderCodeProp.TryGetInt64(out var orderCode))
        {
            return BadRequest();
        }

        var order = await _payOSService.FindOrderAsync(orderCode);
        if (order == null)
        {
            // Also the case for the test message PayOS sends when the webhook URL is confirmed. It must be acknowledged.
            Logger.Info("PayOS webhook for unknown order code {0} acknowledged.", orderCode);
            return Ok(new { success = true });
        }

        try
        {
            var settings = await _payOSService.LoadSettingsAsync(order.StoreId);
            if (!PayOSSignature.Verify(message.Data, message.Signature, settings.ChecksumKey))
            {
                Logger.Warn("PayOS webhook with invalid signature for order code {0} rejected.", orderCode);
                return BadRequest();
            }

            await _payOSService.SyncPaymentStatusAsync(order, orderCode, settings, "webhook");

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PayOS webhook handling failed for order code {0}.", orderCode);

            // Let PayOS retry the message.
            return StatusCode(500);
        }
    }

    private async Task<IActionResult> RedirectToOrderAsync(Order order)
    {
        var customer = Services.WorkContext.CurrentCustomer;
        if (customer.Id != order.CustomerId)
        {
            // E.g. the payment was completed on another device.
            NotifyInfo(T("Plugins.Smartstore.PayOS.SeeOrderEmail"));
            return RedirectToRoute("Homepage");
        }

        var latestOrderId = await _db.Orders
            .Where(x => x.CustomerId == customer.Id && x.StoreId == order.StoreId && !x.Deleted)
            .OrderByDescending(x => x.CreatedOnUtc)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        // The "completed" page always shows the latest order, so only use it for that one.
        return latestOrderId == order.Id
            ? RedirectToAction(nameof(CheckoutController.Completed), "Checkout")
            : RedirectToAction(nameof(OrderController.Details), "Order", new { id = order.Id });
    }
}
