using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Smartstore.Imaging.Barcodes;

namespace Smartstore.Split3D.Services;

/// <summary>
/// VietQR (Napas 247) bank transfer payload, following the EMVCo merchant-presented QR specification.
/// Any Vietnamese banking app (and MoMo, ZaloPay) can scan it and prefills account, amount and transfer content.
/// </summary>
public partial class VietQRPayload : QrPayload
{
    const string NapasGuid = "A000000727";
    const string ServiceTransferToAccount = "QRIBFTTA";

    /// <param name="bankBin">6-digit Napas bank identification number, e.g. 970422 for MB.</param>
    /// <param name="accountNumber">Account number of the beneficiary.</param>
    /// <param name="amount">Amount in VND, or <c>null</c> to let the payer enter it.</param>
    /// <param name="purpose">Transfer content, or <c>null</c> to let the payer enter it.</param>
    public VietQRPayload(string bankBin, string accountNumber, long? amount = null, string purpose = null)
    {
        Guard.NotEmpty(bankBin);
        Guard.NotEmpty(accountNumber);

        BankBin = bankBin;
        AccountNumber = accountNumber.Replace(" ", string.Empty);
        Amount = amount > 0 ? amount : null;
        Purpose = SanitizePurpose(purpose);
    }

    public string BankBin { get; }
    public string AccountNumber { get; }
    public long? Amount { get; }
    public string Purpose { get; }

    public override string Serialize()
    {
        var beneficiary = Field("00", BankBin) + Field("01", AccountNumber);
        var merchantInfo = Field("00", NapasGuid) + Field("01", beneficiary) + Field("02", ServiceTransferToAccount);

        var sb = new StringBuilder()
            .Append(Field("00", "01"))
            // 11 = static (reusable) code, 12 = dynamic code for a single payment.
            .Append(Field("01", Amount.HasValue || Purpose != null ? "12" : "11"))
            .Append(Field("38", merchantInfo))
            .Append(Field("53", "704"));

        if (Amount.HasValue)
        {
            sb.Append(Field("54", Amount.Value.ToString(CultureInfo.InvariantCulture)));
        }

        sb.Append(Field("58", "VN"));

        if (Purpose != null)
        {
            sb.Append(Field("62", Field("08", Purpose)));
        }

        sb.Append("6304");
        sb.Append(Crc16(sb.ToString()).ToString("X4"));

        return sb.ToString();
    }

    private static string Field(string id, string value)
        => id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;

    /// <summary>
    /// Banks only accept plain ASCII letters, digits and spaces in the transfer content.
    /// </summary>
    private static string SanitizePurpose(string purpose)
    {
        if (purpose.IsEmpty())
        {
            return null;
        }

        var result = NonAsciiRegex().Replace(purpose.Replace('đ', 'd').Replace('Đ', 'D').RemoveDiacritics(), string.Empty).Trim();
        if (result.Length > 25)
        {
            result = result[..25];
        }

        return result.NullEmpty();
    }

    /// <summary>
    /// CRC-16/CCITT-FALSE as required by EMVCo.
    /// </summary>
    private static ushort Crc16(string data)
    {
        ushort crc = 0xFFFF;

        foreach (var b in Encoding.ASCII.GetBytes(data))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    [GeneratedRegex("[^A-Za-z0-9 ]")]
    private static partial Regex NonAsciiRegex();
}
