using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

/// <summary>
/// Who ended a device activation.
/// </summary>
public static class Split3DDeactivatedBy
{
    public const string Device = "Device";
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}

/// <summary>
/// A computer on which a <see cref="Split3DLicense"/> is (or was) activated. There is one row per
/// license and device; activating the key again on the same device reuses the row.
/// </summary>
[Table("Split3DDevice")]
[Index(nameof(Split3DLicenseId), nameof(DeviceId), IsUnique = true)]
[Index(nameof(DeviceId))]
public class Split3DDevice : BaseEntity
{
    /// <summary>
    /// Id of the <see cref="Split3DLicense"/> record (not the signed "id" of the key).
    /// </summary>
    public int Split3DLicenseId { get; set; }

    /// <summary>
    /// Hashed machine fingerprint sent by the addon. Never the raw hardware id.
    /// </summary>
    [Required, StringLength(64)]
    public string DeviceId { get; set; }

    /// <summary>
    /// Computer name reported by the addon.
    /// </summary>
    [StringLength(200)]
    public string DeviceName { get; set; }

    /// <summary>
    /// Operating system reported by the addon, e.g. "Windows 11".
    /// </summary>
    [StringLength(100)]
    public string Platform { get; set; }

    [StringLength(50)]
    public string BlenderVersion { get; set; }

    [StringLength(50)]
    public string AddonVersion { get; set; }

    [StringLength(100)]
    public string LastIpAddress { get; set; }

    /// <summary>
    /// When the key was activated on this device for the very first time.
    /// </summary>
    public DateTime FirstActivatedOnUtc { get; set; }

    /// <summary>
    /// Start of the current activation. Reset when the device is activated again after a deactivation.
    /// </summary>
    public DateTime ActivatedOnUtc { get; set; }

    /// <summary>
    /// Last successful online check of the addon on this device.
    /// </summary>
    public DateTime LastSeenOnUtc { get; set; }

    /// <summary>
    /// <c>null</c> while the activation is active.
    /// </summary>
    public DateTime? DeactivatedOnUtc { get; set; }

    /// <summary>
    /// See <see cref="Split3DDeactivatedBy"/>.
    /// </summary>
    [StringLength(20)]
    public string DeactivatedBy { get; set; }

    [NotMapped]
    public bool IsActive => DeactivatedOnUtc == null;
}
