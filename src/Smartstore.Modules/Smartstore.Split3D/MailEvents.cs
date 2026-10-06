using Smartstore.Core.Data;
using Smartstore.Core.Messaging;
using Smartstore.Core.Messaging.Events;
using Smartstore.Events;

namespace Smartstore.Split3D;

/// <summary>
/// Applies the studio branding to every store email and provides the preview model of the key email.
/// </summary>
public class MailEvents : IConsumer
{
    public async Task HandleEventAsync(MessageModelCreatedEvent message, StudioMailService mailService, ISettingFactory settingFactory)
    {
        var ctx = message.MessageContext;
        var storeId = ctx.Store?.Id ?? 0;

        var mail = mailService.LayoutOverride ?? await settingFactory.LoadSettingsAsync<StudioMailSettings>(storeId);
        if (!mail.BrandingEnabled)
        {
            return;
        }

        var studio = await settingFactory.LoadSettingsAsync<StudioSettings>(storeId);
        var model = ctx.Model;

        model["Theme"] = StudioMailService.CreateThemePart(mail);
        model["Studio"] = StudioMailService.CreateStudioPart(mail, studio, ctx.BaseUri?.ToString() ?? ctx.Store?.GetBaseUrl());
    }

    /// <summary>
    /// Orders with only key products get a single email (the key email, sent when the order is paid):
    /// Smartstore's own "order placed" / "order completed" customer emails stay in the queue as "send manually".
    /// </summary>
    public async Task HandleEventAsync(
        MessageQueuingEvent message,
        Split3DSettings settings,
        Split3DLicenseService licenseService,
        SmartDbContext db,
        CancellationToken cancelToken)
    {
        var templateName = message.MessageContext?.MessageTemplate?.Name;
        if (!settings.CombineOrderEmails
            || message.QueuedEmail == null
            || message.MessageContext.TestMode
            || !(templateName == MessageTemplateNames.OrderPlacedCustomer || templateName == MessageTemplateNames.OrderCompletedCustomer))
        {
            return;
        }

        var orderId = GetOrderId(message.MessageModel);
        if (orderId == 0)
        {
            return;
        }

        var planProductIds = (await licenseService.GetProductPlansAsync(cancelToken)).Keys.ToArray();
        var productIds = await db.OrderItems
            .Where(x => x.OrderId == orderId)
            .Select(x => x.ProductId)
            .ToListAsync(cancelToken);

        if (productIds.Count > 0 && productIds.All(planProductIds.Contains))
        {
            message.QueuedEmail.SendManually = true;
        }
    }

    private static int GetOrderId(TemplateModel model)
    {
        try
        {
            return model != null && model.TryGetValue("Order", out var order) && order != null
                ? Convert.ToInt32(((dynamic)order).Id)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task HandleEventAsync(PreviewModelResolveEvent message, StudioMailService mailService)
    {
        if (message.ModelName == StudioMailService.LicenseModelName)
        {
            message.Result = await mailService.CreateDemoLicensePartAsync();
        }
    }
}
