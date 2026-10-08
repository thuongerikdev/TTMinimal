#nullable enable

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Data;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Saves the spec of a design from the 3D designer of a product page (studio.js, before a line goes into the cart)
/// and returns its code, which studio.js writes into the line's "Thiết kế" attribute. The admin order page rebuilds
/// the print files from the saved spec (see <see cref="Components.OrderPrintFilesViewComponent"/>).
/// </summary>
[Route("studio/design")]
public class StudioDesignController : PublicController
{
    // A large class board with every cell filled stays well below this.
    private const int MaxLength = 400_000;

    // Product kinds of the designer (studio-designs.js); anything else is not a design of this shop.
    private static readonly HashSet<string> _kinds = new(StringComparer.Ordinal) { "nameplate", "classboard", "qr", "keycap" };

    private readonly SmartDbContext _db;

    public StudioDesignController(SmartDbContext db)
    {
        _db = db;
    }

    [HttpPost("")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(MaxLength * 4)]
    public async Task<IActionResult> Save()
    {
        string json;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            json = await reader.ReadToEndAsync();
        }

        if (json.Length == 0 || json.Length > MaxLength)
        {
            return BadRequest();
        }

        string? kind;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return BadRequest();
            }

            kind = doc.RootElement.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            if (kind == null || !_kinds.Contains(kind))
            {
                return BadRequest();
            }
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        var code = Split3DDesign.CodeOf(json);
        if (!await _db.Split3DDesigns().AnyAsync(x => x.Code == code))
        {
            _db.Split3DDesigns().Add(new Split3DDesign
            {
                Code = code,
                Kind = kind,
                SpecJson = json,
                CreatedOnUtc = DateTime.UtcNow
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The same design saved by a parallel request is fine; anything else is a real failure.
                if (!await _db.Split3DDesigns().AsNoTracking().AnyAsync(x => x.Code == code))
                {
                    throw;
                }
            }
        }

        return Json(new { code = Split3DDesign.DisplayPrefix + code });
    }
}
