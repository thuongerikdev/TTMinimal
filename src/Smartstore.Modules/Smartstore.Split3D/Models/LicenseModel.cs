namespace Smartstore.Split3D.Models;

[LocalizedDisplay("Plugins.Split3D.License.")]
public class LicenseModel : EntityModelBase
{
    [LocalizedDisplay("*LicenseId")]
    public string LicenseId { get; set; }

    public int AddonId { get; set; }

    [LocalizedDisplay("*Addon")]
    public string AddonName { get; set; }

    [LocalizedDisplay("*Email")]
    public string Email { get; set; }

    [LocalizedDisplay("*CustomerName")]
    public string CustomerName { get; set; }

    [LocalizedDisplay("*Phone")]
    public string Phone { get; set; }

    [LocalizedDisplay("*KeyType")]
    public string KeyType { get; set; }

    [LocalizedDisplay("*Price")]
    public decimal Price { get; set; }
    public string PriceString { get; set; }

    [LocalizedDisplay("*PurchaseDate")]
    public DateTime? PurchaseDate { get; set; }
    public string PurchaseDateString { get; set; }

    [LocalizedDisplay("*IssuedOn")]
    public DateTime IssuedOn { get; set; }

    [LocalizedDisplay("*ExpiresOn")]
    public DateTime? ExpiresOn { get; set; }
    public string ExpiresOnString { get; set; }

    [LocalizedDisplay("*State")]
    public string State { get; set; }
    public bool IsExpired { get; set; }

    [LocalizedDisplay("*Token")]
    public string Token { get; set; }

    [LocalizedDisplay("*Notes")]
    public string Notes { get; set; }

    [LocalizedDisplay("*Order")]
    public int OrderId { get; set; }
    public string OrderUrl { get; set; }

    [LocalizedDisplay("*EmailSent")]
    public bool EmailSent { get; set; }

    [LocalizedDisplay("*Devices")]
    public int ActiveDevices { get; set; }
    public int MaxDevices { get; set; }
    public string DevicesUrl { get; set; }
    public bool Blocked { get; set; }
}

[LocalizedDisplay("Plugins.Split3D.License.")]
public class LicenseListModel : ModelBase
{
    [LocalizedDisplay("*SearchTerm")]
    public string SearchTerm { get; set; }

    [LocalizedDisplay("*KeyType")]
    public string SearchKeyType { get; set; }

    [LocalizedDisplay("*Addon")]
    public int? SearchAddonId { get; set; }

    public int TotalCount { get; set; }
    public int CustomerCount { get; set; }
    public string TotalRevenue { get; set; }
    public int ActiveCount { get; set; }
    public string KeyStatus { get; set; }
    public bool KeyValid { get; set; }
    public bool AutoIssueEnabled { get; set; }
    public List<PendingOrderModel> PendingOrders { get; set; } = [];
}

public class PendingOrderModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public string CreatedOn { get; set; }
    public string Customer { get; set; }
    public string Items { get; set; }
    public string OrderTotal { get; set; }
    public Split3DOrderState State { get; set; }
    public int IssuedKeys { get; set; }
    public int ExpectedKeys { get; set; }
    public string EditUrl { get; set; }
}

[LocalizedDisplay("Plugins.Split3D.License.")]
public class IssueLicenseModel : ModelBase
{
    [LocalizedDisplay("*Addon")]
    public int AddonId { get; set; }

    [LocalizedDisplay("*Email")]
    public string Email { get; set; }

    [LocalizedDisplay("*CustomerName")]
    public string CustomerName { get; set; }

    [LocalizedDisplay("*Phone")]
    public string Phone { get; set; }

    [LocalizedDisplay("*KeyType")]
    public string KeyType { get; set; } = Split3DPlans.OneYear;

    [LocalizedDisplay("*Days")]
    public int? Days { get; set; }

    [LocalizedDisplay("*Price")]
    public decimal Price { get; set; }

    [LocalizedDisplay("*PurchaseDate")]
    public DateTime? PurchaseDate { get; set; }

    [LocalizedDisplay("*Notes")]
    public string Notes { get; set; }

    [LocalizedDisplay("*SendEmailNow")]
    public bool SendEmailNow { get; set; } = true;
}
