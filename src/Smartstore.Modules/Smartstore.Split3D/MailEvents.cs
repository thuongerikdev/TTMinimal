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

    public async Task HandleEventAsync(PreviewModelResolveEvent message, StudioMailService mailService)
    {
        if (message.ModelName == StudioMailService.LicenseModelName)
        {
            message.Result = await mailService.CreateDemoLicensePartAsync();
        }
    }
}
