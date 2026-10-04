using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Data;
using Smartstore.Core.Seo;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Personalized products the customer configures before ordering (name plate, keycaps): text the customer types
/// plus color / size options. Created once by <see cref="StudioStorefrontSetup"/>; prices and options are edited
/// in the catalog afterwards and are never overwritten.
/// </summary>
public static class StudioCustomProducts
{
    public const string CategoryName = "Quà tặng cá nhân hoá";
    public const string CategorySlug = "qua-tang-ca-nhan-hoa";

    private static readonly (string Name, string Color)[] _colors =
    [
        ("Trắng", "#ffffff"), ("Đen", "#20201f"), ("Hồng", "#ff67bc"), ("Mint", "#80e5cb"), ("Vàng", "#ffe348")
    ];

    private sealed record Option(string Name, decimal PriceAdjustment = 0, string Color = null, bool PreSelected = false);

    private sealed record Attribute(string Name, AttributeControlType ControlType, bool IsRequired, string TextPrompt, Option[] Options);

    private sealed record Spec(string Sku, string Name, string Slug, decimal Price, string ShortDescription, string FullDescription, Attribute[] Attributes);

    private static readonly Spec[] _products =
    [
        new("TT-NAMEPLATE", "Bảng tên 3D theo yêu cầu", "bang-ten-3d-theo-yeu-cau", 150000,
            "Bảng tên in 3D chữ nổi: tên riêng, tên shop, bàn làm việc. Tự chọn chữ, màu và kích thước.",
            "<p>Bảng tên in 3D chữ nổi hai màu. Nhập nội dung chữ, chọn màu nền, màu chữ và kích thước — studio dựng file, gửi ảnh xem trước rồi mới in.</p>",
            [
                new("Nội dung chữ", AttributeControlType.TextBox, true, "Tên / chữ muốn in", []),
                new("Màu nền", AttributeControlType.Boxes, true, null, ColorOptions(0)),
                new("Màu chữ", AttributeControlType.Boxes, true, null, ColorOptions(1)),
                new("Kích thước", AttributeControlType.RadioList, true, null,
                [
                    new("Dài 10 cm", 0, PreSelected: true), new("Dài 15 cm", 50000), new("Dài 20 cm", 100000)
                ])
            ]),
        new("TT-KEYCAP", "Keycap in 3D theo yêu cầu", "keycap-in-3d-theo-yeu-cau", 45000,
            "Keycap in Resin cho bàn phím cơ: ký tự, logo hoặc hình nhỏ theo ý bạn.",
            "<p>Keycap in Resin mịn, vừa switch MX. Nhập ký tự / mô tả hình muốn in, chọn profile và màu. Giá tính theo mỗi keycap — chọn số lượng khi đặt.</p>",
            [
                new("Ký tự / hình trên keycap", AttributeControlType.TextBox, true, "VD: ESC, logo mèo, chữ T", []),
                new("Profile", AttributeControlType.RadioList, true, null,
                [
                    new("OEM", PreSelected: true), new("Cherry"), new("XDA"), new("Artisan (tượng nhỏ)", 35000)
                ]),
                new("Màu", AttributeControlType.Boxes, true, null, ColorOptions(0))
            ])
    ];

    /// <summary>
    /// Products whose text field can be filled from a list (one row per piece), see <see cref="Filters.TextListFilter"/>.
    /// Key is the SKU; Title heads the list dialog, Item names one row ("bảng tên").
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Title, string Item)> TextListProducts = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        ["TT-NAMEPLATE"] = ("Danh sách tên cần in", "bảng tên"),
        ["TT-KEYCAP"] = ("Danh sách keycap cần in", "keycap")
    };

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

            var product = new Product
            {
                ProductType = ProductType.SimpleProduct,
                Sku = spec.Sku,
                Name = spec.Name,
                ShortDescription = spec.ShortDescription,
                FullDescription = spec.FullDescription,
                MetaTitle = spec.Name,
                MetaDescription = spec.ShortDescription,
                ProductTemplateId = template.Id,
                Price = spec.Price,
                Visibility = ProductVisibility.Full,
                Published = true,
                ShowOnHomePage = true,
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
        }
    }

    /// <summary>
    /// Whether the product takes text from the customer (a "customize" product).
    /// </summary>
    public static bool IsCustomizable(AttributeControlType controlType)
        => controlType is AttributeControlType.TextBox or AttributeControlType.MultilineTextbox or AttributeControlType.FileUpload;
}
