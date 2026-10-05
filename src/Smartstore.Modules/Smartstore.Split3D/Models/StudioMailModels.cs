using Microsoft.AspNetCore.Mvc.Rendering;

namespace Smartstore.Split3D.Models;

/// <summary>
/// Admin page "TT Minimal Studio &gt; Email": look of all store emails plus the key email message template.
/// </summary>
[LocalizedDisplay("Plugins.Split3D.Mail.Fields.")]
public class StudioMailModel : ModelBase
{
    [LocalizedDisplay("*BrandingEnabled")]
    public bool BrandingEnabled { get; set; }

    [LocalizedDisplay("*Layout")]
    public string Layout { get; set; }

    [LocalizedDisplay("*HeaderStyle")]
    public string HeaderStyle { get; set; }

    [LocalizedDisplay("*ShowLogo")]
    public bool ShowLogo { get; set; }

    [LocalizedDisplay("*AccentColor")]
    public string AccentColor { get; set; }

    [LocalizedDisplay("*HighlightColor")]
    public string HighlightColor { get; set; }

    [LocalizedDisplay("*BackgroundColor")]
    public string BackgroundColor { get; set; }

    [LocalizedDisplay("*InkColor")]
    public string InkColor { get; set; }

    [LocalizedDisplay("*HeaderNote")]
    public string HeaderNote { get; set; }

    [LocalizedDisplay("*FooterText")]
    public string FooterText { get; set; }

    [LocalizedDisplay("*ShowContact")]
    public bool ShowContact { get; set; }

    [LocalizedDisplay("*SendEmail")]
    public bool SendEmail { get; set; }

    [LocalizedDisplay("*AttachInstaller")]
    public bool AttachInstaller { get; set; }

    [LocalizedDisplay("*TemplateActive")]
    public bool TemplateActive { get; set; }

    [LocalizedDisplay("*Subject")]
    public string Subject { get; set; }

    [UIHint("Liquid")]
    [LocalizedDisplay("*Body")]
    public string Body { get; set; }

    [LocalizedDisplay("*EmailAccountId")]
    public int EmailAccountId { get; set; }

    [LocalizedDisplay("*Bcc")]
    public string Bcc { get; set; }

    [LocalizedDisplay("*TestEmail")]
    public string TestEmail { get; set; }

    public int TemplateId { get; set; }
    public string DefaultAccountEmail { get; set; }
    public List<SelectListItem> EmailAccounts { get; set; } = [];
    public List<SelectListItem> PreviewTemplates { get; set; } = [];
}
