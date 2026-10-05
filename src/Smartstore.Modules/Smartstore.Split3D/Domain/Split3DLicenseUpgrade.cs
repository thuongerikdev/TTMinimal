using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

public enum Split3DUpgradeStatus
{
    /// <summary>
    /// The customer picked the upgrade; it is in the cart and not ordered yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The order is placed and waits for payment.
    /// </summary>
    Ordered = 10,

    /// <summary>
    /// The key was upgraded.
    /// </summary>
    Applied = 20,

    /// <summary>
    /// Replaced by another upgrade, or the order was cancelled.
    /// </summary>
    Cancelled = 30
}

/// <summary>
/// An upgrade of a <see cref="Split3DLicense"/> to a bigger plan (longer validity and/or more devices).
/// The key keeps its id, so its device activations stay; it is re-signed with the new expiry and the
/// addon swaps in the new key at its next online check.
/// Bought by the customer (one row per upgrade in the cart/order) or applied directly by an admin.
/// </summary>
[Table("Split3DLicenseUpgrade")]
[Index(nameof(Split3DLicenseId))]
[Index(nameof(CustomerId), nameof(StatusId))]
[Index(nameof(OrderId))]
public class Split3DLicenseUpgrade : BaseEntity
{
    /// <summary>
    /// Id of the <see cref="Split3DLicense"/> record.
    /// </summary>
    public int Split3DLicenseId { get; set; }

    public int StatusId { get; set; }

    [NotMapped]
    public Split3DUpgradeStatus Status
    {
        get => (Split3DUpgradeStatus)StatusId;
        set => StatusId = (int)value;
    }

    /// <summary>
    /// <c>true</c> if an admin applied the upgrade directly (no order).
    /// </summary>
    public bool ByAdmin { get; set; }

    /// <summary>
    /// The customer who bought the upgrade. 0 for admin upgrades.
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// The plan product the key is upgraded to. 0 for admin upgrades.
    /// </summary>
    public int TargetProductId { get; set; }

    /// <summary>
    /// The order paying for the upgrade. 0 while pending or for admin upgrades.
    /// </summary>
    public int OrderId { get; set; }

    [Required, StringLength(50)]
    public string FromKeyType { get; set; }

    [Required, StringLength(50)]
    public string ToKeyType { get; set; }

    /// <summary>
    /// Validity in days counted from the key's issue date. <c>null</c> means lifetime.
    /// </summary>
    public int? ToDays { get; set; }

    public DateTime? FromExpiresOnUtc { get; set; }

    /// <summary>
    /// New expiry. <c>null</c> means lifetime.
    /// </summary>
    public DateTime? ToExpiresOnUtc { get; set; }

    public int? FromMaxDevices { get; set; }

    public int? ToMaxDevices { get; set; }

    /// <summary>
    /// Amount charged for the upgrade in VND (added to the key's price when applied).
    /// </summary>
    public decimal Price { get; set; }

    [MaxLength]
    public string Notes { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public DateTime? AppliedOnUtc { get; set; }
}
