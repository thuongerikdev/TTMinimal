#nullable enable

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Split3D.Configuration;
using Smartstore.Utilities.Html;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Saves the print settings of the "File in 3D" card on the admin order page as the defaults
/// (<see cref="PrintFileSettings"/>) and the designs the studio changed in the card's editor.
/// </summary>
public partial class PrintFilesController : AdminController
{
    private static readonly HashSet<string> _formats = new(StringComparer.Ordinal) { "3mf", "stl", "glb" };
    private static readonly HashSet<string> _layouts = new(StringComparer.Ordinal) { "assembled", "beside", "split" };

    private readonly SmartDbContext _db;
    private readonly PrintFileSettings _settings;

    public PrintFilesController(SmartDbContext db, PrintFileSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    [GeneratedRegex("^TT3D-([A-Z2-7]{10})$")]
    private static partial Regex CodeRegex();

    [HttpPost]
    [Permission(Permissions.Order.Update)]
    public async Task<IActionResult> SaveSettings(string clearance, string pocket, string format, string? layout)
    {
        if (!double.TryParse(clearance, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) || c < 0 || c > 1)
        {
            return Json(new { success = false, message = "Khe lắp phải từ 0 đến 1 mm." });
        }

        if (!double.TryParse(pocket, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) || p < 10 || p > 100)
        {
            return Json(new { success = false, message = "Độ sâu hốc phải từ 10 đến 100%." });
        }

        if (format == null || !_formats.Contains(format))
        {
            return Json(new { success = false, message = "Định dạng không hợp lệ." });
        }

        if (layout != null && !_layouts.Contains(layout))
        {
            return Json(new { success = false, message = "Cách gộp file không hợp lệ." });
        }

        _settings.Clearance = Math.Round(c, 2);
        _settings.PocketPercent = Math.Round(p);
        _settings.Format = format;
        _settings.Layout = layout ?? _settings.Layout;
        await Services.SettingFactory.SaveSettingsAsync(_settings);

        return Json(new { success = true });
    }

    /// <summary>
    /// Puts a design changed in the card's editor on its order line: the "Thiết kế" attribute (summary + code) and
    /// the line's attribute description get the new design, the order a note naming both codes.
    /// </summary>
    /// <param name="orderItemId">The order line.</param>
    /// <param name="oldCode">Design code the line carries now, e.g. "TT3D-ABCDE23456".</param>
    /// <param name="newCode">Code of the changed design, already saved through studio/design.</param>
    /// <param name="summary">Readable summary of the changed design (as the product page writes it).</param>
    [HttpPost]
    [Permission(Permissions.Order.Update)]
    public async Task<IActionResult> UpdateDesign(int orderItemId, string oldCode, string newCode, string? summary)
    {
        var oldMatch = CodeRegex().Match(oldCode ?? string.Empty);
        var newMatch = CodeRegex().Match(newCode ?? string.Empty);
        if (!oldMatch.Success || !newMatch.Success)
        {
            return Json(new { success = false, message = "Mã thiết kế không hợp lệ." });
        }

        var raw = newMatch.Groups[1].Value;
        if (!await _db.Split3DDesigns().AnyAsync(x => x.Code == raw))
        {
            return Json(new { success = false, message = "Không tìm thấy thiết kế mới." });
        }

        var item = await _db.OrderItems
            .Include(x => x.Order)
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == orderItemId);
        if (item == null)
        {
            return Json(new { success = false, message = "Không tìm thấy dòng đơn hàng." });
        }

        // The attribute value that holds the old code ("<summary>\nMã file 3D: TT3D-…").
        var selection = item.AttributeSelection;
        var hit = selection.AttributesMap
            .SelectMany(x => x.Value.Select(v => new { AttributeId = x.Key, Value = v?.ToString() }))
            .FirstOrDefault(x => x.Value != null && x.Value.Contains(oldCode!, StringComparison.Ordinal));
        if (hit == null)
        {
            return Json(new { success = false, message = "Dòng đơn hàng không còn mang mã " + oldCode + "." });
        }

        var text = summary.HasValue()
            ? summary!.Trim() + "\nMã file 3D: " + newCode
            : hit.Value!.Replace(oldCode!, newCode, StringComparison.Ordinal);

        selection.RemoveAttribute(hit.AttributeId);
        selection.AddAttributeValue(hit.AttributeId, text);
        item.RawAttributes = selection.AsJson();

        // The description holds the value as ProductAttributeFormatter wrote it (HTML encoded, line breaks as <br />).
        var description = item.AttributeDescription ?? string.Empty;
        var oldHtml = HtmlUtility.ConvertPlainTextToHtml(hit.Value!.HtmlEncode());
        item.AttributeDescription = oldHtml.HasValue() && description.Contains(oldHtml, StringComparison.Ordinal)
            ? description.Replace(oldHtml, HtmlUtility.ConvertPlainTextToHtml(text.HtmlEncode()), StringComparison.Ordinal)
            : description.Replace(oldCode!, newCode, StringComparison.Ordinal);

        var user = Services.WorkContext.CurrentCustomer;
        _db.OrderNotes.Add(item.Order, $"Thiết kế của dòng \"{item.Product?.Name ?? item.ProductId.ToString()}\" được chỉnh trong admin ({user.Email ?? user.Username}): {oldCode} → {newCode}.");

        await _db.SaveChangesAsync();

        return Json(new { success = true, description = item.AttributeDescription });
    }
}
