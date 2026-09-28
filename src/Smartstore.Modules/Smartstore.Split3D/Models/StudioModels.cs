namespace Smartstore.Split3D.Models;

/// <summary>
/// Contact details shown in the header, footer and on the studio pages.
/// </summary>
public class StudioContactModel
{
    public string BrandName { get; set; }
    public string Tagline { get; set; }
    public string Address { get; set; }
    public string Phone1 { get; set; }
    public string Phone2 { get; set; }
    public string ZaloUrl { get; set; }
    public string FacebookUrl { get; set; }
    public string Email { get; set; }
    public string MapUrl { get; set; }

    public IEnumerable<string> Phones => new[] { Phone1, Phone2 }.Where(x => x.HasValue());

    /// <summary>
    /// Creates the contact details from the studio settings. The email is only shown when set explicitly, never the sender account.
    /// </summary>
    public static StudioContactModel Create(StudioSettings settings)
    {
        var zalo = new string((settings.ZaloPhone ?? string.Empty).Where(char.IsDigit).ToArray());

        return new StudioContactModel
        {
            BrandName = settings.BrandName.NullEmpty() ?? "TT Minimal",
            Tagline = settings.Tagline,
            Address = settings.Address,
            Phone1 = settings.Phone1,
            Phone2 = settings.Phone2,
            ZaloUrl = zalo.HasValue() ? "https://zalo.me/" + zalo : null,
            FacebookUrl = settings.FacebookUrl.NullEmpty(),
            Email = settings.Email.NullEmpty(),
            MapUrl = settings.MapUrl.NullEmpty()
        };
    }

    /// <summary>
    /// Formats a phone number for display, e.g. "0333 424 766".
    /// </summary>
    public static string FormatPhone(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 10 ? $"{digits[..4]} {digits[4..7]} {digits[7..]}" : phone;
    }

    public static string TelUrl(string phone)
        => "tel:" + new string((phone ?? string.Empty).Where(c => char.IsDigit(c) || c == '+').ToArray());
}

public class StudioHomeModel
{
    public StudioContactModel Contact { get; set; }
    public List<PrintTechnology> Technologies { get; set; } = [];
    public string PrintPriceNote { get; set; }
    public string PrintServiceUrl { get; set; }
    public List<StudioCategoryCard> Categories { get; set; } = [];
    public List<StudioProductCard> Products { get; set; } = [];
    public List<StudioAddonCard> Addons { get; set; } = [];
    public string AddonCategoryUrl { get; set; }
}

public class StudioCategoryCard
{
    public string Name { get; set; }
    public string Url { get; set; }
    public int Id { get; set; }
    public string ImageUrl { get; set; }
    public int ProductCount { get; set; }
}

public class StudioProductCard
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string ImageUrl { get; set; }
    public string Price { get; set; }
    public string OldPrice { get; set; }
    public bool PriceFrom { get; set; }
    public string CategoryName { get; set; }
    public int CategoryId { get; set; }
    public bool HasVariants { get; set; }
}

public class StudioAddonCard
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }
    public string ImageUrl { get; set; }
    public string PriceFrom { get; set; }
    public int PlanCount { get; set; }
}

public class PrintServicePageModel
{
    public StudioContactModel Contact { get; set; }
    public List<PrintTechnology> Technologies { get; set; } = [];
    public string PrintPriceNote { get; set; }
    public PrintQuoteFormModel Form { get; set; } = new();
    public int MaxFileSizeMb { get; set; }
    public string AllowedExtensions { get; set; }
    public int? SubmittedId { get; set; }
}

/// <summary>
/// Quote request form on the "In 3D" page. Labels are Vietnamese in the view because the page is Vietnamese only.
/// </summary>
public class PrintQuoteFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(200)]
    public string Name { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại."), StringLength(50)]
    [RegularExpression(@"^[0-9+ ().-]{8,20}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string Phone { get; set; }

    [StringLength(255), EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; }

    [StringLength(100)]
    public string Technology { get; set; }

    [StringLength(400)]
    public string Material { get; set; }

    [Range(1, 100000, ErrorMessage = "Số lượng không hợp lệ.")]
    public int Quantity { get; set; } = 1;

    [Range(1, 1000000, ErrorMessage = "Khối lượng không hợp lệ.")]
    public int? EstimatedGrams { get; set; }

    public bool NeedsDesign { get; set; }

    [StringLength(4000)]
    public string Note { get; set; }

    [StringLength(1000), Url(ErrorMessage = "Link file không hợp lệ.")]
    public string FileLink { get; set; }
}

public class StudioNavModel
{
    public List<StudioNavItem> Items { get; } = [];
}

public class StudioNavItem
{
    public string Text { get; set; }
    public string Url { get; set; }
    public string ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public List<StudioNavItem> Children { get; } = [];
}
