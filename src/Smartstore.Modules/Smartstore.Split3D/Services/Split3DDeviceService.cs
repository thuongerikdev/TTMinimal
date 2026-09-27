#nullable enable

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;

namespace Smartstore.Split3D.Services;

/// <summary>
/// What the Blender addon sends to the activation endpoints.
/// </summary>
public sealed class Split3DDeviceRequest
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>
    /// The PRODUCT constant of the addon that sends the request.
    /// </summary>
    [JsonPropertyName("product")]
    public string? Product { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("blender")]
    public string? BlenderVersion { get; set; }

    [JsonPropertyName("addon_version")]
    public string? AddonVersion { get; set; }

    [JsonIgnore]
    public string? IpAddress { get; set; }
}

/// <summary>
/// Result codes of the activation endpoints. The addon maps them to its own translated messages.
/// </summary>
public static class Split3DActivationCodes
{
    public const string Ok = "ok";
    public const string BadRequest = "bad_request";
    public const string InvalidKey = "invalid_key";
    public const string UnknownKey = "unknown_key";
    public const string WrongProduct = "wrong_product";
    public const string Blocked = "blocked";
    public const string Expired = "expired";
    public const string DeviceLimit = "device_limit";
    public const string Deactivated = "deactivated";
    public const string ServerError = "server_error";
}

public sealed class Split3DActivationResult
{
    [JsonPropertyName("ok")]
    public bool Ok => Code == Split3DActivationCodes.Ok;

    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>
    /// English fallback message for addon versions that do not know <see cref="Code"/>.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("lease")]
    public string? Lease { get; init; }

    /// <summary>
    /// Names of the devices that block a new activation (<see cref="Split3DActivationCodes.DeviceLimit"/>).
    /// </summary>
    [JsonPropertyName("devices")]
    public List<string>? Devices { get; init; }

    [JsonPropertyName("max_devices")]
    public int? MaxDevices { get; init; }

    public static Split3DActivationResult Fail(string code, string message)
        => new() { Code = code, Message = message };
}

/// <summary>
/// Online activation of keys on devices: binds a key to a limited number of computers, hands out
/// signed leases the addon needs in addition to the key, and signs devices out.
/// </summary>
public partial class Split3DDeviceService
{
    [GeneratedRegex("^[a-f0-9]{32,64}$")]
    private static partial Regex DeviceIdRegex();

    public const int CustomerLimitPeriodDays = 30;

    private readonly SmartDbContext _db;
    private readonly Split3DLicenseService _licenseService;
    private readonly Split3DSettings _settings;

    public Split3DDeviceService(SmartDbContext db, Split3DLicenseService licenseService, Split3DSettings settings)
    {
        _db = db;
        _licenseService = licenseService;
        _settings = settings;
    }

    public int GetMaxDevices(Split3DLicense license)
        => Math.Max(1, license.MaxDevices ?? _settings.DefaultMaxDevices);

    /// <summary>
    /// Activates the key on the requesting device, or refreshes the lease of an active device.
    /// </summary>
    /// <param name="allowNewActivation">
    /// <c>true</c> when the user explicitly activates. <c>false</c> for the periodic check, which must
    /// never re-activate a device that was signed out elsewhere.
    /// </param>
    public async Task<Split3DActivationResult> ActivateAsync(Split3DDeviceRequest request, bool allowNewActivation, CancellationToken cancelToken = default)
    {
        Guard.NotNull(request);

        var (license, payload, error) = await ValidateAsync(request, cancelToken);
        if (error != null)
        {
            return error;
        }

        var now = DateTime.UtcNow;
        var devices = await _db.Split3DDevices()
            .Where(x => x.Split3DLicenseId == license!.Id)
            .ToListAsync(cancelToken);

        var device = devices.FirstOrDefault(x => x.DeviceId == request.DeviceId);
        var maxDevices = GetMaxDevices(license!);

        if (device == null || !device.IsActive)
        {
            if (!allowNewActivation)
            {
                return Split3DActivationResult.Fail(Split3DActivationCodes.Deactivated, "This device was signed out. Activate the key again.");
            }

            var others = devices.Where(x => x.IsActive && x.DeviceId != request.DeviceId).ToList();
            if (others.Count >= maxDevices)
            {
                return new Split3DActivationResult
                {
                    Code = Split3DActivationCodes.DeviceLimit,
                    Message = $"The key is already active on {others.Count} of {maxDevices} allowed device(s). Sign out one of them first.",
                    Devices = others.OrderByDescending(x => x.LastSeenOnUtc).Select(x => x.DeviceName.NullEmpty() ?? x.DeviceId[..8]).ToList(),
                    MaxDevices = maxDevices
                };
            }

            if (device == null)
            {
                device = new Split3DDevice
                {
                    Split3DLicenseId = license!.Id,
                    DeviceId = request.DeviceId!,
                    FirstActivatedOnUtc = now
                };

                _db.Split3DDevices().Add(device);
                devices.Add(device);
            }

            device.ActivatedOnUtc = now;
            device.DeactivatedOnUtc = null;
            device.DeactivatedBy = null;
        }

        device.DeviceName = request.DeviceName?.Trim().Truncate(200).NullEmpty() ?? device.DeviceName;
        device.Platform = request.Platform?.Trim().Truncate(100).NullEmpty() ?? device.Platform;
        device.BlenderVersion = request.BlenderVersion?.Trim().Truncate(50).NullEmpty() ?? device.BlenderVersion;
        device.AddonVersion = request.AddonVersion?.Trim().Truncate(50).NullEmpty() ?? device.AddonVersion;
        device.LastIpAddress = request.IpAddress?.Truncate(100) ?? device.LastIpAddress;
        device.LastSeenOnUtc = now;

        await _db.SaveChangesAsync(cancelToken);

        var issued = new DateTimeOffset(now).ToUnixTimeSeconds();
        var leaseEnd = issued + Math.Clamp(_settings.LeaseDays, 1, 90) * 86400L;
        long? keyExpires = license!.ExpiresOnUtc.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(license.ExpiresOnUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds()
            : null;

        if (keyExpires.HasValue && keyExpires.Value < leaseEnd)
        {
            leaseEnd = keyExpires.Value;
        }

        var lease = _licenseService.CreateSigner(true).SignLease(new Split3DLeasePayload
        {
            Product = payload!.Product,
            Id = payload.Id,
            Device = device.DeviceId,
            Issued = issued,
            Expires = leaseEnd,
            KeyExpires = keyExpires,
            Activated = new DateTimeOffset(DateTime.SpecifyKind(device.ActivatedOnUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            MaxDevices = maxDevices,
            ActiveDevices = devices.Count(x => x.IsActive)
        });

        return new Split3DActivationResult
        {
            Code = Split3DActivationCodes.Ok,
            Message = "Activated.",
            Lease = lease,
            MaxDevices = maxDevices
        };
    }

    /// <summary>
    /// Signs the requesting device out, freeing its slot. Idempotent.
    /// </summary>
    public async Task<Split3DActivationResult> DeactivateAsync(Split3DDeviceRequest request, CancellationToken cancelToken = default)
    {
        Guard.NotNull(request);

        // Signing out must work for expired and blocked keys too, so only the signature and the record are checked.
        if (!IsValidDeviceId(request.DeviceId) || request.Token.IsEmpty())
        {
            return Split3DActivationResult.Fail(Split3DActivationCodes.BadRequest, "Invalid request.");
        }

        Split3DKeyPayload payload;
        try
        {
            payload = _licenseService.CreateSigner(false).Verify(request.Token!);
        }
        catch (Split3DKeyException)
        {
            return Split3DActivationResult.Fail(Split3DActivationCodes.InvalidKey, "Invalid key.");
        }

        var device = await (
            from d in _db.Split3DDevices()
            join l in _db.Split3DLicenses() on d.Split3DLicenseId equals l.Id
            where l.LicenseId == payload.Id && d.DeviceId == request.DeviceId
            select d)
            .FirstOrDefaultAsync(cancelToken);

        if (device?.IsActive == true)
        {
            Deactivate(device, Split3DDeactivatedBy.Device);
            await _db.SaveChangesAsync(cancelToken);
        }

        return new Split3DActivationResult { Code = Split3DActivationCodes.Ok, Message = "Signed out." };
    }

    /// <summary>
    /// Number of devices the customer signed out on the website within the last <see cref="CustomerLimitPeriodDays"/> days.
    /// </summary>
    public Task<int> CountCustomerDeactivationsAsync(IEnumerable<int> licenseIds, CancellationToken cancelToken = default)
    {
        var ids = licenseIds.ToArray();
        var since = DateTime.UtcNow.AddDays(-CustomerLimitPeriodDays);

        return _db.Split3DDevices().CountAsync(x =>
            ids.Contains(x.Split3DLicenseId)
            && x.DeactivatedBy == Split3DDeactivatedBy.Customer
            && x.DeactivatedOnUtc > since, cancelToken);
    }

    /// <summary>
    /// Marks <paramref name="device"/> as signed out (without committing).
    /// </summary>
    public static void Deactivate(Split3DDevice device, string deactivatedBy)
    {
        Guard.NotNull(device);

        device.DeactivatedOnUtc = DateTime.UtcNow;
        device.DeactivatedBy = deactivatedBy;
    }

    public static bool IsValidDeviceId(string? deviceId)
        => deviceId != null && DeviceIdRegex().IsMatch(deviceId);

    /// <summary>
    /// Formats a period as "12 days", "5 hours" or "&lt; 1 hour".
    /// </summary>
    public static string FormatDuration(TimeSpan span, Localizer T)
    {
        if (span.TotalDays >= 1)
        {
            return T("Plugins.Split3D.Duration.Days", (int)span.TotalDays);
        }

        return span.TotalHours >= 1
            ? T("Plugins.Split3D.Duration.Hours", (int)span.TotalHours)
            : T("Plugins.Split3D.Duration.LessThanHour");
    }

    private async Task<(Split3DLicense? License, Split3DKeyPayload? Payload, Split3DActivationResult? Error)> ValidateAsync(
        Split3DDeviceRequest request,
        CancellationToken cancelToken)
    {
        if (!IsValidDeviceId(request.DeviceId) || request.Token.IsEmpty() || request.Token!.Length > Split3DKeySigner.MaxTokenLength * 2)
        {
            return (null, null, Split3DActivationResult.Fail(Split3DActivationCodes.BadRequest, "Invalid request."));
        }

        Split3DKeySigner signer;
        try
        {
            signer = _licenseService.CreateSigner(true);
        }
        catch (Split3DKeyException)
        {
            return (null, null, Split3DActivationResult.Fail(Split3DActivationCodes.ServerError, "The activation server is not configured."));
        }

        Split3DKeyPayload payload;
        try
        {
            payload = signer.Verify(request.Token);
        }
        catch (Split3DKeyException)
        {
            return (null, null, Split3DActivationResult.Fail(Split3DActivationCodes.InvalidKey, "Invalid key."));
        }

        if (!string.IsNullOrEmpty(request.Product) && request.Product != payload.Product)
        {
            return (null, null, Split3DActivationResult.Fail(Split3DActivationCodes.WrongProduct, "This key belongs to another addon."));
        }

        var license = await _db.Split3DLicenses().FirstOrDefaultAsync(x => x.LicenseId == payload.Id, cancelToken);
        if (license == null)
        {
            return (null, null, Split3DActivationResult.Fail(Split3DActivationCodes.UnknownKey, "This key is not registered. Please contact the seller."));
        }

        if (license.Blocked)
        {
            return (null, null, Split3DActivationResult.Fail(Split3DActivationCodes.Blocked, "This key has been blocked. Please contact the seller."));
        }

        if (license.ExpiresOnUtc.HasValue && license.ExpiresOnUtc.Value <= DateTime.UtcNow)
        {
            return (null, null, Split3DActivationResult.Fail(Split3DActivationCodes.Expired, "The key has expired."));
        }

        return (license, payload, null);
    }
}
