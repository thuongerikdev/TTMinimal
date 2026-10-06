#nullable enable

using System.Net;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Follow-up of 3D printing quote requests: who was contacted when and how (the contact log), when the request
/// has to be picked up again, and the ready-made message the studio sends by Zalo, SMS, Messenger or email.
/// </summary>
public class PrintQuoteFollowUpService
{
    private readonly SmartDbContext _db;
    private readonly StudioSettings _settings;

    public PrintQuoteFollowUpService(SmartDbContext db, StudioSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    /// <summary>
    /// Writes an entry into the contact log of a request and moves a new request to
    /// <see cref="PrintQuoteStatus.Contacted"/>.
    /// </summary>
    /// <param name="followUpDays">Schedule the next follow-up in so many days, or <c>null</c> to leave it alone.</param>
    public async Task<PrintQuoteContact> LogAsync(
        PrintQuoteRequest quote,
        PrintContactChannel channel,
        string? message,
        bool isIncoming = false,
        int userId = 0,
        string? userName = null,
        int? followUpDays = null,
        CancellationToken cancelToken = default)
    {
        Guard.NotNull(quote);

        var now = DateTime.UtcNow;
        var entry = new PrintQuoteContact
        {
            PrintQuoteRequestId = quote.Id,
            CreatedOnUtc = now,
            Channel = channel,
            IsIncoming = isIncoming,
            Message = message?.Trim().NullEmpty(),
            UserId = userId,
            UserName = userName?.Truncate(200)
        };

        _db.PrintQuoteContacts().Add(entry);

        quote.ContactCount++;
        quote.UpdatedOnUtc = now;

        if (channel != PrintContactChannel.Note)
        {
            quote.LastContactOnUtc = now;

            if (quote.Status == PrintQuoteStatus.New)
            {
                quote.Status = PrintQuoteStatus.Contacted;
            }
        }

        if (followUpDays.HasValue)
        {
            quote.FollowUpOnUtc = followUpDays.Value > 0 ? now.AddDays(followUpDays.Value) : null;
        }

        await _db.SaveChangesAsync(cancelToken);

        return entry;
    }

    /// <summary>
    /// Sets or clears the date the request has to be followed up on.
    /// </summary>
    public async Task SetFollowUpAsync(PrintQuoteRequest quote, DateTime? dateUtc, CancellationToken cancelToken = default)
    {
        Guard.NotNull(quote);

        quote.FollowUpOnUtc = dateUtc;
        quote.UpdatedOnUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancelToken);
    }

    /// <summary>
    /// Gets the contact log of a request, newest first.
    /// </summary>
    public Task<List<PrintQuoteContact>> GetLogAsync(int quoteId, CancellationToken cancelToken = default)
        => _db.PrintQuoteContacts()
            .AsNoTracking()
            .Where(x => x.PrintQuoteRequestId == quoteId)
            .OrderByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancelToken);

    /// <summary>
    /// Number of requests that need attention: never contacted, or their follow-up date has passed.
    /// </summary>
    public Task<int> CountDueAsync(CancellationToken cancelToken = default)
    {
        var now = DateTime.UtcNow;
        var open = new[] { (int)PrintQuoteStatus.New, (int)PrintQuoteStatus.Contacted, (int)PrintQuoteStatus.Quoted };

        return _db.PrintQuoteRequests()
            .AsNoTracking()
            .CountAsync(x => open.Contains(x.StatusId)
                && (x.StatusId == (int)PrintQuoteStatus.New || (x.FollowUpOnUtc != null && x.FollowUpOnUtc <= now)),
                cancelToken);
    }

    /// <summary>
    /// Fills the message template with the data of a request.
    /// </summary>
    /// <param name="payUrl">Payment link of the print job created from the request, if there is one.</param>
    public string BuildMessage(PrintQuoteRequest quote, string? payUrl = null, string? template = null)
    {
        Guard.NotNull(quote);

        var price = quote.QuotedPrice > 0
            ? PrintPriceList.FormatPrice(quote.QuotedPrice!.Value)
            : "studio báo lại sau khi xem file";

        return (template.NullEmpty() ?? _settings.QuoteMessageTemplate.NullEmpty() ?? StudioSettings.DefaultQuoteMessageTemplate)
            .Replace("{Name}", quote.Name)
            .Replace("{Id}", quote.Id.ToString())
            .Replace("{Brand}", _settings.BrandName)
            .Replace("{Price}", price)
            .Replace("{Phone}", _settings.Phone1)
            .Replace("{Link}", payUrl.NullEmpty() ?? "https://ttminimal.com/in-3d")
            .Trim();
    }

    /// <summary>
    /// Deep links of the contact buttons in the admin area.
    /// </summary>
    public static string TelUrl(string? phone)
        => "tel:" + Digits(phone);

    public static string ZaloUrl(string? phone)
        => "https://zalo.me/" + Digits(phone);

    public static string SmsUrl(string? phone, string? body)
        // "?&body=" works on both iOS and Android.
        => $"sms:{Digits(phone)}?&body={Uri.EscapeDataString(body ?? string.Empty)}";

    public static string MailToUrl(string? email, string? subject, string? body)
        => $"mailto:{email}?subject={Uri.EscapeDataString(subject ?? string.Empty)}&body={Uri.EscapeDataString(body ?? string.Empty)}";

    public static string ChannelName(PrintContactChannel channel) => channel switch
    {
        PrintContactChannel.Phone => "Gọi điện",
        PrintContactChannel.Zalo => "Zalo",
        PrintContactChannel.Sms => "SMS",
        PrintContactChannel.Facebook => "Messenger",
        PrintContactChannel.Email => "Email",
        PrintContactChannel.Note => "Ghi chú",
        _ => channel.ToString()
    };

    public static string ChannelIcon(PrintContactChannel channel) => channel switch
    {
        PrintContactChannel.Phone => "bi-telephone",
        PrintContactChannel.Zalo => "bi-chat-dots",
        PrintContactChannel.Sms => "bi-chat-text",
        PrintContactChannel.Facebook => "bi-messenger",
        PrintContactChannel.Email => "bi-envelope",
        _ => "bi-journal-text"
    };

    /// <summary>
    /// HTML encoded message, with line breaks, for the admin preview.
    /// </summary>
    public static string Preview(string? message)
        => WebUtility.HtmlEncode(message ?? string.Empty).Replace("\n", "<br />");

    private static string Digits(string? value)
        => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
