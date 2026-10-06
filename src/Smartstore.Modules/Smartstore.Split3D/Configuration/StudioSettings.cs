namespace Smartstore.Split3D.Configuration;

/// <summary>
/// Settings of the TT Minimal studio storefront: contact details, 3D printing price list and quote requests.
/// </summary>
public class StudioSettings : ISettings
{
    public string BrandName { get; set; } = "TT Minimal";

    public string Tagline { get; set; } = "Minimal 3D Printing Studio";

    public string Address { get; set; } = "Số 2, Nhà vườn 5, Yên Xá, Phường Thanh Liệt, Hà Nội";

    public string Phone1 { get; set; } = "0333424766";

    public string Phone2 { get; set; } = "0963490626";

    /// <summary>
    /// Phone number of the Zalo account. Empty = no Zalo link.
    /// </summary>
    public string ZaloPhone { get; set; } = "0333424766";

    public string FacebookUrl { get; set; } = "https://facebook.com/TT.minimal";

    /// <summary>
    /// Public contact email shown in the footer. Empty = no email shown.
    /// </summary>
    public string Email { get; set; }

    public string MapUrl { get; set; } = "https://www.google.com/maps/search/?api=1&query=Y%C3%AAn+X%C3%A1%2C+Thanh+Li%E1%BB%87t%2C+H%C3%A0+N%E1%BB%99i";

    /// <summary>
    /// 3D printing price list, one tier per line: <c>Technology | Materials | From grams | Price per gram</c>.
    /// See <see cref="Services.PrintPriceList"/>.
    /// </summary>
    public string PrintPriceTable { get; set; } = DefaultPrintPriceTable;

    public string PrintPriceNote { get; set; } = "Đơn giá chưa bao gồm xử lý phôi in (chà nhám, sơn) và ghép phôi.";

    /// <summary>
    /// Calibration of the weight estimated from an uploaded FDM model, in percent of the computed value.
    /// Below 100 when the estimate is higher than the sliced / real weight.
    /// </summary>
    public int FdmWeightFactor { get; set; } = 77;

    /// <summary>
    /// Calibration of the weight estimated from an uploaded resin model, in percent of the computed value.
    /// Above 100 covers supports and resin the plain model volume does not include.
    /// </summary>
    public int ResinWeightFactor { get; set; } = 125;

    /// <summary>
    /// Receives new quote requests. Empty = the store's default email account.
    /// </summary>
    public string QuoteNotifyEmail { get; set; }

    /// <summary>
    /// Maximum size of a model file attached to a quote request, in megabytes.
    /// </summary>
    public int QuoteMaxFileSizeMb { get; set; } = 100;

    /// <summary>
    /// Share of the estimated price a print job is paid with up front, in percent. 100 = the whole amount.
    /// The rest is collected when the print is handed over.
    /// </summary>
    public int DepositPercent { get; set; } = 50;

    /// <summary>
    /// Whether the customer may pick the print up at the studio instead of having it delivered.
    /// </summary>
    public bool AllowPickup { get; set; } = true;

    /// <summary>
    /// Hint below the deposit on the order page, e.g. how the rest is settled.
    /// </summary>
    public string DepositNote { get; set; } = DefaultDepositNote;

    /// <summary>
    /// Template of the message the studio sends a quote request by Zalo, SMS or Messenger.
    /// Placeholders: {Name}, {Id}, {Brand}, {Price}, {Phone}, {Link}.
    /// </summary>
    public string QuoteMessageTemplate { get; set; } = DefaultQuoteMessageTemplate;

    /// <summary>
    /// Version of the storefront layout (theme, menus, trimmed forms) applied by <see cref="Services.StudioStorefrontSetup"/>.
    /// </summary>
    public int LayoutVersion { get; set; }

    public const string DefaultDepositNote =
        "Số tiền đặt cọc giữ chỗ in; phần còn lại thanh toán khi nhận hàng. Nếu file slice thực tế nhẹ hơn, " +
        "studio tính lại theo khối lượng thật và hoàn phần chênh.";

    public const string DefaultQuoteMessageTemplate =
        "Chào {Name}, {Brand} đã nhận yêu cầu in 3D #{Id} của bạn. " +
        "Báo giá: {Price}. Bạn cho mình biết màu và thời gian cần hàng để chốt đơn nhé. " +
        "Đặt in và thanh toán tại: {Link}";

    public const string DefaultPrintPriceTable =
        "# Kiểu in | Vật liệu (cách nhau dấu phẩy, tỉ trọng g/cm³ trong ngoặc) | Từ gram | Giá/gram\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 0 | 1500\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 1000 | 1200\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 2000 | 1000\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 4000 | 800\n" +
        "Resin | Standard (1.15) | 0 | 4000\n" +
        "Resin | Standard (1.15) | 50 | 3500\n" +
        "Resin | Standard (1.15) | 500 | 3200\n" +
        "Resin | Standard (1.15) | 1000 | 3000\n" +
        "Resin | Standard (1.15) | 2000 | 2800\n" +
        "Resin | Like ABS (1.12), Trong suốt (1.10) | 0 | 4600\n" +
        "Resin | Like ABS (1.12), Trong suốt (1.10) | 50 | 4000\n" +
        "Resin | Like ABS (1.12), Trong suốt (1.10) | 500 | 3600\n" +
        "Resin | Like ABS (1.12), Trong suốt (1.10) | 1000 | 3400\n" +
        "Resin | Like ABS (1.12), Trong suốt (1.10) | 2000 | 3200";

    /// <summary>
    /// The default price list before clear resin was added. Replaced by <see cref="DefaultPrintPriceTable"/> when unchanged.
    /// </summary>
    internal const string PreviousDefaultPrintPriceTable =
        "# Kiểu in | Vật liệu (cách nhau dấu phẩy, tỉ trọng g/cm³ trong ngoặc) | Từ gram | Giá/gram\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 0 | 1500\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 1000 | 1200\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 2000 | 1000\n" +
        "FDM | PLA (1.24), PETG (1.27), ABS (1.05) | 4000 | 800\n" +
        "Resin | Standard (1.15) | 0 | 4000\n" +
        "Resin | Standard (1.15) | 50 | 3500\n" +
        "Resin | Standard (1.15) | 500 | 3200\n" +
        "Resin | Standard (1.15) | 1000 | 3000\n" +
        "Resin | Standard (1.15) | 2000 | 2800\n" +
        "Resin | Like ABS (1.12) | 0 | 4600\n" +
        "Resin | Like ABS (1.12) | 50 | 4000\n" +
        "Resin | Like ABS (1.12) | 500 | 3600\n" +
        "Resin | Like ABS (1.12) | 1000 | 3400\n" +
        "Resin | Like ABS (1.12) | 2000 | 3200";

    /// <summary>
    /// The default price list before materials were grouped by technology (one technology per resin type).
    /// Replaced by <see cref="DefaultPrintPriceTable"/> when the storefront layout is upgraded.
    /// </summary>
    internal const string LegacyPrintPriceTable =
        "FDM | PLA, PETG, ABS | 0 | 1500\n" +
        "FDM | PLA, PETG, ABS | 1000 | 1200\n" +
        "FDM | PLA, PETG, ABS | 2000 | 1000\n" +
        "FDM | PLA, PETG, ABS | 4000 | 800\n" +
        "Resin Standard | Resin tiêu chuẩn | 0 | 4000\n" +
        "Resin Standard | Resin tiêu chuẩn | 50 | 3500\n" +
        "Resin Standard | Resin tiêu chuẩn | 500 | 3200\n" +
        "Resin Standard | Resin tiêu chuẩn | 1000 | 3000\n" +
        "Resin Standard | Resin tiêu chuẩn | 2000 | 2800\n" +
        "Resin Like ABS | Resin dẻo dai như ABS | 0 | 4600\n" +
        "Resin Like ABS | Resin dẻo dai như ABS | 50 | 4000\n" +
        "Resin Like ABS | Resin dẻo dai như ABS | 500 | 3600\n" +
        "Resin Like ABS | Resin dẻo dai như ABS | 1000 | 3400\n" +
        "Resin Like ABS | Resin dẻo dai như ABS | 2000 | 3200";
}
