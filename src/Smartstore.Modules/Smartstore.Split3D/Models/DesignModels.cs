namespace Smartstore.Split3D.Models;

/// <summary>
/// A 3D design service offered on the "Thiết kế" page.
/// </summary>
public sealed record DesignCategory(string Key, string Title, string Description, string Examples, string Icon, string Color)
{
    public static readonly IReadOnlyList<DesignCategory> All =
    [
        new("tranh-ve", "Tranh vẽ & phù điêu 3D", "Biến tranh vẽ, ảnh chụp, chữ ký thành tranh nổi, phù điêu, đèn lithophane.", "Tranh treo tường · Lithophane · Phù điêu chân dung", "image", "pink"),
        new("nhan-vat", "Nhân vật & figure", "Dựng nhân vật từ ảnh hoặc bản phác: chibi, mascot, figure sưu tầm, tượng chân dung.", "Chibi · Mascot · Figure · Tượng", "puzzle", "mint"),
        new("do-vat", "Đồ vật & sản phẩm", "Thiết kế đồ gia dụng, phụ kiện, vỏ hộp, linh kiện kỹ thuật theo kích thước thật.", "Vỏ hộp · Giá đỡ · Linh kiện · Phụ kiện", "cube", "yellow"),
        new("boi-canh", "Bối cảnh & diorama", "Mô hình sa bàn, kiến trúc thu nhỏ, bối cảnh cho figure, quà lưu niệm.", "Diorama · Sa bàn · Kiến trúc mini", "layers", "lilac"),
        new("ca-nhan-hoa", "Logo, bảng tên & quà tặng", "Logo nổi, bảng tên, keycap, móc khoá, quà tặng khắc tên riêng.", "Bảng tên · Keycap · Móc khoá · Logo 3D", "pencil", "mint"),
        new("sua-file", "Sửa & tối ưu file in", "Sửa lỗi lưới, làm rỗng, chia mảnh vừa bàn in, thêm khớp nối, giảm support.", "Sửa lỗi · Chia mảnh · Làm rỗng", "wrench", "yellow"),
        new("render", "Render hình ảnh 3D", "Ảnh render sản phẩm, bối cảnh, ảnh quảng cáo từ mô hình 3D.", "Ảnh sản phẩm · Ảnh quảng cáo", "spark", "pink")
    ];

    public static DesignCategory Find(string key)
        => All.FirstOrDefault(x => x.Key.EqualsNoCase(key) || x.Title.EqualsNoCase(key));
}

public class DesignPageModel
{
    public StudioContactModel Contact { get; set; }
    public DesignRequestFormModel Form { get; set; } = new();
    public int MaxFileSizeMb { get; set; }
    public string AllowedExtensions { get; set; }
    public int? SubmittedId { get; set; }
}

/// <summary>
/// Design request form on the "Thiết kế" page. Stored as a <see cref="PrintQuoteRequest"/> that needs design.
/// </summary>
public class DesignRequestFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(200)]
    public string Name { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại."), StringLength(50)]
    [RegularExpression(@"^[0-9+ ().-]{8,20}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string Phone { get; set; }

    [StringLength(255), EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; }

    [StringLength(100)]
    public string Category { get; set; }

    [Required(ErrorMessage = "Hãy mô tả bạn cần thiết kế gì."), StringLength(4000)]
    public string Note { get; set; }

    /// <summary>
    /// Also print the design after it is done.
    /// </summary>
    public bool AlsoPrint { get; set; } = true;

    [StringLength(1000), Url(ErrorMessage = "Link file không hợp lệ.")]
    public string FileLink { get; set; }
}
