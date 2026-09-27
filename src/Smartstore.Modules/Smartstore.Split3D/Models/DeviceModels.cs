namespace Smartstore.Split3D.Models;

[LocalizedDisplay("Plugins.Split3D.Device.")]
public class DeviceModel : EntityModelBase
{
    public int Split3DLicenseId { get; set; }

    [LocalizedDisplay("*DeviceName")]
    public string DeviceName { get; set; }

    [LocalizedDisplay("*Platform")]
    public string Platform { get; set; }

    [LocalizedDisplay("*BlenderVersion")]
    public string BlenderVersion { get; set; }

    [LocalizedDisplay("*AddonVersion")]
    public string AddonVersion { get; set; }

    [LocalizedDisplay("*LastIpAddress")]
    public string LastIpAddress { get; set; }

    [LocalizedDisplay("Plugins.Split3D.License.Email")]
    public string Email { get; set; }

    [LocalizedDisplay("Plugins.Split3D.License.Addon")]
    public string AddonName { get; set; }

    [LocalizedDisplay("*FirstActivatedOn")]
    public DateTime FirstActivatedOn { get; set; }

    [LocalizedDisplay("*ActivatedOn")]
    public DateTime ActivatedOn { get; set; }

    [LocalizedDisplay("*ActiveFor")]
    public string ActiveFor { get; set; }

    [LocalizedDisplay("*LastSeenOn")]
    public DateTime LastSeenOn { get; set; }

    [LocalizedDisplay("*Status")]
    public string Status { get; set; }
    public bool IsActive { get; set; }

    public string DevicesUrl { get; set; }
}

[LocalizedDisplay("Plugins.Split3D.Device.")]
public class DeviceListModel : ModelBase
{
    [LocalizedDisplay("*SearchTerm")]
    public string SearchTerm { get; set; }

    [LocalizedDisplay("*SearchActiveOnly")]
    public bool SearchActiveOnly { get; set; } = true;

    /// <summary>
    /// Restricts the list to the devices of one key (<see cref="Split3DLicense.Id"/>).
    /// </summary>
    public int? LicenseId { get; set; }

    public int ActiveCount { get; set; }
    public int TodayCount { get; set; }

    /// <summary>
    /// Set when the list is restricted to one key.
    /// </summary>
    public LicenseDevicesModel License { get; set; }
}

/// <summary>
/// Device settings of one key: limit and block flag.
/// </summary>
[LocalizedDisplay("Plugins.Split3D.License.")]
public class LicenseDevicesModel : EntityModelBase
{
    public string Email { get; set; }
    public string CustomerName { get; set; }
    public string AddonName { get; set; }
    public string KeyType { get; set; }
    public string ExpiresOn { get; set; }
    public bool IsExpired { get; set; }
    public int ActiveDevices { get; set; }
    public int DefaultMaxDevices { get; set; }

    [LocalizedDisplay("*MaxDevices")]
    public int? MaxDevices { get; set; }

    [LocalizedDisplay("*Blocked")]
    public bool Blocked { get; set; }
}
