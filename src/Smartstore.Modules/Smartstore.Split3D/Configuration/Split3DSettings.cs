namespace Smartstore.Split3D.Configuration;

public class Split3DSettings : ISettings
{
    /// <summary>
    /// Automatically issue keys when an order containing a mapped product is paid.
    /// </summary>
    public bool AutoIssueEnabled { get; set; } = true;

    /// <summary>
    /// Queue an email with the key to the customer after issuing. Content and look are edited in
    /// TT Minimal Studio &gt; Email (message template <see cref="Services.StudioMailService.LicenseTemplateName"/>).
    /// </summary>
    public bool SendEmail { get; set; } = true;

    /// <summary>
    /// Also attach the installer zip to the key email. The email always has a direct download button;
    /// attachments make the email large and some mail providers block zip files.
    /// </summary>
    public bool AttachInstaller { get; set; }

    /// <summary>
    /// The "product" value embedded in the signed payload. Must match what the addon expects.
    /// </summary>
    public string ProductCode { get; set; } = "split3d-custom-109";

    /// <summary>
    /// Public key JSON (<c>{"n": ..., "e": ...}</c>), identical to the addon's public_key.json.
    /// </summary>
    public string PublicKeyJson { get; set; }

    /// <summary>
    /// Private signing key JSON (<c>{"n": ..., "d": ...}</c>) from private_key.json.
    /// </summary>
    public string PrivateKeyJson { get; set; }

    /// <summary>
    /// Number of devices a key may be active on at the same time, unless the key overrides it.
    /// </summary>
    public int DefaultMaxDevices { get; set; } = 1;

    /// <summary>
    /// Obfuscate marketplace add-ons with PyArmor on upload (see <see cref="Services.MarketplacePackager"/>).
    /// </summary>
    public bool ObfuscateAddons { get; set; }

    /// <summary>
    /// Python executable with PyArmor installed. Its version must match Blender's Python
    /// (Blender 4.2–4.5: 3.11, Blender 5.x: 3.13), e.g. "C:\Python313\python.exe" or "/usr/bin/python3.13".
    /// </summary>
    public string PyArmorPython { get; set; }

    /// <summary>
    /// PyArmor target platforms, comma separated.
    /// </summary>
    public string PyArmorPlatforms { get; set; } = "windows.x86_64, linux.x86_64, darwin.x86_64, darwin.aarch64";

    /// <summary>
    /// Days the addon keeps working offline after its last successful online check.
    /// </summary>
    public int LeaseDays { get; set; } = 7;

    /// <summary>
    /// How many devices a customer may sign out on the website within 30 days. 0 = unlimited.
    /// Limits passing one key around; signing out from the device itself is always allowed.
    /// </summary>
    public int CustomerDeactivationLimit { get; set; } = 3;

    /// <summary>
    /// Address of this shop as the addon reaches it. Written as SERVER_URL into every uploaded addon zip.
    /// Empty = the store URL (Configuration > Stores). Defaults to the production shop, so zips uploaded
    /// on a local development copy still point customers to the live server.
    /// </summary>
    public string ActivationServerUrl { get; set; } = "https://ttminimal.com";

    /// <summary>
    /// Hides the admin menu items listed in <see cref="HiddenAdminMenuItems"/>.
    /// Only affects visibility; permissions and direct URLs are unchanged.
    /// </summary>
    public bool SimplifyAdminMenu { get; set; } = true;

    /// <summary>
    /// Comma or line separated ids of admin menu nodes (see Areas/Admin/sitemap.xml) to hide.
    /// </summary>
    public string HiddenAdminMenuItems { get; set; } = DefaultHiddenAdminMenuItems;

    public const string DefaultHiddenAdminMenuItems =
        "product-rules, reviews, manufacturers, tags, stockreport, specification-attributes, checkout-attributes,\n" +
        "shipments, recurring-payments, return-cases, gift-cards, shopping-carts, wishlists, flopsellers,\n" +
        "customer-rules, online-customers, customer-reports, external-auth, activity-log,\n" +
        "cart-rules, affiliates, newsletter-subscribers, campaigns,\n" +
        "widgets,\n" +
        "tax-header, tax-providers, tax-categories, list-settings, activity-types, import, export,\n" +
        "rulesets, seo-names";

    /// <summary>
    /// Bank transfer details shown at checkout and in the order note after placing an order.
    /// </summary>
    public string BankName { get; set; }
    public string BankAccountNumber { get; set; }
    public string BankAccountHolder { get; set; }

    public int OneYearProductId { get; set; }
    public int SixMonthsProductId { get; set; }
    public int ThreeMonthsProductId { get; set; }
    public int LifetimeProductId { get; set; }
}
