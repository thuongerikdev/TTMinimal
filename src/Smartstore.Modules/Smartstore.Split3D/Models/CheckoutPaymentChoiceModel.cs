namespace Smartstore.Split3D.Models;

/// <summary>
/// Payment method choice on the checkout confirm page (two-step checkout).
/// </summary>
public class CheckoutPaymentChoiceModel
{
    public List<PaymentMethodItem> Methods { get; } = [];

    public class PaymentMethodItem
    {
        public string SystemName { get; set; }
        public string Name { get; set; }
        public bool Selected { get; set; }
    }
}
