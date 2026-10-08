#nullable enable

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Data;
using Smartstore.Split3D.Configuration;
using Smartstore.Split3D.Filters;
using Smartstore.Web.Components;

namespace Smartstore.Split3D.Components;

/// <summary>
/// "File in 3D" card on the admin order page (zone order_edit_top, see <see cref="OrderPrintFilesFilter"/>): every
/// line of the order designed in the 3D designer, with buttons that rebuild its print files in the browser from the
/// saved design (studio-export.js): the frame with pockets and the content pieces, each printed apart in its color,
/// with the print settings (<see cref="PrintFileSettings"/>). Lines of designer products without a design code
/// (ordered before the codes existed) are listed too, so the studio knows to ask the customer.
/// </summary>
public partial class OrderPrintFilesViewComponent : SmartViewComponent
{
    private readonly SmartDbContext _db;
    private readonly IModuleCatalog _moduleCatalog;
    private readonly PrintFileSettings _settings;

    public OrderPrintFilesViewComponent(SmartDbContext db, IModuleCatalog moduleCatalog, PrintFileSettings settings)
    {
        _db = db;
        _moduleCatalog = moduleCatalog;
        _settings = settings;
    }

    [GeneratedRegex("TT3D-([A-Z2-7]{10})")]
    private static partial Regex CodeRegex();

    public async Task<IViewComponentResult> InvokeAsync(int orderId)
    {
        var lines = await _db.OrderItems
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Quantity, x.AttributeDescription, ProductName = x.Product.Name, ProductSku = x.Product.Sku })
            .ToListAsync();

        var coded = lines
            .Select(x => new { Line = x, Code = CodeRegex().Match(x.AttributeDescription ?? string.Empty) is { Success: true } m ? m.Groups[1].Value : null })
            .Where(x => x.Code != null || (x.Line.ProductSku != null && StudioCustomProducts.DesignProducts.ContainsKey(x.Line.ProductSku)))
            .ToList();

        if (coded.Count == 0)
        {
            return Empty();
        }

        var codes = coded.Where(x => x.Code != null).Select(x => x.Code!).Distinct().ToArray();
        var designs = await _db.Split3DDesigns()
            .AsNoTracking()
            .Where(x => codes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, x => x.SpecJson);

        var model = new OrderPrintFilesModel
        {
            OrderNumber = await _db.Orders.Where(x => x.Id == orderId).Select(x => x.OrderNumber).FirstOrDefaultAsync() ?? orderId.ToString(),
            Settings = _settings
        };

        var items = new List<object>();
        var no = 0;
        foreach (var x in coded)
        {
            no++;
            JsonElement? spec = null;
            string? text = null;
            if (x.Code != null && designs.TryGetValue(x.Code, out var json))
            {
                using var doc = JsonDocument.Parse(json);
                spec = doc.RootElement.Clone();
                text = spec.Value.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            }

            model.Items.Add(new OrderPrintFileItem
            {
                No = no,
                ProductName = x.Line.ProductName,
                Text = text,
                Quantity = x.Line.Quantity,
                Code = x.Code != null ? Split3DDesign.DisplayPrefix + x.Code : null,
                HasSpec = spec != null,
                Kind = spec?.TryGetProperty("kind", out var k) == true && k.ValueKind == JsonValueKind.String ? k.GetString() : null
            });

            items.Add(new { no, name = x.Line.ProductName, text = text ?? string.Empty, qty = x.Line.Quantity, spec });
        }

        // "<" never appears outside JSON strings, where < means the same: the JSON cannot close the script element.
        model.ConfigJson = JsonSerializer.Serialize(new
        {
            order = model.OrderNumber,
            saveUrl = Url.Action("SaveSettings", "PrintFiles", new { area = "Admin" }),
            fonts = TextListFilter.ShopFonts(_moduleCatalog, Url),
            items
        }).Replace("<", "\\u003c");

        return View(model);
    }
}
