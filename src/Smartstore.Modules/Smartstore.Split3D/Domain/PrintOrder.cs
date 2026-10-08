#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

/// <summary>
/// Where a 3D printing job stands. The customer pays a deposit up front
/// (<see cref="PrintOrder.DepositPercent"/>); the studio confirms the job before printing.
/// </summary>
public enum PrintOrderStatus
{
    /// <summary>In the cart or offered to the customer, no order placed yet.</summary>
    Draft = 0,

    /// <summary>Order placed, deposit not received yet.</summary>
    AwaitingPayment = 10,

    /// <summary>Deposit received, the studio has to accept the job.</summary>
    Paid = 20,

    /// <summary>Accepted by the studio, queued for printing.</summary>
    Confirmed = 30,

    Printing = 40,

    /// <summary>Printed, waiting for pickup or handover to the carrier.</summary>
    Ready = 50,

    /// <summary>Handed over and fully paid.</summary>
    Completed = 60,

    Cancelled = 90
}

/// <summary>
/// What a job produces. Both kinds run through the same studio workflow (<see cref="PrintOrderStatus"/>).
/// </summary>
public enum PrintJobKind
{
    /// <summary>A print from model files the customer uploaded, carried by the hidden print product.</summary>
    File = 0,

    /// <summary>The physical products of an ordinary order (shop products, name plates, class boards, ...).</summary>
    Goods = 10
}

public enum PrintDeliveryMethod
{
    /// <summary>The customer picks the print up at the studio.</summary>
    Pickup = 0,

    Shipping = 10
}

/// <summary>
/// One model of a print job as weighed in the price calculator.
/// </summary>
public sealed record PrintOrderModel
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Weight of a single piece in grams.</summary>
    public int Grams { get; init; }

    public int Quantity { get; init; } = 1;

    /// <summary>Size as "X × Y × Z mm".</summary>
    public string? Size { get; init; }

    /// <summary>Volume in cm³.</summary>
    public decimal Volume { get; init; }

    /// <summary>Infill or print mode the weight was calculated with.</summary>
    public string? Fill { get; init; }

    /// <summary>Whether the customer typed the weight in instead of letting the browser weigh the model.</summary>
    public bool Manual { get; init; }

    public int TotalGrams => Grams * Math.Max(Quantity, 1);
}

/// <summary>
/// A 3D printing job the customer ordered and paid a deposit for, either straight from the price calculator
/// or from a quote request the studio turned into an order. Unlike <see cref="PrintQuoteRequest"/> this one
/// always carries money: it is linked to a Smartstore order.
/// </summary>
[Table("PrintOrder")]
[Index(nameof(StatusId))]
[Index(nameof(CreatedOnUtc))]
[Index(nameof(OrderId))]
public class PrintOrder : BaseEntity
{
    public DateTime CreatedOnUtc { get; set; }

    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Job number shown to the customer and used as the attribute of the cart line, e.g. "IN00123".
    /// </summary>
    [StringLength(30)]
    public string? Code { get; set; }

    /// <summary>
    /// Random token of the payment link of a job the studio created from a quote request.
    /// </summary>
    [StringLength(40)]
    public string? PayToken { get; set; }

    /// <summary>
    /// Id of the registered customer, 0 for guests.
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// The quote request this job was created from, 0 if the customer ordered directly.
    /// </summary>
    public int QuoteRequestId { get; set; }

    [StringLength(100)]
    public string? Technology { get; set; }

    [StringLength(400)]
    public string? Material { get; set; }

    /// <summary>
    /// Infill or print mode, e.g. "Độ đặc 20%".
    /// </summary>
    [StringLength(200)]
    public string? Fill { get; set; }

    /// <summary>
    /// Total weight of every piece of the job, in grams.
    /// </summary>
    public int TotalGrams { get; set; }

    /// <summary>
    /// Number of pieces (sum of the quantities of all models).
    /// </summary>
    public int Pieces { get; set; }

    public int ModelCount { get; set; }

    /// <summary>
    /// Price per gram of the tier the total weight falls into.
    /// </summary>
    public decimal PricePerGram { get; set; }

    /// <summary>
    /// Estimated total price of the job (weight × price per gram), before the studio verifies the sliced files.
    /// </summary>
    public decimal PriceEstimate { get; set; }

    /// <summary>
    /// Share of <see cref="PriceEstimate"/> the customer pays up front; 100 = everything.
    /// 0 on a job that is still being created: <see cref="Services.PrintOrderService.CreateDraftAsync"/>
    /// fills it from the studio settings.
    /// </summary>
    public int DepositPercent { get; set; }

    /// <summary>
    /// Amount the customer pays with the order, which is the total of the Smartstore order.
    /// </summary>
    public decimal DepositAmount { get; set; }

    /// <summary>
    /// Price the studio settled on after slicing the files. Null while the estimate stands.
    /// </summary>
    public decimal? FinalPrice { get; set; }

    /// <summary>
    /// Delivery cost the studio settled on after the job was ordered.
    /// </summary>
    public decimal? ShippingFee { get; set; }

    /// <summary>
    /// The weighed models as JSON, see <see cref="Models"/>.
    /// </summary>
    [MaxLength]
    public string? ModelsJson { get; set; }

    /// <summary>
    /// Original name of the uploaded file, or of the ZIP holding all of them.
    /// </summary>
    [StringLength(400)]
    public string? FileName { get; set; }

    /// <summary>
    /// Path of the uploaded file relative to the tenant root.
    /// </summary>
    [StringLength(500)]
    public string? FilePath { get; set; }

    public long FileSize { get; set; }

    /// <summary>
    /// Link to a model file hosted elsewhere, taken over from the quote request.
    /// </summary>
    [StringLength(1000)]
    public string? FileLink { get; set; }

    public int DeliveryMethodId { get; set; }

    [NotMapped]
    public PrintDeliveryMethod DeliveryMethod
    {
        get => (PrintDeliveryMethod)DeliveryMethodId;
        set => DeliveryMethodId = (int)value;
    }

    [StringLength(200)]
    public string? RecipientName { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(400)]
    public string? AddressLine { get; set; }

    [StringLength(150)]
    public string? City { get; set; }

    /// <summary>
    /// Date the customer needs the print, if any.
    /// </summary>
    public DateTime? DesiredOnUtc { get; set; }

    [MaxLength]
    public string? Note { get; set; }

    public int KindId { get; set; }

    [NotMapped]
    public PrintJobKind Kind
    {
        get => (PrintJobKind)KindId;
        set => KindId = (int)value;
    }

    public int StatusId { get; set; }

    [NotMapped]
    public PrintOrderStatus Status
    {
        get => (PrintOrderStatus)StatusId;
        set => StatusId = (int)value;
    }

    /// <summary>
    /// The Smartstore order that carries the deposit, 0 while the job is still a draft.
    /// </summary>
    public int OrderId { get; set; }

    public DateTime? PaidOnUtc { get; set; }

    public DateTime? ConfirmedOnUtc { get; set; }

    public DateTime? CompletedOnUtc { get; set; }

    [MaxLength]
    public string? AdminNote { get; set; }

    [StringLength(100)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// The weighed models of this job.
    /// </summary>
    [NotMapped]
    public IReadOnlyList<PrintOrderModel> Models
    {
        get
        {
            if (ModelsJson.IsEmpty())
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<PrintOrderModel>>(ModelsJson!) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
        set => ModelsJson = value?.Count > 0 ? JsonSerializer.Serialize(value) : null;
    }

    /// <summary>
    /// Amount still to be collected when the print is handed over.
    /// </summary>
    [NotMapped]
    public decimal Outstanding => Math.Max(0, (FinalPrice ?? PriceEstimate) + (ShippingFee ?? 0) - DepositAmount);
}
