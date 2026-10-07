namespace Smartstore.Split3D.Models;

/// <summary>
/// Delivery block of the two-step checkout (confirm page), see <see cref="Services.StudioDeliveryService"/>.
/// Also the form posted to <c>StudioCheckout/SaveDelivery</c>.
/// </summary>
public class CheckoutDeliveryModel
{
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string Address1 { get; set; }
    public string City { get; set; }
    public int ShippingMethodId { get; set; }

    /// <summary>
    /// Address and shipping method are saved, the order can be placed.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Workshop address shown for pickup.
    /// </summary>
    public string StudioAddress { get; set; }

    public List<CheckoutDeliveryMethodModel> Methods { get; set; } = [];
}

public class CheckoutDeliveryMethodModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Rate { get; set; }
    public bool IsPickup { get; set; }
    public bool Selected { get; set; }
}
