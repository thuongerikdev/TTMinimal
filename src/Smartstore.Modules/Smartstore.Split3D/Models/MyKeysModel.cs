namespace Smartstore.Split3D.Models;

public class MyKeysModel : ModelBase
{
    public List<MyKeysOrderModel> Orders { get; set; } = [];

    /// <summary>
    /// Keys issued to the customer's email without an order (manual or imported).
    /// </summary>
    public List<MyKeyModel> OtherKeys { get; set; } = [];

    public string BankName { get; set; }
    public string BankAccountNumber { get; set; }
    public string BankAccountHolder { get; set; }

    /// <summary>
    /// Devices the customer may still sign out on the website. <c>null</c> = unlimited.
    /// </summary>
    public int? RemainingDeactivations { get; set; }
}

public class MyKeysOrderModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public DateTime CreatedOn { get; set; }
    public string OrderTotal { get; set; }
    public Split3DOrderState State { get; set; }
    public List<string> Items { get; set; } = [];
    public int ExpectedKeys { get; set; }
    public List<MyKeyModel> Keys { get; set; } = [];

    /// <summary>
    /// The order is paid by manual bank transfer (Prepayment), so the bank details are shown.
    /// </summary>
    public bool IsBankTransfer { get; set; }

    /// <summary>
    /// Inline SVG VietQR code with amount and transfer content prefilled. <c>null</c> if the bank is not supported.
    /// </summary>
    public string BankQrSvg { get; set; }

    /// <summary>
    /// The order is awaiting an online payment (e.g. PayOS) that the customer can start from here.
    /// </summary>
    public bool CanPayOnline { get; set; }
}

public class MyKeyModel
{
    public int Id { get; set; }
    public string AddonName { get; set; }
    public string Plan { get; set; }
    public string Token { get; set; }
    public string IssuedOn { get; set; }
    public string ExpiresOn { get; set; }
    public bool IsExpired { get; set; }
    public bool IsLifetime { get; set; }
    public bool IsBlocked { get; set; }

    /// <summary>
    /// Localized remaining validity, e.g. "120 days". <c>null</c> for lifetime or expired keys.
    /// </summary>
    public string Remaining { get; set; }

    public int MaxDevices { get; set; }
    public List<MyDeviceModel> Devices { get; set; } = [];

    /// <summary>
    /// Blender repository URL of the key (paste into Preferences > Get Extensions > Repositories).
    /// <c>null</c> when the addon has no extension package or the key cannot receive updates.
    /// </summary>
    public string RepoUrl { get; set; }

    /// <summary>
    /// Link to drag into Blender: installs the addon and adds the repository in one step.
    /// </summary>
    public string InstallUrl { get; set; }

    /// <summary>
    /// Direct download of the package zip: dropping the file into Blender installs it in one step.
    /// </summary>
    public string DownloadUrl { get; set; }

    public string PackageVersion { get; set; }

    /// <summary>
    /// A bigger package can be bought for the key.
    /// </summary>
    public bool CanUpgrade { get; set; }

    /// <summary>
    /// Formatted price of the cheapest upgrade.
    /// </summary>
    public string UpgradeFrom { get; set; }
}

public class UpgradeKeyModel : EntityModelBase
{
    public string AddonName { get; set; }
    public string Plan { get; set; }

    /// <summary>
    /// <c>null</c> for lifetime keys.
    /// </summary>
    public string ExpiresOn { get; set; }

    public int MaxDevices { get; set; }
    public string PaidPrice { get; set; }

    /// <summary>
    /// <c>false</c> for blocked or expired keys.
    /// </summary>
    public bool CanUpgrade { get; set; }

    public List<UpgradeKeyOptionModel> Options { get; set; } = [];
}

public class UpgradeKeyOptionModel
{
    public int ProductId { get; set; }
    public string Name { get; set; }
    public string Plan { get; set; }
    public int MaxDevices { get; set; }

    /// <summary>
    /// Expiry after the upgrade, <c>null</c> for lifetime.
    /// </summary>
    public string ExpiresOn { get; set; }

    public string FullPrice { get; set; }
    public string Price { get; set; }
}

public class MyDeviceModel
{
    public int Id { get; set; }
    public string DeviceName { get; set; }
    public string Platform { get; set; }
    public string BlenderVersion { get; set; }
    public string ActivatedOn { get; set; }

    /// <summary>
    /// How long the key has been active on this device, e.g. "12 days".
    /// </summary>
    public string ActiveFor { get; set; }

    public string LastSeenOn { get; set; }
}
