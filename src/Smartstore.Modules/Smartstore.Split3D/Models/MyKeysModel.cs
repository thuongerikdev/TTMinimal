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
