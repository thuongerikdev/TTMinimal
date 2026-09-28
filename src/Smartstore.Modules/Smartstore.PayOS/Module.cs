global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Smartstore.Web.Modelling;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.PayOS.Configuration;

namespace Smartstore.PayOS;

internal class Module : ModuleBase, IConfigurable
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "PayOSAdmin", new { area = "Admin" });

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await ImportLanguageResourcesAsync();
        await TrySaveSettingsAsync<PayOSSettings>();

        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteLanguageResourcesAsync();
        await DeleteSettingsAsync<PayOSSettings>();

        await base.UninstallAsync();
    }
}
