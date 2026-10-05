namespace Smartstore.Split3D.Models;

/// <summary>
/// Next steps shown on the order completed page, depending on how the order is paid.
/// </summary>
public class CheckoutCompletedModel
{
    public Split3DSettings Settings { get; set; }
    public int OrderId { get; set; }
    public bool IsPaid { get; set; }

    /// <summary>
    /// The order is paid by manual bank transfer (Prepayment), so the bank details are shown.
    /// </summary>
    public bool IsBankTransfer { get; set; }

    /// <summary>
    /// Inline SVG VietQR code with amount and transfer content prefilled. <c>null</c> if the bank is not supported.
    /// </summary>
    public string BankQrSvg { get; set; }

    /// <summary>
    /// The order is paid on a payment provider page (e.g. PayOS) that can be (re)started by the customer.
    /// </summary>
    public bool CanPayOnline { get; set; }
}
