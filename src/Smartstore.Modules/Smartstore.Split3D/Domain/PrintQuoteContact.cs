#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

/// <summary>
/// How the studio reached the customer of a quote request.
/// </summary>
public enum PrintContactChannel
{
    Phone = 0,
    Zalo = 10,
    Sms = 20,
    Facebook = 30,
    Email = 40,

    /// <summary>A note the studio wrote down without contacting anybody.</summary>
    Note = 90
}

/// <summary>
/// One entry of the contact log of a <see cref="PrintQuoteRequest"/>: a call, a Zalo or SMS message,
/// a Messenger chat or a note. Written when the studio uses one of the contact buttons in the admin area.
/// </summary>
[Table("PrintQuoteContact")]
[Index(nameof(PrintQuoteRequestId))]
public class PrintQuoteContact : BaseEntity
{
    public int PrintQuoteRequestId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public int ChannelId { get; set; }

    [NotMapped]
    public PrintContactChannel Channel
    {
        get => (PrintContactChannel)ChannelId;
        set => ChannelId = (int)value;
    }

    /// <summary>
    /// The customer answered or wrote first, instead of the studio reaching out.
    /// </summary>
    public bool IsIncoming { get; set; }

    /// <summary>
    /// What was said: the message that was sent, or the result of the call.
    /// </summary>
    [MaxLength]
    public string? Message { get; set; }

    /// <summary>
    /// Id of the backend user who logged the contact.
    /// </summary>
    public int UserId { get; set; }

    [StringLength(200)]
    public string? UserName { get; set; }
}
