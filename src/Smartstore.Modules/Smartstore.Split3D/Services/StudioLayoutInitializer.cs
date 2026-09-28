using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Engine.Initialization;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Applies a new version of the studio storefront layout on the first request after deployment,
/// so a server update needs no manual step. Skipped until the shop has been set up once.
/// </summary>
internal class StudioLayoutInitializer : IApplicationInitializer
{
    private readonly IModuleCatalog _moduleCatalog;
    private readonly Split3DStorefrontSetup _storefrontSetup;
    private readonly StudioStorefrontSetup _studioSetup;

    public StudioLayoutInitializer(IModuleCatalog moduleCatalog, Split3DStorefrontSetup storefrontSetup, StudioStorefrontSetup studioSetup)
    {
        _moduleCatalog = moduleCatalog;
        _storefrontSetup = storefrontSetup;
        _studioSetup = studioSetup;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    // After database migrations and permission installation.
    public int Order => int.MaxValue;
    public bool ThrowOnError => false;
    public int MaxAttempts => 3;

    public async Task InitializeAsync(HttpContext httpContext)
    {
        if (_studioSetup.IsUpToDate || _moduleCatalog.GetModuleByAssembly(GetType().Assembly)?.IsInstalled() != true)
        {
            return;
        }

        // Not bound to the request: a client giving up on the (slow) first request must not cancel the setup halfway.
        if (await _storefrontSetup.IsSetUpAsync())
        {
            await _studioSetup.ApplyAsync();
        }
    }

    public Task OnFailAsync(Exception exception, bool willRetry)
    {
        Logger.Error(exception, "Applying the TT Minimal storefront layout failed.");
        return Task.CompletedTask;
    }
}
