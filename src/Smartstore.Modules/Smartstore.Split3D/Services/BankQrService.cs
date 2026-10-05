using System.Text;
using System.Text.RegularExpressions;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Imaging.Barcodes;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Generates VietQR codes for the bank account configured in <see cref="Split3DSettings"/>.
/// The bank BIN is looked up from <see cref="Split3DSettings.BankName"/>.
/// </summary>
public partial class BankQrService
{
    /// <summary>
    /// Napas bank identification numbers by normalized bank name (lowercase ASCII, no spaces).
    /// </summary>
    private static readonly Dictionary<string, string> _bins = new()
    {
        ["mb"] = "970422", ["mbbank"] = "970422", ["quandoi"] = "970422",
        ["vcb"] = "970436", ["vietcombank"] = "970436", ["ngoaithuong"] = "970436",
        ["vietinbank"] = "970415", ["ctg"] = "970415", ["congthuong"] = "970415",
        ["bidv"] = "970418", ["dautuvaphattrien"] = "970418",
        ["agribank"] = "970405", ["nongnghiep"] = "970405",
        ["tcb"] = "970407", ["techcombank"] = "970407",
        ["acb"] = "970416", ["achau"] = "970416",
        ["vpbank"] = "970432", ["vpb"] = "970432",
        ["tpbank"] = "970423", ["tienphong"] = "970423",
        ["sacombank"] = "970403", ["stb"] = "970403",
        ["vib"] = "970441", ["quocte"] = "970441",
        ["shb"] = "970443",
        ["hdbank"] = "970437",
        ["ocb"] = "970448", ["phuongdong"] = "970448",
        ["msb"] = "970426", ["maritimebank"] = "970426", ["hanghai"] = "970426",
        ["seabank"] = "970440",
        ["eximbank"] = "970431",
        ["lpbank"] = "970449", ["lienvietpostbank"] = "970449", ["locphat"] = "970449",
        ["namabank"] = "970428", ["nama"] = "970428",
        ["abbank"] = "970425",
        ["vietabank"] = "970427",
        ["bacabank"] = "970409",
        ["pvcombank"] = "970412",
        ["scb"] = "970429",
        ["kienlongbank"] = "970452",
        ["ncb"] = "970419",
        ["shinhanbank"] = "970424", ["shinhan"] = "970424",
        ["woori"] = "970457", ["wooribank"] = "970457",
        ["bvbank"] = "970454", ["vietcapitalbank"] = "970454", ["timo"] = "970454",
        ["vietbank"] = "970433",
        ["pgbank"] = "970430",
        ["baovietbank"] = "970438",
        ["saigonbank"] = "970400",
        ["gpbank"] = "970408",
        ["dongabank"] = "970406",
        ["oceanbank"] = "970414",
        ["cake"] = "546034",
        ["ubank"] = "546035"
    };

    private readonly IBarcodeEncoder _encoder;

    public BankQrService(IBarcodeEncoder encoder)
    {
        _encoder = encoder;
    }

    /// <summary>
    /// Gets the Napas BIN of a bank. A 6-digit number in the name (e.g. "MB (970422)") wins over the name lookup.
    /// </summary>
    /// <returns>The BIN or <c>null</c> if the bank is unknown.</returns>
    public static string GetBankBin(string bankName)
    {
        if (bankName.IsEmpty())
        {
            return null;
        }

        var match = BinRegex().Match(bankName);
        if (match.Success)
        {
            return match.Value;
        }

        var key = NonLetterRegex().Replace(bankName.Replace('đ', 'd').Replace('Đ', 'D').RemoveDiacritics().ToLowerInvariant(), string.Empty);
        if (_bins.TryGetValue(key, out var bin))
        {
            return bin;
        }

        // "Ngân hàng Quân đội", "MB Bank", "NH ACB"...
        if (key.StartsWith("nganhang", StringComparison.Ordinal))
        {
            key = key[8..];
        }
        else if (key.StartsWith("nh", StringComparison.Ordinal))
        {
            key = key[2..];
        }

        if (_bins.TryGetValue(key, out bin) || (key.EndsWith("bank", StringComparison.Ordinal) && _bins.TryGetValue(key[..^4], out bin)))
        {
            return bin;
        }

        return null;
    }

    /// <summary>
    /// Generates an inline SVG QR code. With an order, amount and transfer content (order number) are prefilled.
    /// </summary>
    /// <returns>The SVG markup or <c>null</c> if the bank account is not configured or the bank is unknown.</returns>
    public string GenerateSvg(Split3DSettings settings, Order order = null)
    {
        var payload = CreatePayload(settings, order);
        if (payload == null)
        {
            return null;
        }

        var svg = _encoder.EncodeBarcode(payload).GenerateSvg(new BarcodeSvgOptions { Margin = 2 });

        // Remove the XML prolog and doctype so the markup can be embedded in HTML.
        var start = svg.IndexOf("<svg", StringComparison.Ordinal);
        return start > 0 ? svg[start..] : svg;
    }

    /// <summary>
    /// Generates the QR code as data URI, e.g. for HTML stored in the database.
    /// </summary>
    public string GenerateDataUri(Split3DSettings settings, Order order = null)
    {
        var svg = GenerateSvg(settings, order);
        return svg == null ? null : "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    private static VietQRPayload CreatePayload(Split3DSettings settings, Order order)
    {
        Guard.NotNull(settings);

        var bin = GetBankBin(settings.BankName);
        if (bin == null || settings.BankAccountNumber.IsEmpty())
        {
            return null;
        }

        long? amount = null;
        string purpose = null;

        if (order != null)
        {
            purpose = order.GetOrderNumber();

            if (order.CustomerCurrencyCode.EqualsNoCase("VND"))
            {
                amount = (long)Math.Round(order.OrderTotal * order.CurrencyRate, 0, MidpointRounding.AwayFromZero);
            }
        }

        return new VietQRPayload(bin, settings.BankAccountNumber, amount, purpose);
    }

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex BinRegex();

    [GeneratedRegex("[^a-z]")]
    private static partial Regex NonLetterRegex();
}
