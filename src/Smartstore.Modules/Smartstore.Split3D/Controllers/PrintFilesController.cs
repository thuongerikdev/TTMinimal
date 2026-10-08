#nullable enable

using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Security;
using Smartstore.Split3D.Configuration;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Saves the print settings of the "File in 3D" card on the admin order page as the defaults
/// (<see cref="PrintFileSettings"/>).
/// </summary>
public class PrintFilesController : AdminController
{
    private static readonly HashSet<string> _formats = new(StringComparer.Ordinal) { "3mf", "stl", "glb" };

    private readonly PrintFileSettings _settings;

    public PrintFilesController(PrintFileSettings settings)
    {
        _settings = settings;
    }

    [HttpPost]
    [Permission(Permissions.Order.Update)]
    public async Task<IActionResult> SaveSettings(string clearance, string pocket, string format)
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

        _settings.Clearance = Math.Round(c, 2);
        _settings.PocketPercent = Math.Round(p);
        _settings.Format = format;
        await Services.SettingFactory.SaveSettingsAsync(_settings);

        return Json(new { success = true });
    }
}
