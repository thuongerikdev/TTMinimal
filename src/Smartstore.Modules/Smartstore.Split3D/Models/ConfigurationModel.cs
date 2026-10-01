namespace Smartstore.Split3D.Models;

[LocalizedDisplay("Plugins.Split3D.Fields.")]
public class ConfigurationModel : ModelBase
{
    [LocalizedDisplay("*AutoIssueEnabled")]
    public bool AutoIssueEnabled { get; set; }

    [LocalizedDisplay("*SendEmail")]
    public bool SendEmail { get; set; }

    [LocalizedDisplay("*ProductCode")]
    public string ProductCode { get; set; }

    [LocalizedDisplay("*PublicKeyJson")]
    public string PublicKeyJson { get; set; }

    /// <summary>
    /// Only posted when the admin wants to replace the stored key. Never rendered back.
    /// </summary>
    [LocalizedDisplay("*PrivateKeyJson")]
    public string PrivateKeyJson { get; set; }

    public bool HasPrivateKey { get; set; }
    public string KeyStatus { get; set; }
    public bool KeyValid { get; set; }

    [LocalizedDisplay("*OneYearProductId")]
    public int OneYearProductId { get; set; }

    [LocalizedDisplay("*SixMonthsProductId")]
    public int SixMonthsProductId { get; set; }

    [LocalizedDisplay("*ThreeMonthsProductId")]
    public int ThreeMonthsProductId { get; set; }

    [LocalizedDisplay("*LifetimeProductId")]
    public int LifetimeProductId { get; set; }

    [LocalizedDisplay("*ObfuscateAddons")]
    public bool ObfuscateAddons { get; set; }

    [LocalizedDisplay("*PyArmorPython")]
    public string PyArmorPython { get; set; }

    [LocalizedDisplay("*PyArmorPlatforms")]
    public string PyArmorPlatforms { get; set; }

    [LocalizedDisplay("*DefaultMaxDevices")]
    [Range(1, 100)]
    public int DefaultMaxDevices { get; set; }

    [LocalizedDisplay("*LeaseDays")]
    [Range(1, 90)]
    public int LeaseDays { get; set; }

    [LocalizedDisplay("*CustomerDeactivationLimit")]
    [Range(0, 100)]
    public int CustomerDeactivationLimit { get; set; }

    [LocalizedDisplay("*ActivationServerUrl")]
    public string ActivationServerUrl { get; set; }

    /// <summary>
    /// The address used when <see cref="ActivationServerUrl"/> is empty.
    /// </summary>
    public string DefaultActivationServerUrl { get; set; }

    [LocalizedDisplay("*SimplifyAdminMenu")]
    public bool SimplifyAdminMenu { get; set; }

    [LocalizedDisplay("*HiddenAdminMenuItems")]
    public string HiddenAdminMenuItems { get; set; }

    [LocalizedDisplay("*BankName")]
    public string BankName { get; set; }

    [LocalizedDisplay("*BankAccountNumber")]
    public string BankAccountNumber { get; set; }

    [LocalizedDisplay("*BankAccountHolder")]
    public string BankAccountHolder { get; set; }

    [LocalizedDisplay("*EmailSubject")]
    public string EmailSubject { get; set; }

    [LocalizedDisplay("*EmailBody")]
    public string EmailBody { get; set; }
}
