using Microsoft.Extensions.Logging;
using Smartstore.Core.Checkout.Orders.Events;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Data;
using Smartstore.Events;

namespace Smartstore.Split3D;

public class Events : IConsumer
{
    /// <summary>
    /// Adds bank transfer instructions (amount, account, transfer reference) as a customer-visible order note.
    /// </summary>
    public async Task HandleEventAsync(
        OrderPlacedEvent message,
        Split3DUpgradeService upgradeService,
        PrintOrderService printOrderService,
        StudioOrderClassifier orderClassifier,
        Split3DSettings settings,
        SmartDbContext db,
        ILogger logger,
        CancellationToken cancelToken)
    {
        var order = message.Order;
        Split3DLicenseUpgrade upgrade = null;

        if (order != null)
        {
            try
            {
                upgrade = await upgradeService.AttachToOrderAsync(order, cancelToken);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Split3D: failed to link the key upgrade to order {order.Id}.");
            }

            try
            {
                // Links the print jobs paid with this order and copies the delivery address into it.
                await printOrderService.AttachToOrderAsync(order, cancelToken);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Split3D: failed to link the print job to order {order.Id}.");
            }
        }

        if (order == null
            || !order.PaymentMethodSystemName.EqualsNoCase(Split3DStorefrontSetup.PrepaymentSystemName)
            || order.PaymentStatus == PaymentStatus.Paid
            || settings.BankAccountNumber.IsEmpty())
        {
            return;
        }

        try
        {
            // Every bank transfer order gets the instructions; only the closing line depends on what was bought.
            var content = await orderClassifier.GetContentAsync(order.Id, cancelToken);
            if (content == StudioOrderContent.None)
            {
                return;
            }

            var nextStep = content == StudioOrderContent.Keys
                ? upgrade != null
                    ? "Key sẽ được nâng cấp tự động ngay khi chúng tôi xác nhận đã nhận tiền; addon trong Blender tự nhận gói mới, không cần cài lại."
                    : "Key kích hoạt và file cài đặt sẽ được gửi tự động ngay khi chúng tôi xác nhận đã nhận tiền."
                : content == StudioOrderContent.Print
                    ? "Studio bắt đầu kiểm tra file và xác nhận đơn in ngay khi nhận được tiền cọc."
                    : "Studio xác nhận và bắt đầu chuẩn bị đơn hàng ngay khi nhận được tiền; chúng tôi sẽ báo cho bạn khi giao hàng.";

            if (content.HasFlag(StudioOrderContent.Keys) && content != StudioOrderContent.Keys)
            {
                nextStep += "\nKey của addon trong đơn được gửi riêng qua email ngay khi xác nhận thanh toán.";
            }

            var note =
                "Cảm ơn bạn đã đặt hàng! Vui lòng chuyển khoản để hoàn tất:\n" +
                $"- Số tiền: {Split3DLicenseService.FormatPrice(order.OrderTotal)}\n" +
                $"- Ngân hàng: {settings.BankName}\n" +
                $"- Số tài khoản: {settings.BankAccountNumber}\n" +
                $"- Chủ tài khoản: {settings.BankAccountHolder}\n" +
                $"- Nội dung chuyển khoản: {order.GetOrderNumber()}\n\n" +
                nextStep;

            db.OrderNotes.Add(order, note, displayToCustomer: true);
            await db.SaveChangesAsync(cancelToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Split3D: failed to add payment instructions to order {order.Id}.");
        }
    }

    public async Task HandleEventAsync(
        OrderPaidEvent message,
        Split3DLicenseService licenseService,
        Split3DUpgradeService upgradeService,
        PrintOrderService printOrderService,
        Split3DSettings settings,
        SmartDbContext db,
        ILogger logger,
        CancellationToken cancelToken)
    {
        if (message.Order == null)
        {
            return;
        }

        try
        {
            // A paid print job waits for the studio to confirm it; the money is in, nothing is printed yet.
            await printOrderService.MarkPaidAsync(message.Order, cancelToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Split3D: failed to mark the print jobs of order {message.Order.Id} as paid.");
        }

        if (!settings.AutoIssueEnabled)
        {
            return;
        }

        try
        {
            await licenseService.IssueForOrderAsync(message.Order, cancelToken);
        }
        catch (Exception ex)
        {
            // Never break payment processing. Leave a note so the admin can issue the key manually.
            logger.Error(ex, $"Split3D: failed to issue license key for order {message.Order.Id}.");

            db.OrderNotes.Add(message.Order, $"Split3D: automatic key issuing failed: {ex.Message}");
            await db.SaveChangesAsync(cancelToken);
        }

        try
        {
            await upgradeService.ApplyForOrderAsync(message.Order, cancelToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Split3D: failed to upgrade the key of order {message.Order.Id}.");

            db.OrderNotes.Add(message.Order, $"Split3D: automatic key upgrade failed: {ex.Message}");
            await db.SaveChangesAsync(cancelToken);
        }
    }
}
