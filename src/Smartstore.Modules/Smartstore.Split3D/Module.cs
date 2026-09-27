global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.ComponentModel.DataAnnotations;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.EntityFrameworkCore;
global using Smartstore.Core.Configuration;
global using Smartstore.Engine.Modularity;
global using Smartstore.Split3D.Configuration;
global using Smartstore.Split3D.Domain;
global using Smartstore.Split3D.Models;
global using Smartstore.Split3D.Services;
global using Smartstore.Web.Modelling;
using Smartstore.Http;

namespace Smartstore.Split3D;

internal class Module : ModuleBase, IConfigurable
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "Split3D", new { area = "Admin" });

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await TrySaveSettingsAsync<Split3DSettings>();
        await ImportLanguageResourcesAsync();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        // License records are kept on purpose: they are the only proof of issued keys.
        await DeleteSettingsAsync<Split3DSettings>();
        await DeleteLanguageResourcesAsync();
        await base.UninstallAsync();
    }
}
