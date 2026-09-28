using Microsoft.AspNetCore.Html;

namespace Smartstore.Split3D;

/// <summary>
/// Inline stroke icons for the studio storefront views (24x24, currentColor).
/// </summary>
public static class StudioIcons
{
    private static readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cube"] = "<path d='M12 2.8 20.5 7.5v9L12 21.2 3.5 16.5v-9Z'/><path d='M3.8 7.6 12 12.2l8.2-4.6M12 12.2v8.8'/>",
        ["printer"] = "<rect x='3' y='3' width='18' height='18' rx='2.5'/><path d='M3 8h18M9 8v3.5h6V8M12 11.5v2'/><path d='M8 18h8l-1.4-3.2H9.4Z'/>",
        ["bag"] = "<path d='M5 8h14l-1.1 12.1a1 1 0 0 1-1 .9H7.1a1 1 0 0 1-1-.9Z'/><path d='M9 10V6.5a3 3 0 0 1 6 0V10'/>",
        ["puzzle"] = "<path d='M10 3.5a2 2 0 0 1 4 0V5h4a1 1 0 0 1 1 1v4h-1.5a2 2 0 0 0 0 4H19v4a1 1 0 0 1-1 1h-4v-1.5a2 2 0 0 0-4 0V19H6a1 1 0 0 1-1-1v-4h1.5a2 2 0 0 0 0-4H5V6a1 1 0 0 1 1-1h4Z'/>",
        ["pencil"] = "<path d='M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16Z'/><path d='m13.5 6.5 4 4'/>",
        ["wrench"] = "<path d='M14.5 6.5a4 4 0 0 0 5 5L12 19a2.1 2.1 0 0 1-3-3Z'/><path d='M14.5 6.5 17 4l3 3-2.5 2.5'/>",
        ["image"] = "<rect x='3' y='4' width='18' height='16' rx='2.5'/><circle cx='9' cy='10' r='1.8'/><path d='m21 16-5-5-9 9'/>",
        ["upload"] = "<path d='M12 15V4M7.5 8.5 12 4l4.5 4.5'/><path d='M4 14v4a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-4'/>",
        ["phone"] = "<path d='M5 4h3.5l1.6 4-2.2 1.4a11 11 0 0 0 6.7 6.7l1.4-2.2 4 1.6V19a1.5 1.5 0 0 1-1.6 1.5A16 16 0 0 1 3.5 5.6 1.5 1.5 0 0 1 5 4Z'/>",
        ["chat"] = "<path d='M4 5.5A1.5 1.5 0 0 1 5.5 4h13A1.5 1.5 0 0 1 20 5.5v9a1.5 1.5 0 0 1-1.5 1.5H10l-4.5 4V16h0A1.5 1.5 0 0 1 4 14.5Z'/><path d='M8 9h8M8 12h5'/>",
        ["facebook"] = "<path d='M14 21v-7.5h2.6l.4-3H14V8.6c0-.9.3-1.5 1.6-1.5H17V4.4a20 20 0 0 0-2.3-.1C12.4 4.3 11 5.7 11 8.2v2.3H8.5v3H11V21'/>",
        ["pin"] = "<path d='M12 21s-7-6.2-7-11.5a7 7 0 0 1 14 0C19 14.8 12 21 12 21Z'/><circle cx='12' cy='9.5' r='2.5'/>",
        ["mail"] = "<rect x='3' y='5' width='18' height='14' rx='2.5'/><path d='m4 7 8 6 8-6'/>",
        ["arrow"] = "<path d='M5 12h14M13 6l6 6-6 6'/>",
        ["check"] = "<path d='m5 12.5 4.5 4.5L19 7.5'/>",
        ["layers"] = "<path d='m12 3 9 5-9 5-9-5Z'/><path d='m3 13 9 5 9-5'/>",
        ["truck"] = "<path d='M3 6h11v10H3zM14 10h4l3 3v3h-7'/><circle cx='7' cy='17.5' r='1.8'/><circle cx='17' cy='17.5' r='1.8'/>",
        ["calc"] = "<rect x='5' y='3' width='14' height='18' rx='2.5'/><path d='M8 7h8M8.5 11h1M11.5 11h1M14.5 11h1M8.5 14h1M11.5 14h1M14.5 14h1M8.5 17h1M11.5 17h4'/>",
        ["clock"] = "<circle cx='12' cy='12' r='8.5'/><path d='M12 7.5V12l3 2'/>",
        ["spark"] = "<path d='M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5 18 18M6 18l2.5-2.5M15.5 8.5 18 6'/>",
        ["scissors"] = "<circle cx='6.5' cy='6.5' r='2.5'/><circle cx='6.5' cy='17.5' r='2.5'/><path d='M8.5 8 20 18M8.5 16 20 6'/>",
        ["ruler"] = "<path d='m3 16 13-13 5 5L8 21Z'/><path d='m7 12 2 2M10 9l2 2M13 6l2 2'/>"
    };

    public static IHtmlContent Get(string name, string cssClass = null)
    {
        var path = _paths.Get(name) ?? _paths["cube"];
        var css = cssClass.HasValue() ? $" class=\"{cssClass}\"" : string.Empty;

        return new HtmlString($"<svg{css} viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">{path}</svg>");
    }
}
