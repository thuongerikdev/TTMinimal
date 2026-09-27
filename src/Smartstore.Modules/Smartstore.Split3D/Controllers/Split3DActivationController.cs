using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    private readonly Split3DDeviceService _deviceService;
    private readonly IWebHelper _webHelper;

    public Split3DActivationController(Split3DDeviceService deviceService, IWebHelper webHelper)
    {
        _deviceService = deviceService;
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
