namespace Smartstore.Split3D.Configuration;

public class Split3DSettings : ISettings
{
    /// <summary>
    /// Automatically issue keys when an order containing a mapped product is paid.
    /// </summary>
    public bool AutoIssueEnabled { get; set; } = true;

    /// <summary>
    /// Queue an email with the key to the customer after issuing.
    /// </summary>
    public bool SendEmail { get; set; } = true;

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
    /// Subject of the key email. Supports the same placeholders as <see cref="EmailBody"/>.
    /// </summary>
    public string EmailSubject { get; set; } = "Key kích hoạt Split3D Print";

    /// <summary>
    /// Plain text body of the key email. Placeholders: {CustomerName}, {Email}, {Plan},
    /// {ExpiresOn}, {Price}, {Key}.
    /// </summary>
    public string EmailBody { get; set; } =
        "Chào {CustomerName},\n\n" +
        "Thông tin kích hoạt Split3D Print:\n" +
        "Email: {Email}\n" +
        "Gói: {Plan}\n" +
        "Hạn sử dụng: {ExpiresOn}\n" +
        "Giá: {Price}\n\n" +
        "Key kích hoạt:\n{Key}\n\n" +
        "Mở bảng Split3D trong Blender, dán toàn bộ key rồi bấm Kích hoạt.\n";

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
        "product-rules, reviews, manufacturers, tags, stockreport, attributes-header, attributes, specification-attributes, checkout-attributes,\n" +
        "shipments, recurring-payments, return-cases, gift-cards, shopping-carts, wishlists, flopsellers,\n" +
        "customer-rules, online-customers, customer-reports, external-auth, activity-log,\n" +
        "cart-rules, affiliates, newsletter-subscribers, campaigns,\n" +
        "widgets,\n" +
        "shipping-header, shipping-methods, shipping-providers, tax-header, tax-providers, tax-categories, list-settings, activity-types, import, export,\n" +
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
