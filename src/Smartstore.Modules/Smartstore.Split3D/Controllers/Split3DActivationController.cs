using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Web;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// JSON endpoints called by the Blender addon: activate a key on a device, refresh the lease,
/// sign the device out. Anonymous by design; the signed key is the credential.
/// </summary>
[IgnoreAntiforgeryToken]
[Route("split3d/api/[action]")]
public class Split3DActivationController : Controller
{
    private const int MaxBodyLength = 32 * 1024;

    private readonly SmartDbContext _db;
    private readonly Split3DDeviceService _deviceService;
    private readonly Split3DRepoService _repoService;
    private readonly IWebHelper _webHelper;

    public Split3DActivationController(
        SmartDbContext db,
        Split3DDeviceService deviceService,
        Split3DRepoService repoService,
        IWebHelper webHelper)
    {
        _db = db;
        _deviceService = deviceService;
        _repoService = repoService;
        _webHelper = webHelper;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    [HttpPost, WebhookEndpoint]
    public Task<IActionResult> Activate()
        => HandleAsync(request => _deviceService.ActivateAsync(request, true, HttpContext.RequestAborted));

    [HttpPost, WebhookEndpoint]
    public Task<IActionResult> Refresh()
        => HandleAsync(request => _deviceService.ActivateAsync(request, false, HttpContext.RequestAborted));

    [HttpPost, WebhookEndpoint]
    public Task<IActionResult> Deactivate()
        => HandleAsync(request => _deviceService.DeactivateAsync(request, HttpContext.RequestAborted));

    private async Task<IActionResult> HandleAsync(Func<Split3DDeviceRequest, Task<Split3DActivationResult>> action)
    {
        Split3DActivationResult result;

        try
        {
            var request = await ReadRequestAsync();
            if (request == null)
            {
                result = Split3DActivationResult.Fail(Split3DActivationCodes.BadRequest, "Invalid request.");
            }
            else
            {
                request.IpAddress = _webHelper.ClientInfo.IpAddress?.ToString();
                result = await action(request);

                if (result.Ok && result.LicenseRecordId > 0)
                {
                    result.Update = await GetUpdateAsync(result.LicenseRecordId, request.AddonVersion);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Split3D activation request failed.");
            result = Split3DActivationResult.Fail(Split3DActivationCodes.ServerError, "Server error. Please try again later.");
        }

        // Always 200: the addon reads "ok"/"code" and never has to parse HTTP error pages.
        Response.Headers.CacheControl = "no-store";
        return Content(JsonSerializer.Serialize(result), "application/json");
    }

    /// <summary>
    /// The newest extension package of the key's addon, if it is newer than the version the addon reported.
    /// The addon's built-in updater downloads it from the key's repository and installs it with Blender's installer.
    /// </summary>
    private async Task<Split3DUpdateInfo> GetUpdateAsync(int licenseId, string installedVersion)
    {
        var license = await _db.Split3DLicenses().FindByIdAsync(licenseId);
        if (license == null || !Split3DRepoService.CanUpdate(license))
        {
            return null;
        }

        var latest = await _repoService.GetLatestPackageAsync(license.AddonId, HttpContext.RequestAborted);
        if (latest is not { } entry || Split3DAddonPackage.CompareVersions(entry.Package.Version, installedVersion) <= 0)
        {
            return null;
        }

        if (license.RepoToken.IsEmpty())
        {
            Split3DRepoService.EnsureToken(license);
            await _db.SaveChangesAsync();
        }

        return new Split3DUpdateInfo
        {
            Version = entry.Package.Version,
            Url = Split3DRepoService.GetFolderUrl(Request, license.RepoToken) + entry.Package.FileName,
            Size = entry.Package.Size,
            Hash = entry.Package.Hash
        };
    }

    private async Task<Split3DDeviceRequest> ReadRequestAsync()
    {
        if (Request.ContentLength > MaxBodyLength)
        {
            return null;
        }

        using var reader = new StreamReader(Request.Body);
        var buffer = new char[MaxBodyLength + 1];
        var length = await reader.ReadBlockAsync(buffer, HttpContext.RequestAborted);
        if (length == 0 || length > MaxBodyLength)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Split3DDeviceRequest>(buffer.AsSpan(0, length));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
