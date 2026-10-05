namespace Smartstore.Split3D.Configuration;

/// <summary>
/// Look of every email the shop sends (key emails, order emails, account emails).
/// Rendered by the TT Minimal theme's <c>Views/Shared/EmailTemplates/master.liquid</c> from the
/// <c>Studio</c> model part, see <see cref="Services.StudioMailService"/>.
/// </summary>
public class StudioMailSettings : ISettings
{
    /// <summary>
    /// Applies the studio colors and layout to all store emails. Off = Smartstore's default look.
    /// </summary>
    public bool BrandingEnabled { get; set; } = true;

    /// <summary>
    /// Overall layout: <c>pop</c> (cream page, outlined card with hard shadow) or <c>clean</c> (white page, soft card).
    /// </summary>
    public string Layout { get; set; } = "pop";

    /// <summary>
    /// Header band: <c>ink</c> (dark), <c>accent</c> (accent color) or <c>light</c> (page background).
    /// </summary>
    public string HeaderStyle { get; set; } = "ink";

    /// <summary>
    /// Shows the store logo image in the header instead of the brand name as text.
    /// </summary>
    public bool ShowLogo { get; set; }

    public string AccentColor { get; set; } = "#ff67bc";

    /// <summary>
    /// Background of highlighted boxes (license key, totals).
    /// </summary>
    public string HighlightColor { get; set; } = "#ffe348";

    public string BackgroundColor { get; set; } = "#fffbed";

    public string InkColor { get; set; } = "#20201f";

    /// <summary>
    /// Small text next to the brand name in the header. Empty = the studio tagline.
    /// </summary>
    public string HeaderNote { get; set; }

    /// <summary>
    /// Closing text above the footer, one paragraph per line.
    /// </summary>
    public string FooterText { get; set; } = "Cảm ơn bạn đã tin chọn TT Minimal. Cần hỗ trợ? Chỉ cần trả lời email này hoặc nhắn Zalo cho chúng mình.";

    /// <summary>
    /// Shows address, phone numbers, Zalo and Facebook (from the studio settings) in the footer.
    /// </summary>
    public bool ShowContact { get; set; } = true;
}
