using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Smartstore.ComponentModel;
using Smartstore.Core.Security;
using Smartstore.Core.Stores;
using Smartstore.Engine.Modularity;
using Smartstore.PayOS.Client;
using Smartstore.PayOS.Configuration;
using Smartstore.PayOS.Models;
using Smartstore.PayOS.Providers;
using Smartstore.PayOS.Services;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.Settings;

namespace Smartstore.PayOS.Controllers;

[Area("Admin")]
public class PayOSAdminController : ModuleController
{
    private readonly IProviderManager _providerManager;
    private readonly PayOSHttpClient _client;

    public PayOSAdminController(IProviderManager providerManager, PayOSHttpClient client)
    {
        _providerManager = providerManager;
        _client = client;
    }

    [LoadSetting, AuthorizeAdmin]
    public IActionResult Configure(PayOSSettings settings)
    {
        ViewBag.Provider = _providerManager.GetProvider(PayOSProvider.SystemName).Metadata;

        var model = MiniMapper.Map<PayOSSettings, ConfigurationModel>(settings);
        model.WebhookUrl = GetWebhookUrl(GetActiveStoreScopeConfiguration());

        return View(model);
    }

    [HttpPost, SaveSetting, AuthorizeAdmin]
    public IActionResult Configure(ConfigurationModel model, PayOSSettings settings)
    {
        if (!ModelState.IsValid)
        {
            return Configure(settings);
        }

        ModelState.Clear();
        MiniMapper.Map(model, settings);

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Configure));
    }

    [HttpPost, AuthorizeAdmin]
    [FormValueRequired("confirmwebhook"), ActionName("Configure")]
    public async Task<IActionResult> ConfirmWebhook()
    {
        var storeScope = GetActiveStoreScopeConfiguration();
        var settings = await Services.SettingFactory.LoadSettingsAsync<PayOSSettings>(storeScope);
        var webhookUrl = GetWebhookUrl(storeScope);

        if (!webhookUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            NotifyError(T("Plugins.Smartstore.PayOS.WebhookRequiresHttps"));
        }
        else if (!settings.HasCredentials())
        {
            NotifyError(T("Plugins.Smartstore.PayOS.NotConfigured"));
        }
        else
        {
            try
            {
                await _client.ConfirmWebhookAsync(settings, webhookUrl);
                NotifySuccess(T("Plugins.Smartstore.PayOS.WebhookConfirmed", webhookUrl));
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                NotifyError(ex.Message);
            }
        }

        return RedirectToAction(nameof(Configure));
    }

    private string GetWebhookUrl(int storeScope)
    {
        var store = storeScope == 0 ? Services.StoreContext.CurrentStore : Services.StoreContext.GetStoreById(storeScope);
        return store.GetAbsoluteUrl(PayOSService.WebhookPath);
    }
}
