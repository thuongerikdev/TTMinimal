using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

/// <summary>
/// A signed Split3D activation key that has been issued to a customer.
/// </summary>
[Table("Split3DLicense")]
[Index(nameof(LicenseId), IsUnique = true)]
[Index(nameof(Email))]
[Index(nameof(OrderItemId))]
[Index(nameof(RepoToken))]
public class Split3DLicense : BaseEntity
{
    /// <summary>
    /// The random license identifier embedded in the signed payload ("id").
    /// </summary>
    [Required, StringLength(64)]
    public string LicenseId { get; set; }

    /// <summary>
    /// The addon this key unlocks. 0 for keys created before addons were introduced (migrated to the default addon).
    /// </summary>
    public int AddonId { get; set; }

    /// <summary>
    /// The "product" value embedded in the signed payload.
    /// </summary>
    [StringLength(100)]
    public string ProductCode { get; set; }

    [Required, StringLength(254)]
    public string Email { get; set; }

    /// <summary>
    /// The customer name embedded in the signed payload ("customer").
    /// </summary>
    [Required, StringLength(400)]
    public string CustomerName { get; set; }

    [StringLength(100)]
    public string Phone { get; set; }

    /// <summary>
    /// Plan name, e.g. "1 năm", "3 tháng", "6 tháng", "Vĩnh viễn" or "Tùy chỉnh".
    /// </summary>
    [Required, StringLength(50)]
    public string KeyType { get; set; }

    /// <summary>
    /// Validity in days counted from <see cref="IssuedOnUtc"/>. <c>null</c> means lifetime.
    /// </summary>
    public int? Days { get; set; }

    /// <summary>
    /// Price in VND (integer amount).
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Purchase date as entered by the admin (transaction info only).
    /// </summary>
    public DateTime? PurchaseDateUtc { get; set; }

    public DateTime IssuedOnUtc { get; set; }

    /// <summary>
    /// Expiry date. <c>null</c> means lifetime.
    /// </summary>
    public DateTime? ExpiresOnUtc { get; set; }

    /// <summary>
    /// The full activation key (base64url payload + "." + base64url signature).
    /// </summary>
    [Required, MaxLength]
    public string Token { get; set; }

    [MaxLength]
    public string Notes { get; set; }

    /// <summary>
    /// The order the key was issued for. 0 if issued manually or imported.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// The order item the key was issued for. 0 if issued manually or imported.
    /// </summary>
    public int OrderItemId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>
    /// Whether the key email has been queued for the customer.
    /// </summary>
    public bool EmailSent { get; set; }

    /// <summary>
    /// Maximum number of devices the key may be active on at the same time.
    /// <c>null</c> uses <see cref="Configuration.Split3DSettings.DefaultMaxDevices"/>.
    /// </summary>
    public int? MaxDevices { get; set; }

    /// <summary>
    /// A blocked key is refused by the activation server, so the addon locks on all devices at their next check.
    /// </summary>
    public bool Blocked { get; set; }

    /// <summary>
    /// Secret part of the key's Blender extension repository URL (/split3d/repo/{token}/index.json).
    /// Created on first use; replacing it invalidates links the customer shared.
    /// </summary>
    [StringLength(32)]
    public string RepoToken { get; set; }
}
