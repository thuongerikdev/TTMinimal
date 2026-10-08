#nullable enable

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Catalog;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Core.Seo;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Admin product overview grouped the way the studio thinks about its catalog: tool keys, each shop category,
/// products without a category and the technical products the shop uses on its own. Replaces the flat core grid
/// as the "Products" menu entry (the core grid stays reachable via <c>/admin/product/list?full=1</c>).
/// </summary>
public class StudioProductsController : AdminController
{
    private const int ThumbnailSize = 128;
    private const int MaxProducts = 2000;
    private const string NoPictureWarning = "Chưa có ảnh";
    private const string MissingPriceWarning = "Chưa có giá";

    private static readonly CultureInfo _vi = CultureInfo.GetCultureInfo("vi-VN");

    // Products the shop adds to carts by itself; they must stay published and are never sold directly.
    private static readonly Dictionary<string, (string Usage, string Controller, string LinkText)> _systemProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        [PrintOrderService.PrintProductSku] = ("Dòng giỏ hàng của đơn in 3D (giá lấy từ đơn in).", "PrintJob", "Đơn in 3D"),
        [Split3DUpgradeService.UpgradeProductSku] = ("Dòng giỏ hàng khi khách nâng cấp gói key.", "Split3D", "Key bản quyền")
    };

    private static readonly string[] _tones = ["mint", "yellow", "lilac", "sky", "pink"];

    private readonly SmartDbContext _db;
    private readonly IMediaService _mediaService;
    private readonly IUrlService _urlService;

    public StudioProductsController(SmartDbContext db, IMediaService mediaService, IUrlService urlService)
    {
        _db = db;
        _mediaService = mediaService;
        _urlService = urlService;
    }

    [Permission(Permissions.Catalog.Product.Read)]
    public async Task<IActionResult> Index()
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(x => !x.Deleted && !x.IsSystemProduct)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Take(MaxProducts)
            .ToListAsync();

        var productIds = products.Select(x => x.Id).ToArray();
        var prices = products.ToDictionary(x => x.Id, x => x.Price);

        var categories = (await _db.Categories
            .AsNoTracking()
            .Where(x => !x.Deleted)
            .ToListAsync())
            .ToDictionary(x => x.Id);

        var mappings = (await _db.ProductCategories
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.ProductId, x.CategoryId, x.DisplayOrder })
            .ToListAsync())
            .Where(x => categories.ContainsKey(x.CategoryId))
            .ToLookup(x => x.ProductId);

        var addonProducts = await _db.Split3DAddonProducts()
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync();
        var addons = (await _db.Split3DAddons().AsNoTracking().ToListAsync()).ToDictionary(x => x.Id);

        var attributes = (await _db.ProductVariantAttributes
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.ProductId, x.ProductAttribute.Name, x.AttributeControlTypeId })
            .ToListAsync())
            .ToLookup(x => x.ProductId);

        var files = (await _mediaService.GetFilesByIdsAsync(
            products.Where(x => x.MainPictureId > 0).Select(x => x.MainPictureId!.Value).Distinct().ToArray()))
            .ToDictionary(x => x.Id);

        await _urlService.PrefetchUrlRecordsAsync(nameof(Product), [0], productIds);

        var canEdit = await Services.Permissions.AuthorizeAsync(Permissions.Catalog.Product.Update);
        var canConfigure = await Services.Permissions.AuthorizeAsync(Permissions.Configuration.Module.Read);
        var canEditCategories = await Services.Permissions.AuthorizeAsync(Permissions.Catalog.Category.Read);

        var model = new StudioProductListModel { CanEdit = canEdit };
        var rows = new Dictionary<int, StudioProductRowModel>();

        foreach (var product in products)
        {
            rows[product.Id] = await CreateRowAsync(product, files, attributes[product.Id].Select(x => (x.Name, x.AttributeControlTypeId)).ToList());
        }

        // 1. Tool keys (one group per add-on).
        var toolProductIds = new HashSet<int>();
        foreach (var addonGroup in addonProducts.GroupBy(x => x.AddonId).OrderBy(x => addons.Get(x.Key)?.DisplayOrder ?? int.MaxValue))
        {
            var addon = addons.Get(addonGroup.Key);
            var group = new StudioProductGroupModel
            {
                Key = "tool-" + addonGroup.Key,
                Eyebrow = "Công cụ 3D ›",
                Title = addon?.Name ?? "Gói key",
                Description = "Gói key phần mềm. Khách mua xong nhận key qua email; thời hạn và số máy chỉnh trong trang quản lý công cụ.",
                Icon = "bi bi-key",
                Tone = "lilac"
            };

            if (addon != null && canConfigure)
            {
                group.Links.Add(new() { Text = "Quản lý gói", Url = Url.Action("Edit", "Split3DAddon", new { id = addon.Id })!, Icon = "bi bi-sliders" });
            }

            group.Links.Add(new() { Text = "Xem trên web", Url = Url.RouteUrl(StudioStorefrontSetup.ToolsRouteName)!, External = true });

            foreach (var mapping in addonGroup.DistinctBy(x => x.ProductId))
            {
                if (rows.TryGetValue(mapping.ProductId, out var row) && toolProductIds.Add(mapping.ProductId))
                {
                    // Packages are sold on the tools page, which shows no product pictures.
                    row.Warnings.Remove(NoPictureWarning);
                    group.Products.Add(row);
                }
            }

            group.Products = [.. group.Products.OrderBy(x => prices[x.Id])];
            model.Groups.Add(group);
        }

        // 2. Shop categories, in the order of the category tree.
        var categoryGroups = new Dictionary<int, StudioProductGroupModel>();
        var uncategorized = new List<StudioProductRowModel>();
        var system = new List<StudioProductRowModel>();

        foreach (var product in products)
        {
            var row = rows[product.Id];
            if (toolProductIds.Contains(product.Id))
            {
                continue;
            }

            if (product.Sku != null && _systemProducts.ContainsKey(product.Sku))
            {
                system.Add(row);
                continue;
            }

            var productMappings = mappings[product.Id].ToList();
            if (productMappings.Count == 0)
            {
                row.Warnings.Add("Chưa có danh mục");
                uncategorized.Add(row);
                continue;
            }

            var primary = productMappings[0];
            if (productMappings.Count > 1)
            {
                row.Badges.Add("Cũng ở: " + string.Join(", ", productMappings.Skip(1).Select(x => categories[x.CategoryId].Name).Distinct()));
            }

            if (!categoryGroups.TryGetValue(primary.CategoryId, out var group))
            {
                var category = categories[primary.CategoryId];
                var path = GetPath(category, categories);
                var slug = await category.GetActiveSlugAsync();

                group = new StudioProductGroupModel
                {
                    Key = "cat-" + category.Id,
                    Title = category.Name,
                    Eyebrow = path.Count > 1 ? string.Join(" › ", path.Take(path.Count - 1).Select(x => x.Name)) + " ›" : "Danh mục",
                    Description = category.Published ? null : "Danh mục này đang ẩn trên web.",
                    Icon = "bi bi-folder2-open"
                };

                if (slug.HasValue())
                {
                    group.Links.Add(new() { Text = "Xem trên web", Url = Url.RouteUrl("Category", new { SeName = slug })!, External = true });
                }

                if (canEditCategories)
                {
                    group.Links.Add(new() { Text = "Sửa danh mục", Url = Url.Action("Edit", "Category", new { id = category.Id })!, Icon = "bi bi-pencil" });
                }

                categoryGroups[category.Id] = group;
            }

            group.Products.Add(row);
        }

        var categoryOrder = categories.Values
            .Select(x => (x.Id, Key: string.Join("/", GetPath(x, categories).Select(c => c.DisplayOrder.ToString("D6") + c.Name))))
            .ToDictionary(x => x.Id, x => x.Key);

        var toneIndex = 0;
        foreach (var group in categoryGroups.OrderBy(x => categoryOrder[x.Key], StringComparer.Ordinal).Select(x => x.Value))
        {
            group.Tone = _tones[toneIndex++ % _tones.Length];
            model.Groups.Add(group);
        }

        // 3. Products without a category: customers can only reach them by search or direct link.
        if (uncategorized.Count > 0)
        {
            model.Groups.Add(new StudioProductGroupModel
            {
                Key = "uncategorized",
                Title = "Chưa xếp danh mục",
                Eyebrow = "Cần sắp xếp",
                Description = "Khách khó tìm thấy các sản phẩm này. Mở sản phẩm › tab Danh mục để gán vào một nhóm.",
                Icon = "bi bi-question-circle",
                Tone = "warn",
                Products = uncategorized
            });
        }

        // 4. Technical products: listed last and collapsed, they only need attention if something breaks.
        if (system.Count > 0)
        {
            foreach (var row in system)
            {
                var info = _systemProducts[row.Sku!];
                row.IsSystem = true;
                row.Warnings.Clear();
                row.Badges.Insert(0, info.Usage);
            }

            var systemGroup = new StudioProductGroupModel
            {
                Key = "system",
                Title = "Sản phẩm hệ thống",
                Eyebrow = "Tự động",
                Description = "Shop tự dùng các sản phẩm này khi khách đặt in hoặc nâng cấp key. Không bán trực tiếp — đừng xóa, ẩn hay đổi SKU.",
                Icon = "bi bi-gear",
                Tone = "gray",
                Collapsed = true,
                Products = system
            };

            if (canConfigure)
            {
                foreach (var info in system.Select(x => _systemProducts[x.Sku!]).DistinctBy(x => x.Controller))
                {
                    systemGroup.Links.Add(new() { Text = info.LinkText, Url = Url.Action("List", info.Controller)!, Icon = "bi bi-arrow-right" });
                }
            }

            model.Groups.Add(systemGroup);
        }

        var all = model.Groups.SelectMany(x => x.Products).ToList();
        model.TotalCount = all.Count;
        model.PublishedCount = all.Count(x => x.Published && !x.IsSystem);
        model.HiddenCount = all.Count(x => !x.Published && !x.IsSystem);
        model.AttentionCount = all.Count(x => x.Warnings.Count > 0);

        return View(model);
    }

    /// <summary>
    /// Shows or hides a product in the shop (the switch on each row).
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Catalog.Product.Update)]
    public async Task<IActionResult> SetPublished(int id, bool published)
    {
        var product = await _db.Products.FindByIdAsync(id);
        if (product == null || product.Deleted)
        {
            return NotFound();
        }

        if (product.Sku != null && _systemProducts.ContainsKey(product.Sku))
        {
            return BadRequest();
        }

        if (product.Published != published)
        {
            product.Published = published;
            await _db.SaveChangesAsync();
        }

        return Json(new { success = true, published = product.Published });
    }

    /// <summary>
    /// Changes the selling price of a product (the inline price field on each row).
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Catalog.Product.Update)]
    public async Task<IActionResult> SetPrice(int id, string? price)
    {
        var product = await _db.Products.FindByIdAsync(id);
        if (product == null || product.Deleted)
        {
            return NotFound();
        }

        if (IsSystemSku(product.Sku) || product.CallForPrice)
        {
            return BadRequest();
        }

        // VND has no minor unit: accept "150.000", "150,000" or "150000 ₫".
        var digits = new string((price ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0 || digits.Length > 12 || !decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return BadRequest();
        }

        if (product.Price != value)
        {
            product.Price = value;
            await _db.SaveChangesAsync();
        }

        return Json(new
        {
            success = true,
            price = product.Price,
            priceText = FormatPrice(product),
            missingPrice = IsMissingPrice(product)
        });
    }

    private async Task<StudioProductRowModel> CreateRowAsync(
        Product product,
        Dictionary<int, MediaFileInfo> files,
        List<(string Name, int ControlTypeId)> attributes)
    {
        var file = product.MainPictureId > 0 ? files.Get(product.MainPictureId.Value) : null;
        var slug = await product.GetActiveSlugAsync();
        var row = new StudioProductRowModel
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            ThumbUrl = file != null ? _mediaService.GetUrl(file, ThumbnailSize, null, false) : null,
            Published = product.Published,
            EditUrl = Url.Action("Edit", "Product", new { id = product.Id })!,
            ShopUrl = slug.HasValue() ? Url.RouteUrl("Product", new { SeName = slug }) : null,
            UpdatedText = Services.DateTimeHelper.ConvertToUserTime(product.UpdatedOnUtc, DateTimeKind.Utc).ToString("dd/MM/yyyy", _vi),
            SearchText = Simplify($"{product.Name} {product.Sku} {product.ManufacturerPartNumber} {product.Gtin}")
        };

        row.PriceText = FormatPrice(product);
        row.Price = product.Price;
        row.PriceEditable = !product.CallForPrice && !IsSystemSku(product.Sku);

        switch (product.ManageInventoryMethod)
        {
            case ManageInventoryMethod.ManageStock:
                row.StockText = product.StockQuantity > 0 ? $"Còn {product.StockQuantity:N0}" : "Hết hàng";
                row.StockTone = product.StockQuantity <= 0 ? "out" : product.StockQuantity <= product.MinStockQuantity ? "low" : "ok";
                if (product.StockQuantity <= 0)
                {
                    row.Warnings.Add("Hết hàng");
                }
                break;
            case ManageInventoryMethod.ManageStockByAttributes:
                row.StockText = "Theo từng tuỳ chọn";
                break;
            default:
                row.StockText = "Luôn sẵn";
                break;
        }

        if (file == null)
        {
            row.Warnings.Add(NoPictureWarning);
        }

        if (IsMissingPrice(product))
        {
            row.Warnings.Add(MissingPriceWarning);
        }

        if (product.ProductType == ProductType.GroupedProduct)
        {
            row.Badges.Add("Nhóm sản phẩm");
        }
        else if (product.ProductType == ProductType.BundledProduct)
        {
            row.Badges.Add("Combo");
        }

        var customizable = attributes.Any(x => StudioCustomProducts.IsCustomizable((AttributeControlType)x.ControlTypeId));
        if (customizable)
        {
            row.Badges.Add("Cá nhân hoá");
        }

        var options = attributes
            .Where(x => !StudioCustomProducts.IsCustomizable((AttributeControlType)x.ControlTypeId))
            .Select(x => x.Name)
            .Distinct()
            .ToList();
        if (options.Count > 0)
        {
            row.Badges.Add("Tuỳ chọn: " + string.Join(", ", options));
        }

        if (product.ShowOnHomePage)
        {
            row.Badges.Add("Trang chủ");
        }

        if (!product.VisibleIndividually)
        {
            row.Badges.Add("Không hiện riêng");
        }

        return row;
    }

    private static bool IsSystemSku(string? sku)
        => sku != null && _systemProducts.ContainsKey(sku);

    private static string FormatPrice(Product product)
        => product.CallForPrice
            ? "Liên hệ"
            : product.Price > 0 ? product.Price.ToString("#,##0", _vi) + " ₫" : "Tự tính";

    private static bool IsMissingPrice(Product product)
        => product.Price <= 0 && !product.CallForPrice && !product.CustomerEntersPrice && !IsSystemSku(product.Sku);

    private static List<Category> GetPath(Category category, Dictionary<int, Category> categories)
    {
        var path = new List<Category>();
        var current = category;

        while (current != null && path.Count < 20 && !path.Contains(current))
        {
            path.Insert(0, current);
            current = current.ParentId is int parentId && parentId > 0 ? categories.Get(parentId) : null;
        }

        return path;
    }

    /// <summary>
    /// Lower case without Vietnamese diacritics, so "chau" finds "Chậu".
    /// </summary>
    internal static string Simplify(string value)
    {
        var normalized = value.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}
