using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Data;
using Smartstore.Core.Widgets;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// On the product page of a personalized product (<see cref="StudioCustomProducts.TextListProducts"/>), lets the customer
/// order a whole list at once: studio.js reads the JSON config added here and opens a table dialog (one row per text
/// with its own colors, quantity and note) that adds every row to the cart as its own line. Name plates also get a
/// live 3D preview of the typed text and of any row of the list.
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

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments.TryGetValue("productId", out var value) && value is int productId)
        {
            var sku = await _db.Products
                .Where(x => x.Id == productId)
                .Select(x => x.Sku)
                .FirstOrDefaultAsync();

            if (sku != null && StudioCustomProducts.TextListProducts.TryGetValue(sku, out var list))
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
                    var design = list.Preview ? await StudioCustomProducts.EnsureDesignAttributeAsync(_db, productId) : null;
                    var designControl = design != null ? ProductVariantQueryItem.CreateKey(productId, 0, design.ProductAttributeId, design.Id) : null;
                    if (designControl != null)
                    {
                        _widgetProvider.Value.RegisterWidget("end", new HtmlWidget($"<style>.form-group.choice:has(#{designControl}) {{ display: none; }}</style>"));
                    }

                    // Free length (slider) instead of fixed sizes, priced by NameplateLengthPriceCalculator.
                    object length = null;
                    if (list.Preview)
                    {
                        var lengthAttribute = await StudioCustomProducts.EnsureLengthAttributeAsync(_db, productId);
                        var lengthControl = ProductVariantQueryItem.CreateKey(productId, 0, lengthAttribute.ProductAttributeId, lengthAttribute.Id);
                        _widgetProvider.Value.RegisterWidget("end", new HtmlWidget($"<style>.form-group.choice:has(#{lengthControl}) {{ display: none; }}</style>"));
                        length = new
                        {
                            control = lengthControl,
                            min = Math.Min(_studioSettings.NameplateMinLength, _studioSettings.NameplateMaxLength),
                            max = Math.Max(_studioSettings.NameplateMinLength, _studioSettings.NameplateMaxLength),
                            @base = _studioSettings.NameplateBaseLength,
                            percent = _studioSettings.NameplatePercentPerCm,
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
                        previewSrc = list.Preview ? url?.Content(StudioAssets.NameplateScript) : null,
                        designControl,
                        length,
                        fonts = list.Preview ? ShopFonts(url) : null
                    });

                    // "<" is escaped by the serializer, so the JSON cannot close the script element.
                    _widgetProvider.Value.RegisterWidget("end", new HtmlWidget($"<script type=\"application/json\" data-tt-textlist>{json}</script>"));
                }
            }
        }

        await next();
    }
}
