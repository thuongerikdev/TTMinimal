namespace Smartstore.Split3D.Models;

[LocalizedDisplay("Plugins.Split3D.Addon.")]
public class AddonModel : EntityModelBase
{
    [LocalizedDisplay("*Name")]
    public string Name { get; set; }

    [LocalizedDisplay("*ProductCode")]
    public string ProductCode { get; set; }

    [LocalizedDisplay("*Version")]
    public string Version { get; set; }

    [LocalizedDisplay("*Description")]
    public string Description { get; set; }

    [LocalizedDisplay("*Active")]
    public bool Active { get; set; } = true;

    [LocalizedDisplay("*DisplayOrder")]
    public int DisplayOrder { get; set; }

    public int ProductCount { get; set; }
    public int LicenseCount { get; set; }
    public List<AddonProductModel> Products { get; set; } = [];
}

public class AddonProductModel
{
    public int MappingId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public string Sku { get; set; }
    public string Price { get; set; }
    public bool Published { get; set; }
    public string KeyType { get; set; }
    public int? Days { get; set; }
    public string DownloadVersions { get; set; }
    public string EditUrl { get; set; }
}

public class CreatePlanProductsModel
{
    public int AddonId { get; set; }
    public bool ThreeMonths { get; set; }
    public decimal ThreeMonthsPrice { get; set; }
    public bool SixMonths { get; set; }
    public decimal SixMonthsPrice { get; set; }
    public bool OneYear { get; set; }
    public decimal OneYearPrice { get; set; }
    public bool Lifetime { get; set; }
    public decimal LifetimePrice { get; set; }
}

public class MapProductModel
{
    public int AddonId { get; set; }
    public int ProductId { get; set; }
    public string KeyType { get; set; }
    public int? Days { get; set; }
}
