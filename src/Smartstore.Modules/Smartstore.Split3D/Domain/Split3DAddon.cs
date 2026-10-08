using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

/// <summary>
/// A sellable Blender addon. Each addon has its own "product" code, which the addon compares
/// against the "product" field of the signed key, so keys of one addon never unlock another.
/// </summary>
[Table("Split3DAddon")]
[Index(nameof(ProductCode), IsUnique = true)]
public class Split3DAddon : BaseEntity
{
    [Required, StringLength(200)]
    public string Name { get; set; }

    /// <summary>
    /// Value of the PRODUCT constant in the addon's license_core.py, e.g. "split3d-custom-109".
    /// </summary>
    [Required, StringLength(100)]
    public string ProductCode { get; set; }

    /// <summary>
    /// Current addon version, used as download file version, e.g. "3.0.121".
    /// </summary>
    [StringLength(50)]
    public string Version { get; set; }

    [MaxLength]
    public string Description { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>
    /// Marketplace mode: the uploaded add-on contains features only; the shop injects licensing
    /// (key activation, online check, updates) and obfuscates the package, see <see cref="Services.MarketplacePackager"/>.
    /// Off for add-ons that ship their own licensing, like Split3D Print.
    /// </summary>
    public bool ManagedLicensing { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// Shown as a "Sắp ra mắt" teaser card on the tools board instead of a card with packages.
    /// </summary>
    public bool ComingSoon { get; set; }

    /// <summary>
    /// Short label above the name on the tools board, e.g. "Addon Blender" or "Ứng dụng độc lập".
    /// </summary>
    [StringLength(100)]
    public string Kind { get; set; }

    /// <summary>
    /// Name of a <see cref="StudioIcons"/> icon for the teaser card, e.g. "layers".
    /// </summary>
    [StringLength(50)]
    public string Icon { get; set; }
}

/// <summary>
/// Maps a catalog product to the addon and plan a buyer receives a key for.
/// One product currently grants one addon; the table allows several rows per product for bundles later.
/// </summary>
[Table("Split3DAddonProduct")]
[Index(nameof(ProductId))]
[Index(nameof(AddonId))]
public class Split3DAddonProduct : BaseEntity
{
    public int AddonId { get; set; }

    public int ProductId { get; set; }

    /// <summary>
    /// Plan name, see <see cref="Services.Split3DPlans"/>.
    /// </summary>
    [Required, StringLength(50)]
    public string KeyType { get; set; }

    /// <summary>
    /// Validity in days for <see cref="Services.Split3DPlans.Custom"/>; ignored for fixed plans.
    /// </summary>
    public int? Days { get; set; }

    /// <summary>
    /// Number of devices a key bought with this product may activate. <c>null</c> uses
    /// <see cref="Configuration.Split3DSettings.DefaultMaxDevices"/>.
    /// </summary>
    public int? MaxDevices { get; set; }
}
