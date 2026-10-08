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

    [LocalizedDisplay("*ManagedLicensing")]
    public bool ManagedLicensing { get; set; } = true;

    [LocalizedDisplay("*DisplayOrder")]
    public int DisplayOrder { get; set; }

    [LocalizedDisplay("*ComingSoon")]
    public bool ComingSoon { get; set; }

    [LocalizedDisplay("*Kind")]
    public string Kind { get; set; }

    [LocalizedDisplay("*Icon")]
    public string Icon { get; set; }

    [LocalizedDisplay("*GuideUrl")]
    public string GuideUrl { get; set; }

    [UIHint("Media")]
    [AdditionalMetadata("album", "catalog"), AdditionalMetadata("transientUpload", true), AdditionalMetadata("entityType", "Split3DAddon")]
    [LocalizedDisplay("*Picture")]
    public int? PictureId { get; set; }

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
    public int? MaxDevices { get; set; }
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

    /// <summary>
    /// Devices per key for all packages created in this run, e.g. 5 for "Gói 5 máy". Empty = default.
    /// </summary>
    public int? MaxDevices { get; set; }
}

public class MapProductModel
{
    public int AddonId { get; set; }
    public int ProductId { get; set; }
    public string KeyType { get; set; }
    public int? Days { get; set; }
    public int? MaxDevices { get; set; }
}
