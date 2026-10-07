using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Data;
using Smartstore.Core.Widgets;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// On the product page of a product with the 3D designer (<see cref="StudioCustomProducts.DesignProducts"/>: name
/// plate, class board, keycap) adds the JSON config studio.js reads: the live 3D preview with its design panel (kind),
/// the hidden design summary and length attributes, the shop's fonts, and for list products a table dialog to order a
/// whole list at once (one row per text with its own colors, quantity and note, each row its own cart line).
/// Registered for Product/ProductDetails, see Startup.
/// </summary>
public class TextListFilter : IAsyncActionFilter
{
    private readonly SmartDbContext _db;
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly StudioSettings _studioSettings;
    private readonly IModuleCatalog _moduleCatalog;

    private static readonly HashSet<string> _fontExtensions = new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf", ".woff", ".woff2" };

    public TextListFilter(SmartDbContext db, Lazy<IWidgetProvider> widgetProvider, StudioSettings studioSettings, IModuleCatalog moduleCatalog)
    {
        _db = db;
        _widgetProvider = widgetProvider;
        _studioSettings = studioSettings;
        _moduleCatalog = moduleCatalog;
    }

    /// <summary>
    /// Font files of the shop in <c>wwwroot/studio/fonts</c> (ttf, otf, woff, woff2) for the name plate designer.
    /// The file name without extension is the font name shown to the customer.
    /// </summary>
    private object[] ShopFonts(IUrlHelper url)
    {
        var webRoot = _moduleCatalog.GetModuleByAssembly(GetType().Assembly)?.WebRoot;
        var contents = webRoot?.GetDirectoryContents(StudioAssets.FontFolder);
        if (url == null || contents == null || !contents.Exists)
        {
            return [];
        }

        return contents
            .Where(x => !x.IsDirectory && _fontExtensions.Contains(Path.GetExtension(x.Name)))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object)new
            {
                name = Path.GetFileNameWithoutExtension(x.Name),
                url = url.Content($"~/Modules/Smartstore.Split3D/{StudioAssets.FontFolder}/{Uri.EscapeDataString(x.Name)}?v={StudioAssets.Version}")
            })
            .ToArray();
    }

    // Page of the class board designer, linked from the ready-made boards ("Tự thiết kế").
    private async Task<string> DesignerUrlAsync(IUrlHelper url)
    {
        if (url == null)
        {
            return null;
        }

        var slug = await _db.Products
            .Where(x => x.Sku == StudioCustomProducts.ClassBoardSku && x.Published && !x.Deleted)
            .SelectMany(x => _db.UrlRecords.Where(u => u.EntityName == "Product" && u.EntityId == x.Id && u.IsActive && u.LanguageId == 0))
            .Select(u => u.Slug)
            .FirstOrDefaultAsync();

        return slug != null ? url.Content("~/" + slug) : null;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments.TryGetValue("productId", out var value) && value is int productId)
        {
            var sku = await _db.Products
                .Where(x => x.Id == productId)
                .Select(x => x.Sku)
                .FirstOrDefaultAsync();

            if (sku != null && StudioCustomProducts.DesignProducts.TryGetValue(sku, out var list))
            {
                var attribute = await _db.ProductVariantAttributes
                    .Where(x => x.ProductId == productId
                        && x.AttributeControlTypeId == (int)AttributeControlType.TextBox
                        && x.ProductAttribute.Name != StudioCustomProducts.LengthAttributeName)
                    .OrderBy(x => x.DisplayOrder)
                    .Select(x => new { x.Id, x.ProductAttributeId })
                    .FirstOrDefaultAsync();

                if (attribute != null)
                {
                    // The 3D designer writes its choices into a hidden text attribute, so they reach cart and order.
                    var design = await StudioCustomProducts.EnsureDesignAttributeAsync(_db, productId);
                    var designControl = design != null ? ProductVariantQueryItem.CreateKey(productId, 0, design.ProductAttributeId, design.Id) : null;
                    if (designControl != null)
                    {
                        _widgetProvider.Value.RegisterWidget("end", new HtmlWidget($"<style>.form-group.choice:has(#{designControl}) {{ display: none; }}</style>"));
                    }

                    // Free length (slider) instead of fixed sizes, priced by NameplateLengthPriceCalculator.
                    object length = null;
                    if (list.Length)
                    {
                        var range = StudioCustomProducts.LengthRange(list.Kind, _studioSettings);
                        var lengthAttribute = await StudioCustomProducts.EnsureLengthAttributeAsync(_db, productId);
                        var lengthControl = ProductVariantQueryItem.CreateKey(productId, 0, lengthAttribute.ProductAttributeId, lengthAttribute.Id);
                        _widgetProvider.Value.RegisterWidget("end", new HtmlWidget($"<style>.form-group.choice:has(#{lengthControl}) {{ display: none; }}</style>"));
                        length = new
                        {
                            control = lengthControl,
                            min = range.Min,
                            max = range.Max,
                            @base = range.Base,
                            percent = range.PercentPerCm,
                            price = await _db.Products.Where(x => x.Id == productId).Select(x => x.Price).FirstOrDefaultAsync()
                        };
                    }

                    var url = (context.Controller as Controller)?.Url;
                    var json = JsonSerializer.Serialize(new
                    {
                        control = ProductVariantQueryItem.CreateKey(productId, 0, attribute.ProductAttributeId, attribute.Id),
                        title = list.Title,
                        item = list.Item,
                        cartUrl = url?.RouteUrl("ShoppingCart"),
                        xlsxSrc = url?.Content(StudioAssets.XlsxScript),
                        kind = list.Kind,
                        list = list.List,
                        previewSrc = url?.Content(StudioAssets.NameplateScript),
                        designsSrc = url?.Content(StudioAssets.DesignsScript),
                        designControl,
                        length,
                        fonts = ShopFonts(url),
                        qrUrl = list.Kind == "qr" ? url?.Content("~/studio/qr") : null,
                        theme = list.Theme,
                        customUrl = list.Theme != null ? await DesignerUrlAsync(url) : null
                    });

                    // "<" is escaped by the serializer, so the JSON cannot close the script element.
                    _widgetProvider.Value.RegisterWidget("end", new HtmlWidget($"<script type=\"application/json\" data-tt-textlist>{json}</script>"));
                }
            }
        }

        await next();
    }
}
