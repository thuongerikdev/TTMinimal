using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;
using Smartstore.Core.Seo;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Personalized products the customer configures before ordering (name plate, class board, keycaps): text the
/// customer types plus color / size options. Created once by <see cref="StudioStorefrontSetup"/>; prices and options
/// are edited in the catalog afterwards and are never overwritten (<see cref="UpgradeAsync"/> only adds options that
/// a newer version of the designer needs).
/// </summary>
public static class StudioCustomProducts
{
    public const string CategoryName = "Quà tặng cá nhân hoá";
    public const string CategorySlug = "qua-tang-ca-nhan-hoa";

    // Filament colors of the design products (same hex as BOARD_COLORS in studio-designs.js where both have the color).
    // Existing products get the missing ones from UpgradeColorsAsync.
    private static readonly (string Name, string Color)[] _colors =
    [
        ("Trắng", "#ffffff"), ("Đen", "#20201f"), ("Xám", "#9e9e9a"), ("Đỏ", "#e5484d"), ("Cam", "#ff7a1a"), ("Vàng", "#ffe348"),
        ("Xanh lá", "#6cc644"), ("Mint", "#80e5cb"), ("Xanh trời", "#5ac8fa"), ("Xanh dương", "#2f6fe4"), ("Tím", "#9b6bf2"), ("Hồng", "#ff67bc")
    ];

    // Color choices of the design products that UpgradeColorsAsync completes.
    private static readonly string[] _colorAttributeNames = ["Màu nền", "Màu chữ", "Màu", "Màu ký tự"];

    private sealed record Option(string Name, decimal PriceAdjustment = 0, string Color = null, bool PreSelected = false);

    private sealed record Attribute(string Name, AttributeControlType ControlType, bool IsRequired, string TextPrompt, Option[] Options);

    private sealed record Spec(string Sku, string Name, string Slug, decimal Price, string ShortDescription, string FullDescription, Attribute[] Attributes);

    public const string ClassBoardSku = "TT-CLASSBOARD";
    public const string QrSku = "TT-QR";

    // Class board: text printed in place, or removable press-fit tiles the class can rearrange.
    private static readonly Attribute TileAttribute = new("Kiểu ô", AttributeControlType.RadioList, true, null,
    [
        new("Chữ in liền (cố định)", PreSelected: true), new(RemovableTilesOption, 120000)
    ]);

    /// <summary>
    /// Name of the removable tiles choice of "Kiểu ô". Its former press-fit / magnet variants are merged into it by
    /// <see cref="ApplyBoardTemplatesAsync"/> (the magnet tiles are no longer offered).
    /// </summary>
    public const string RemovableTilesOption = "Ô rời tháo lắp";

    // Key width (price per size) and legend color of keycaps. Declared before _products: static fields are
    // initialized in text order.
    private static readonly Attribute KeySizeAttribute = new("Kích cỡ phím", AttributeControlType.RadioList, true, null,
    [
        new("1u (phím chữ, số)", PreSelected: true), new("1,25u (Ctrl, Alt, Win)", 5000), new("1,5u (Tab, \\)", 8000),
        new("1,75u (Caps Lock)", 10000), new("2u (Backspace)", 15000), new("2,25u (Enter, Shift trái)", 18000),
        new("2,75u (Shift phải)", 25000), new("6,25u (phím cách)", 60000)
    ]);

    private static readonly Attribute LegendColorAttribute = new("Màu ký tự", AttributeControlType.Boxes, true, null, ColorOptions(1));

    // Profiles the keycap designer draws, added to the profile choice of existing keycaps.
    private static readonly Option[] _extraProfiles = [new("DSA"), new("SA", 5000)];

    private static readonly Spec[] _products =
    [
        new("TT-NAMEPLATE", "Bảng tên 3D theo yêu cầu", "bang-ten-3d-theo-yeu-cau", 150000,
            "Bảng tên in 3D chữ nổi: tên riêng, tên shop, bàn làm việc. Tự chọn chữ, màu và kích thước.",
            "<p>Bảng tên in 3D chữ nổi hai màu. Nhập nội dung chữ, chọn màu nền, màu chữ và kích thước — studio dựng file, gửi ảnh xem trước rồi mới in.</p>",
            [
                new("Nội dung chữ", AttributeControlType.TextBox, true, "Tên / chữ muốn in", []),
                new("Màu nền", AttributeControlType.Boxes, true, null, ColorOptions(0)),
                new("Màu chữ", AttributeControlType.Boxes, true, null, ColorOptions(1)),
                new(LengthAttributeName, AttributeControlType.TextBox, false, null, [])
            ]),
        // TT-KEYCAP is no longer sold (2026-10-07): not created anymore. An existing one keeps its designer
        // (DesignProducts) and options (UpgradeAsync) in case it is published again.
        new(ClassBoardSku, "Thời khoá biểu & sơ đồ lớp in 3D", "thoi-khoa-bieu-so-do-lop-in-3d", 350000,
            "Bảng thời khoá biểu hoặc sơ đồ chỗ ngồi của lớp, in 3D chữ nổi: tự nhập môn học / tên học sinh, xem trước 3D.",
            "<p>Một sản phẩm cho cả hai: <b>thời khoá biểu</b> (số ngày, số tiết sáng / chiều, tên môn từng ô) hoặc <b>sơ đồ lớp</b> "
            + "(số hàng, dãy, chỗ mỗi bàn, bàn giáo viên, tên học sinh). Chọn phông, màu, kiểu đế, lỗ treo hoặc chân đứng — xem trước 3D ngay "
            + "trên trang, studio gửi file xem trước rồi mới in. Giá theo chiều dài bảng.</p>",
            [
                new("Tên lớp / tiêu đề", AttributeControlType.TextBox, true, "Tiêu đề trên bảng (VD: Thời khoá biểu lớp 6A1)", []),
                new("Màu nền", AttributeControlType.Boxes, true, null, ColorOptions(0)),
                new("Màu chữ", AttributeControlType.Boxes, true, null, ColorOptions(1)),
                TileAttribute,
                new(LengthAttributeName, AttributeControlType.TextBox, false, null, [])
            ]),
        new(QrSku, "Mã QR in 3D theo yêu cầu", "ma-qr-in-3d-theo-yeu-cau", 120000,
            "Bảng mã QR in 3D: WiFi, chuyển khoản ngân hàng (VietQR), link menu / website, Zalo — để bàn, treo tường hoặc móc khoá.",
            "<p>Nhập nội dung (link, WiFi, số tài khoản, Zalo…), chọn kiểu mã, chữ dưới mã, màu, kích thước, chân đứng hoặc lỗ treo — "
            + "xem trước 3D ngay trên trang và quét thử từ màn hình. Mã đậm trên nền sáng để điện thoại quét được. Giá theo chiều dài.</p>",
            [
                new("Chữ trên bảng", AttributeControlType.TextBox, false, "Chữ dưới mã QR (VD: Quét để kết nối WiFi)", []),
                new("Màu nền", AttributeControlType.Boxes, true, null, ColorOptions(0)),
                new("Màu chữ", AttributeControlType.Boxes, true, null, ColorOptions(1)),
                new(LengthAttributeName, AttributeControlType.TextBox, false, null, [])
            ])
    ];

    /// <summary>
    /// A product with the 3D designer, see <see cref="Filters.TextListFilter"/>. Kind selects the 3D model and the
    /// design panel (studio-nameplate.js, studio-designs.js): "nameplate", "classboard" or "keycap". List: the text
    /// field can be filled from a list (one row per piece; Title heads the list dialog, Item names one row).
    /// Length: free length slider priced by <see cref="NameplateLengthPriceCalculator"/>.
    /// </summary>
    /// <param name="Theme">
    /// Fixed look of a ready-made class board (a theme key of studio-designs.js); the customer only types the name.
    /// <c>null</c>: the customer designs the look.
    /// </param>
    public sealed record DesignProduct(string Kind, string Title, string Item, bool List, bool Length, string Theme = null);

    /// <summary>
    /// Name of the category of the ready-made class boards (<see cref="BoardTemplates"/>) and the class board designer.
    /// </summary>
    public const string BoardCategoryName = "Thời khoá biểu";
    public const string BoardCategorySlug = "thoi-khoa-bieu";

    /// <summary>
    /// Category the board category is put under when it exists (the shop's "Bán hàng" next to "Chậu hoa").
    /// </summary>
    public const string BoardParentCategoryName = "Bán hàng";

    /// <summary>
    /// A ready-made class board: a fixed look of the designer (Theme), its product image (embedded resource
    /// <c>Boards/{Theme}.jpg</c>, a render of the 3D preview) and a short description of the look.
    /// </summary>
    public sealed record BoardTemplate(string Sku, string Theme, string Name, string Slug, string Look);

    /// <summary>
    /// Ready-made class boards, after the boards customers ask for most. Created by <see cref="ApplyBoardTemplatesAsync"/>.
    /// </summary>
    public static readonly BoardTemplate[] BoardTemplates =
    [
        new("TT-TKB-PASTEL", "pastel", "Thời khoá biểu Pastel tím in 3D", "thoi-khoa-bieu-pastel-tim",
            "viền gợn sóng màu tím, tiêu đề nổi trên nhãn tím, nhãn Sáng tím – Chiều hồng, sticker trái tim và ngôi sao"),
        new("TT-TKB-GAMER", "gamer", "Thời khoá biểu Gamer in 3D", "thoi-khoa-bieu-gamer",
            "khung đen trên nền trắng ngà, số ngày trong vòng tròn, nhãn Sáng đen – Chiều cam, sticker bánh răng và tia sét"),
        new("TT-TKB-PANDA", "panda", "Thời khoá biểu Gấu trúc in 3D", "thoi-khoa-bieu-gau-truc",
            "khung xanh lá tre, hàng ngày dạng viên xanh, ô xanh nhạt, sticker gấu trúc và lá tre"),
        new("TT-TKB-SPACE", "space", "Thời khoá biểu Vũ trụ in 3D", "thoi-khoa-bieu-vu-tru",
            "nền đen điểm sao lấp lánh, nhãn Sáng tím – Chiều cam, sticker tên lửa và hành tinh"),
        new("TT-TKB-CLASSIC", "classic", "Thời khoá biểu Cổ điển in 3D", "thoi-khoa-bieu-co-dien",
            "nền trắng gọn gàng, số ngày trong vòng tròn cam, nhãn Sáng xanh – Chiều đỏ")
    ];

    /// <summary>
    /// Base price of a ready-made class board (base length, text printed in place).
    /// </summary>
    public const decimal BoardTemplatePrice = 290000;

    /// <summary>
    /// Products with the 3D designer, by SKU.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, DesignProduct> DesignProducts = new Dictionary<string, DesignProduct>(StringComparer.OrdinalIgnoreCase)
    {
        ["TT-NAMEPLATE"] = new("nameplate", "Danh sách tên cần in", "bảng tên", true, true),
        ["TT-KEYCAP"] = new("keycap", "Danh sách keycap cần in", "keycap", true, false),
        [ClassBoardSku] = new("classboard", "Danh sách bảng cần in", "bảng", false, true),
        [QrSku] = new("qr", "Danh sách mã QR cần in", "bảng QR", false, true),
        ["TT-TKB-PASTEL"] = new("classboard", "Danh sách bảng cần in", "bảng", false, true, "pastel"),
        ["TT-TKB-GAMER"] = new("classboard", "Danh sách bảng cần in", "bảng", false, true, "gamer"),
        ["TT-TKB-PANDA"] = new("classboard", "Danh sách bảng cần in", "bảng", false, true, "panda"),
        ["TT-TKB-SPACE"] = new("classboard", "Danh sách bảng cần in", "bảng", false, true, "space"),
        ["TT-TKB-CLASSIC"] = new("classboard", "Danh sách bảng cần in", "bảng", false, true, "classic")
    };

    /// <summary>
    /// Length range and price of a kind with a length slider: shortest, longest and base length (cm) and the price
    /// change per cm in percent.
    /// </summary>
    public static (decimal Min, decimal Max, decimal Base, decimal PercentPerCm) LengthRange(string kind, StudioSettings settings)
    {
        var (a, b, baseLength, percent) = kind switch
        {
            "classboard" => (settings.ClassBoardMinLength, settings.ClassBoardMaxLength, settings.ClassBoardBaseLength, settings.ClassBoardPercentPerCm),
            "qr" => (settings.QrMinLength, settings.QrMaxLength, settings.QrBaseLength, settings.QrPercentPerCm),
            _ => (settings.NameplateMinLength, settings.NameplateMaxLength, settings.NameplateBaseLength, settings.NameplatePercentPerCm)
        };
        return (Math.Min(a, b), Math.Max(a, b), baseLength, percent);
    }

    /// <summary>
    /// Name of the text attribute that holds the plate length in cm, picked with the slider on the product page and
    /// priced by <see cref="NameplateLengthPriceCalculator"/>. Replaces the former fixed sizes.
    /// </summary>
    public const string LengthAttributeName = "Chiều dài (cm)";

    /// <summary>
    /// Name of the former fixed-size choice (10 / 15 / 20 cm) that the length slider replaces.
    /// </summary>
    public const string FormerSizeAttributeName = "Kích thước";

    /// <summary>
    /// Returns the product's length attribute (<see cref="LengthAttributeName"/>), adding it on first use. Adding it
    /// removes the product's former fixed-size choice, whose price adjustments would otherwise add up with the length price.
    /// </summary>
    public static Task<ProductVariantAttribute> EnsureLengthAttributeAsync(SmartDbContext db, int productId, CancellationToken cancelToken = default)
        => EnsureAttributeAsync(db, productId, LengthAttributeName, AttributeControlType.TextBox, true, cancelToken);

    /// <summary>
    /// The length in cm a customer entered, limited to the range of the kind; the base length when empty or invalid.
    /// </summary>
    public static decimal ParseLength(string value, string kind, StudioSettings settings)
    {
        var range = LengthRange(kind, settings);
        var text = value?.Trim().Replace(',', '.');
        if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var length))
        {
            length = range.Base;
        }

        // Half centimetres, like the slider.
        return Math.Clamp(Math.Round(length * 2, MidpointRounding.AwayFromZero) / 2, range.Min, range.Max);
    }

    /// <summary>
    /// Price factor of a length: 1 + (length − base) × percent per cm / 100, never below 0.2.
    /// </summary>
    public static decimal LengthFactor(decimal length, string kind, StudioSettings settings)
    {
        var range = LengthRange(kind, settings);
        return Math.Max(0.2m, 1 + (length - range.Base) * range.PercentPerCm / 100);
    }

    /// <summary>
    /// Name of the optional text attribute that holds the 3D designer's choices (font, shape, holes…) as a readable summary.
    /// </summary>
    public const string DesignAttributeName = "Thiết kế";

    /// <summary>
    /// Returns the product's design attribute (<see cref="DesignAttributeName"/>, multiline text, optional), adding it
    /// on first use so products created before the designer get it too. The storefront hides its input.
    /// </summary>
    public static Task<ProductVariantAttribute> EnsureDesignAttributeAsync(SmartDbContext db, int productId, CancellationToken cancelToken = default)
        => EnsureAttributeAsync(db, productId, DesignAttributeName, AttributeControlType.MultilineTextbox, false, cancelToken);

    // Product pages opened at the same moment must not add the same attribute twice.
    private static readonly SemaphoreSlim _ensureLock = new(1, 1);

    /// <summary>
    /// Returns the product's attribute named <paramref name="name"/>, adding it on first use (and then removing the
    /// former fixed sizes if <paramref name="replaceFormerSizes"/>). Duplicates left by concurrent first visits are
    /// removed, keeping the oldest.
    /// </summary>
    private static async Task<ProductVariantAttribute> EnsureAttributeAsync(
        SmartDbContext db,
        int productId,
        string name,
        AttributeControlType controlType,
        bool replaceFormerSizes,
        CancellationToken cancelToken)
    {
        // Fast path without the lock: the attribute exists exactly once.
        var found = await db.ProductVariantAttributes
            .Include(x => x.ProductAttribute)
            .Where(x => x.ProductId == productId && x.ProductAttribute.Name == name)
            .OrderBy(x => x.Id)
            .ToListAsync(cancelToken);
        if (found.Count == 1)
        {
            return found[0];
        }

        await _ensureLock.WaitAsync(cancelToken);
        try
        {
            var attributes = await db.ProductVariantAttributes
                .Include(x => x.ProductAttribute)
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.Id)
                .ToListAsync(cancelToken);

            var matches = attributes.Where(x => x.ProductAttribute.Name == name).ToList();
            if (matches.Count > 0)
            {
                if (matches.Count > 1)
                {
                    db.ProductVariantAttributes.RemoveRange(matches.Skip(1));
                    await db.SaveChangesAsync(cancelToken);
                    await RemoveUnusedDuplicatesAsync(db, name, matches[0].ProductAttributeId, cancelToken);
                }

                return matches[0];
            }

            var productAttribute = await db.ProductAttributes
                .Where(x => x.Name == name)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancelToken);
            if (productAttribute == null)
            {
                productAttribute = new ProductAttribute { Name = name };
                db.ProductAttributes.Add(productAttribute);
                await db.SaveChangesAsync(cancelToken);
            }

            var sizes = replaceFormerSizes
                ? attributes.FirstOrDefault(x => x.ProductAttribute.Name == FormerSizeAttributeName && x.AttributeControlType == AttributeControlType.RadioList)
                : null;

            var attribute = new ProductVariantAttribute
            {
                ProductId = productId,
                ProductAttributeId = productAttribute.Id,
                AttributeControlTypeId = (int)controlType,
                IsRequired = false,
                DisplayOrder = sizes?.DisplayOrder ?? (attributes.Count > 0 ? attributes.Max(x => x.DisplayOrder) + 1 : 1)
            };
            db.ProductVariantAttributes.Add(attribute);
            if (sizes != null)
            {
                db.ProductVariantAttributes.Remove(sizes);
            }

            await db.SaveChangesAsync(cancelToken);
            await RemoveUnusedDuplicatesAsync(db, name, productAttribute.Id, cancelToken);
            return attribute;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another server instance changed the attributes at the same time: use what is stored now.
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is ProductVariantAttribute or ProductAttribute).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return await db.ProductVariantAttributes
                .Include(x => x.ProductAttribute)
                .Where(x => x.ProductId == productId && x.ProductAttribute.Name == name)
                .OrderBy(x => x.Id)
                .FirstAsync(cancelToken);
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    // Removes attributes of the same name that no product uses any more (left by concurrent first visits).
    private static async Task RemoveUnusedDuplicatesAsync(SmartDbContext db, string name, int keepId, CancellationToken cancelToken)
    {
        var unused = await db.ProductAttributes
            .Where(x => x.Name == name && x.Id != keepId && !db.ProductVariantAttributes.Any(m => m.ProductAttributeId == x.Id))
            .ToListAsync(cancelToken);
        if (unused.Count > 0)
        {
            db.ProductAttributes.RemoveRange(unused);
            await db.SaveChangesAsync(cancelToken);
        }
    }

    private static Option[] ColorOptions(int preselected)
        => _colors.Select((c, i) => new Option(c.Name, 0, c.Color, i == preselected)).ToArray();

    /// <summary>
    /// Creates the category and the products that do not exist yet (looked up by SKU).
    /// </summary>
    public static async Task ApplyAsync(SmartDbContext db, IUrlService urlService, CancellationToken cancelToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(x => x.Name == CategoryName && !x.Deleted, cancelToken);
        if (category == null)
        {
            var categoryTemplate = await db.CategoryTemplates.FirstOrDefaultAsync(x => x.ViewPath == "CategoryTemplate.ProductsInGridOrLines", cancelToken)
                ?? await db.CategoryTemplates.FirstAsync(cancelToken);

            category = new Category
            {
                Name = CategoryName,
                Description = "<p>Sản phẩm in 3D mang dấu ấn riêng: bạn chọn chữ, màu, kiểu dáng — studio in cho bạn.</p>",
                CategoryTemplateId = categoryTemplate.Id,
                Published = true,
                DisplayOrder = 0
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync(cancelToken);
            await urlService.SaveSlugAsync(category, CategorySlug, category.Name, true);
        }

        var template = await db.ProductTemplates.FirstOrDefaultAsync(x => x.ViewPath == "Product", cancelToken)
            ?? await db.ProductTemplates.FirstAsync(cancelToken);
        var displayOrder = 0;

        foreach (var spec in _products)
        {
            displayOrder++;
            if (await db.Products.AnyAsync(x => x.Sku == spec.Sku, cancelToken))
            {
                continue;
            }

            await CreateProductAsync(db, urlService, spec, category, template.Id, displayOrder, true, cancelToken);
        }
    }

    // Creates a product of a spec with its attributes and puts it into the category.
    private static async Task<Product> CreateProductAsync(
        SmartDbContext db,
        IUrlService urlService,
        Spec spec,
        Category category,
        int templateId,
        int displayOrder,
        bool showOnHomePage,
        CancellationToken cancelToken)
    {
        var product = new Product
        {
            ProductType = ProductType.SimpleProduct,
            Sku = spec.Sku,
            Name = spec.Name,
            ShortDescription = spec.ShortDescription,
            FullDescription = spec.FullDescription,
            MetaTitle = spec.Name,
            MetaDescription = spec.ShortDescription,
            ProductTemplateId = templateId,
            Price = spec.Price,
            Visibility = ProductVisibility.Full,
            Published = true,
            ShowOnHomePage = showOnHomePage,
            HomePageDisplayOrder = displayOrder,
            ManageInventoryMethod = ManageInventoryMethod.DontManageStock,
            OrderMinimumQuantity = 1,
            OrderMaximumQuantity = 1000,
            QuantityStep = 1,
            IsShippingEnabled = true,
            AllowCustomerReviews = true,
            DisplayOrder = displayOrder
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancelToken);
        await urlService.SaveSlugAsync(product, spec.Slug, spec.Name, true);

        db.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = category.Id, DisplayOrder = displayOrder });

        var attributeOrder = 0;
        foreach (var attr in spec.Attributes)
        {
            var productAttribute = await db.ProductAttributes.FirstOrDefaultAsync(x => x.Name == attr.Name, cancelToken);
            if (productAttribute == null)
            {
                productAttribute = new ProductAttribute { Name = attr.Name };
                db.ProductAttributes.Add(productAttribute);
                await db.SaveChangesAsync(cancelToken);
            }

            var variantAttribute = new ProductVariantAttribute
            {
                ProductId = product.Id,
                ProductAttributeId = productAttribute.Id,
                AttributeControlTypeId = (int)attr.ControlType,
                IsRequired = attr.IsRequired,
                TextPrompt = attr.TextPrompt,
                DisplayOrder = ++attributeOrder
            };

            db.ProductVariantAttributes.Add(variantAttribute);
            await db.SaveChangesAsync(cancelToken);

            var valueOrder = 0;
            foreach (var option in attr.Options)
            {
                db.ProductVariantAttributeValues.Add(new ProductVariantAttributeValue
                {
                    ProductVariantAttributeId = variantAttribute.Id,
                    Name = option.Name,
                    Color = option.Color,
                    PriceAdjustment = option.PriceAdjustment,
                    IsPreSelected = option.PreSelected,
                    DisplayOrder = ++valueOrder,
                    Quantity = 1
                });
            }
        }

        await db.SaveChangesAsync(cancelToken);
        return product;
    }

    /// <summary>
    /// Creates the "Thời khoá biểu" category (under "Bán hàng" when that exists) with the ready-made boards that do not
    /// exist yet, each with its product image, and adds the class board designer to it as the "design your own" board.
    /// The designer's look now comes from its themes: its former "Màu nền" / "Màu chữ" choices are removed.
    /// </summary>
    public static async Task ApplyBoardTemplatesAsync(SmartDbContext db, IUrlService urlService, IMediaService mediaService, CancellationToken cancelToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(x => x.Name == BoardCategoryName && !x.Deleted, cancelToken);
        if (category == null)
        {
            var parent = await db.Categories.FirstOrDefaultAsync(x => x.Name == BoardParentCategoryName && !x.Deleted, cancelToken);
            var categoryTemplate = await db.CategoryTemplates.FirstOrDefaultAsync(x => x.ViewPath == "CategoryTemplate.ProductsInGridOrLines", cancelToken)
                ?? await db.CategoryTemplates.FirstAsync(cancelToken);

            category = new Category
            {
                Name = BoardCategoryName,
                ParentId = parent?.Id,
                Description = "<p>Bảng thời khoá biểu in 3D nhiều màu cho bé: chọn mẫu, nhập tên là xong — studio in đúng mẫu, có khung ô cho từng tiết. "
                    + "Muốn tự chọn màu, bố cục hoặc làm sơ đồ chỗ ngồi của lớp thì dùng mẫu <b>Tự thiết kế</b>.</p>",
                CategoryTemplateId = categoryTemplate.Id,
                Published = true,
                DisplayOrder = parent != null ? 1 : 0
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync(cancelToken);
            await urlService.SaveSlugAsync(category, BoardCategorySlug, category.Name, true);
        }

        var template = await db.ProductTemplates.FirstOrDefaultAsync(x => x.ViewPath == "Product", cancelToken)
            ?? await db.ProductTemplates.FirstAsync(cancelToken);
        var displayOrder = 0;

        foreach (var board in BoardTemplates)
        {
            displayOrder++;
            var product = await db.Products.FirstOrDefaultAsync(x => x.Sku == board.Sku, cancelToken);
            if (product == null)
            {
                var spec = new Spec(board.Sku, board.Name, board.Slug, BoardTemplatePrice,
                    $"Thời khoá biểu in 3D nhiều màu: {board.Look}. Chỉ cần nhập tên muốn in.",
                    $"<p>Bảng thời khoá biểu in 3D nhiều màu theo mẫu: {board.Look}. Bạn chỉ cần <b>nhập tên</b> (tên bé hoặc tên lớp) — "
                    + "xem trước 3D ngay trên trang. Bảng có sẵn ô cho từng tiết buổi sáng và buổi chiều, từ thứ 2 đến thứ 7, 2 lỗ treo tường.</p>"
                    + "<p>Muốn in sẵn tên môn vào từng ô thì điền thêm ở phần xem trước (không bắt buộc). Chọn <b>ô rời</b> để có các miếng môn học "
                    + "cắm vào hốc, rút ra đổi lại được khi thay thời khoá biểu. Giá theo chiều dài bảng.</p>",
                    [
                        new("Tên trên bảng", AttributeControlType.TextBox, true, "Tên muốn in (VD: Vũ Lan Anh, Lớp 6A1)", []),
                        TileAttribute,
                        new(LengthAttributeName, AttributeControlType.TextBox, false, null, [])
                    ]);

                product = await CreateProductAsync(db, urlService, spec, category, template.Id, displayOrder, false, cancelToken);
            }

            await EnsureBoardPictureAsync(db, mediaService, product, board.Theme, cancelToken);
        }

        // One kind of removable tiles only: the press-fit choice is renamed, the magnet choice removed.
        var tileValues = await db.ProductVariantAttributeValues
            .Where(x => x.ProductVariantAttribute.ProductAttribute.Name == TileAttribute.Name && x.Name.StartsWith("Ô rời tháo lắp –"))
            .ToListAsync(cancelToken);
        foreach (var value in tileValues)
        {
            if (value.Name.Contains("nam châm"))
            {
                db.ProductVariantAttributeValues.Remove(value);
            }
            else
            {
                value.Name = RemovableTilesOption;
            }
        }

        await db.SaveChangesAsync(cancelToken);

        // The designer: the last board of the category, and its colors now come from the looks.
        var designer = await db.Products.FirstOrDefaultAsync(x => x.Sku == ClassBoardSku && !x.Deleted, cancelToken);
        if (designer != null)
        {
            if (!await db.ProductCategories.AnyAsync(x => x.ProductId == designer.Id && x.CategoryId == category.Id, cancelToken))
            {
                db.ProductCategories.Add(new ProductCategory { ProductId = designer.Id, CategoryId = category.Id, DisplayOrder = BoardTemplates.Length + 1 });
            }

            var colors = await db.ProductVariantAttributes
                .Where(x => x.ProductId == designer.Id && (x.ProductAttribute.Name == "Màu nền" || x.ProductAttribute.Name == "Màu chữ"))
                .ToListAsync(cancelToken);
            db.ProductVariantAttributes.RemoveRange(colors);

            await db.SaveChangesAsync(cancelToken);
            await EnsureBoardPictureAsync(db, mediaService, designer, "custom", cancelToken);
        }
    }

    // Product image of a ready-made board (embedded Boards/{theme}.jpg), unless the product has pictures already.
    private static async Task EnsureBoardPictureAsync(SmartDbContext db, IMediaService mediaService, Product product, string theme, CancellationToken cancelToken)
    {
        if (await db.ProductMediaFiles.AnyAsync(x => x.ProductId == product.Id, cancelToken))
        {
            return;
        }

        using var resource = typeof(StudioCustomProducts).Assembly.GetManifestResourceStream($"Smartstore.Split3D.Boards.{theme}.jpg");
        if (resource == null)
        {
            return;
        }

        var path = mediaService.CombinePaths(SystemAlbumProvider.Catalog, $"thoi-khoa-bieu-{theme}.jpg");
        var file = await mediaService.GetFileByPathAsync(path);
        if (file == null)
        {
            using var stream = new MemoryStream();
            await resource.CopyToAsync(stream, cancelToken);
            stream.Position = 0;
            file = await mediaService.SaveFileAsync(path, stream, false, DuplicateFileHandling.Rename);
        }

        db.ProductMediaFiles.Add(new ProductMediaFile { ProductId = product.Id, MediaFileId = file.Id, DisplayOrder = 1 });
        product.MainPictureId = file.Id;
        await db.SaveChangesAsync(cancelToken);
    }

    /// <summary>
    /// Whether the product takes text from the customer (a "customize" product).
    /// </summary>
    /// <summary>
    /// Adds what a newer designer needs to products created before it: the key size, legend color and the DSA / SA
    /// profiles of the keycap, the tile choice of the class board. Existing options, prices and names are left alone.
    /// </summary>
    public static async Task UpgradeAsync(SmartDbContext db, CancellationToken cancelToken = default)
    {
        await UpgradeColorsAsync(db, cancelToken);

        var boardId = await db.Products.Where(x => x.Sku == ClassBoardSku).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancelToken);
        if (boardId != null)
        {
            var boardAttributes = await db.ProductVariantAttributes
                .Include(x => x.ProductAttribute)
                .Where(x => x.ProductId == boardId.Value)
                .ToListAsync(cancelToken);
            if (!boardAttributes.Any(x => x.ProductAttribute.Name == TileAttribute.Name))
            {
                // Right after the colors: the attributes behind them move one place down.
                var colors = boardAttributes.Where(x => x.ProductAttribute.Name.StartsWith("Màu")).Select(x => x.DisplayOrder).DefaultIfEmpty(0).Max();
                foreach (var later in boardAttributes.Where(x => x.DisplayOrder > colors))
                {
                    later.DisplayOrder++;
                }

                await db.SaveChangesAsync(cancelToken);
                await AddAttributeAsync(db, boardId.Value, TileAttribute, colors + 1, cancelToken);
            }
        }

        var keycapId = await db.Products.Where(x => x.Sku == "TT-KEYCAP").Select(x => (int?)x.Id).FirstOrDefaultAsync(cancelToken);
        if (keycapId == null)
        {
            return;
        }

        var mapped = await db.ProductVariantAttributes
            .Include(x => x.ProductAttribute)
            .Include(x => x.ProductVariantAttributeValues)
            .Where(x => x.ProductId == keycapId.Value)
            .ToListAsync(cancelToken);
        var order = mapped.Count > 0 ? mapped.Max(x => x.DisplayOrder) : 0;

        foreach (var attr in new[] { KeySizeAttribute, LegendColorAttribute })
        {
            if (!mapped.Any(x => x.ProductAttribute.Name == attr.Name))
            {
                await AddAttributeAsync(db, keycapId.Value, attr, ++order, cancelToken);
            }
        }

        var profile = mapped.FirstOrDefault(x => x.ProductAttribute.Name == "Profile");
        if (profile != null)
        {
            var valueOrder = profile.ProductVariantAttributeValues.Count > 0 ? profile.ProductVariantAttributeValues.Max(x => x.DisplayOrder) : 0;
            foreach (var option in _extraProfiles.Where(o => !profile.ProductVariantAttributeValues.Any(v => v.Name == o.Name)))
            {
                db.ProductVariantAttributeValues.Add(new ProductVariantAttributeValue
                {
                    ProductVariantAttributeId = profile.Id,
                    Name = option.Name,
                    PriceAdjustment = option.PriceAdjustment,
                    DisplayOrder = ++valueOrder,
                    Quantity = 1
                });
            }

            await db.SaveChangesAsync(cancelToken);
        }
    }

    /// <summary>
    /// Completes the color choices of the design products with the colors of <see cref="_colors"/> they lack (matched
    /// by name) and orders them like that list. Names, colors and prices of existing values are left alone; colors the
    /// shop added itself follow the list.
    /// </summary>
    private static async Task UpgradeColorsAsync(SmartDbContext db, CancellationToken cancelToken)
    {
        var skus = DesignProducts.Keys.ToArray();
        var attributes = await db.ProductVariantAttributes
            .Include(x => x.ProductAttribute)
            .Include(x => x.ProductVariantAttributeValues)
            .Where(x => skus.Contains(x.Product.Sku)
                && _colorAttributeNames.Contains(x.ProductAttribute.Name)
                && x.AttributeControlTypeId == (int)AttributeControlType.Boxes)
            .ToListAsync(cancelToken);

        static bool Same(ProductVariantAttributeValue value, string name)
            => string.Equals(value.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase);

        foreach (var attribute in attributes)
        {
            var values = attribute.ProductVariantAttributeValues.ToList();
            var order = 0;
            foreach (var (name, color) in _colors)
            {
                var value = values.FirstOrDefault(v => Same(v, name));
                if (value != null)
                {
                    value.DisplayOrder = ++order;
                    continue;
                }

                db.ProductVariantAttributeValues.Add(new ProductVariantAttributeValue
                {
                    ProductVariantAttributeId = attribute.Id,
                    Name = name,
                    Color = color,
                    DisplayOrder = ++order,
                    Quantity = 1
                });
            }

            foreach (var other in values.Where(v => !_colors.Any(c => Same(v, c.Name))).OrderBy(v => v.DisplayOrder))
            {
                other.DisplayOrder = ++order;
            }
        }

        await db.SaveChangesAsync(cancelToken);
    }

    private static async Task AddAttributeAsync(SmartDbContext db, int productId, Attribute attr, int displayOrder, CancellationToken cancelToken)
    {
        var productAttribute = await db.ProductAttributes.Where(x => x.Name == attr.Name).OrderBy(x => x.Id).FirstOrDefaultAsync(cancelToken);
        if (productAttribute == null)
        {
            productAttribute = new ProductAttribute { Name = attr.Name };
            db.ProductAttributes.Add(productAttribute);
            await db.SaveChangesAsync(cancelToken);
        }

        var variantAttribute = new ProductVariantAttribute
        {
            ProductId = productId,
            ProductAttributeId = productAttribute.Id,
            AttributeControlTypeId = (int)attr.ControlType,
            IsRequired = attr.IsRequired,
            TextPrompt = attr.TextPrompt,
            DisplayOrder = displayOrder
        };
        db.ProductVariantAttributes.Add(variantAttribute);
        await db.SaveChangesAsync(cancelToken);

        var valueOrder = 0;
        foreach (var option in attr.Options)
        {
            db.ProductVariantAttributeValues.Add(new ProductVariantAttributeValue
            {
                ProductVariantAttributeId = variantAttribute.Id,
                Name = option.Name,
                Color = option.Color,
                PriceAdjustment = option.PriceAdjustment,
                IsPreSelected = option.PreSelected,
                DisplayOrder = ++valueOrder,
                Quantity = 1
            });
        }

        await db.SaveChangesAsync(cancelToken);
    }

    public static bool IsCustomizable(AttributeControlType controlType)
        => controlType is AttributeControlType.TextBox or AttributeControlType.MultilineTextbox or AttributeControlType.FileUpload;
}
