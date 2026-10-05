global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Smartstore.Web.Modelling;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.PayOS.Configuration;
using Smartstore.PayOS.Tasks;
using Smartstore.Scheduling;

namespace Smartstore.PayOS;

internal class Module : ModuleBase, IConfigurable
{
    private readonly ITaskStore _taskStore;

    public Module(ITaskStore taskStore)
    {
        _taskStore = taskStore;
    }

    public RouteInfo GetConfigurationRoute()
        => new("Configure", "PayOSAdmin", new { area = "Admin" });

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await ImportLanguageResourcesAsync();
        await TrySaveSettingsAsync<PayOSSettings>();
        await PayOSTaskInitializer.RegisterTaskAsync(_taskStore);

        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteLanguageResourcesAsync();
        await DeleteSettingsAsync<PayOSSettings>();
        await _taskStore.TryDeleteTaskAsync<PayOSSyncTask>();

        await base.UninstallAsync();
    }
}
