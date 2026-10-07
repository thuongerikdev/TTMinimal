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

    /// <summary>
    /// Print jobs paid with this order, see <see cref="Services.PrintOrderService"/>.
    /// </summary>
    public List<string> PrintJobCodes { get; set; } = [];

    /// <summary>
    /// What the order contains (keys, print jobs, goods). Key steps and the "My keys" link are only shown for keys.
    /// </summary>
    public StudioOrderContent Content { get; set; }

    /// <summary>
    /// The order contains nothing but keys or key upgrades: the next steps are about receiving the key.
    /// </summary>
    public bool KeysOnly => Content == StudioOrderContent.Keys;

    /// <summary>
    /// The order contains nothing but print jobs, so the next steps are about printing, not about keys.
    /// </summary>
    public bool PrintOnly => Content == StudioOrderContent.Print;

    /// <summary>
    /// The order contains keys next to other products: keys are sent by email, the rest follows the order.
    /// </summary>
    public bool HasKeys => Content.HasFlag(StudioOrderContent.Keys);

    /// <summary>
    /// Amount the studio collects when the print is handed over.
    /// </summary>
    public decimal Outstanding { get; set; }
}
