using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Turns the plain text typed into an addon description textarea into HTML that keeps its layout:
/// line breaks, blank lines (paragraphs), leading spaces (indent), "# heading", "- bullet", "1. item",
/// "**bold**" and links. Text that already is HTML (e.g. from the product editor) is returned unchanged.
/// </summary>
public static partial class StudioDescription
{
    const int MaxIndent = 6;

    [GeneratedRegex(@"<\s*(p|br|ul|ol|li|div|h[1-6]|table|strong|b|em|i|a|span)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"^(#{1,3})\s+(.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^([-*•+–])\s+(.*)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^(\d{1,3}[.)])\s+(.*)$")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"\bhttps?://[^\s<]+[^\s<.,;:!?)\]]")]
    private static partial Regex UrlRegex();

    public static string ToHtml(string text)
    {
        if (text.IsEmpty() || HtmlTagRegex().IsMatch(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 64);
        var paragraphIndent = -1;

        void CloseParagraph()
        {
            if (paragraphIndent >= 0)
            {
                sb.Append("</p>");
                paragraphIndent = -1;
            }
        }

        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                CloseParagraph();
                continue;
            }

            var indent = Indent(line);
            line = line.TrimStart();

            Match match;
            if ((match = HeadingRegex().Match(line)).Success)
            {
                CloseParagraph();
                sb.Append(match.Groups[1].Length == 1 ? "<h3>" : "<h4>")
                    .Append(Inline(match.Groups[2].Value))
                    .Append(match.Groups[1].Length == 1 ? "</h3>" : "</h4>");
            }
            else if ((match = BulletRegex().Match(line)).Success || (match = NumberRegex().Match(line)).Success)
            {
                CloseParagraph();
                var marker = match.Groups[1].Value.Length == 1 && !char.IsDigit(match.Groups[1].Value[0]) ? "•" : match.Groups[1].Value;
                sb.Append("<p class=\"tt-desc-li\"").Append(IndentStyle(indent)).Append('>')
                    .Append("<span class=\"tt-desc-mark\">").Append(WebUtility.HtmlEncode(marker)).Append("</span>")
                    .Append("<span>").Append(Inline(match.Groups[2].Value)).Append("</span></p>");
            }
            else if (paragraphIndent == indent)
            {
                sb.Append("<br>").Append(Inline(line));
            }
            else
            {
                CloseParagraph();
                sb.Append("<p").Append(IndentStyle(indent)).Append('>').Append(Inline(line));
                paragraphIndent = indent;
            }
        }

        CloseParagraph();
        return sb.ToString();
    }

    /// <summary>
    /// Indent level of a line: two spaces or one tab per level.
    /// </summary>
    private static int Indent(string line)
    {
        var spaces = 0;
        foreach (var c in line)
        {
            if (c == ' ') spaces++;
            else if (c == '\t') spaces += 4;
            else if (c == ' ') spaces++;
            else break;
        }

        return Math.Min(spaces / 2, MaxIndent);
    }

    private static string IndentStyle(int indent)
        => indent > 0 ? $" style=\"--ind:{indent}\"" : string.Empty;

    private static string Inline(string text)
    {
        var html = WebUtility.HtmlEncode(text);
        html = BoldRegex().Replace(html, "<strong>$1</strong>");
        return UrlRegex().Replace(html, m => $"<a href=\"{m.Value}\" target=\"_blank\" rel=\"noopener\">{m.Value}</a>");
    }
}
