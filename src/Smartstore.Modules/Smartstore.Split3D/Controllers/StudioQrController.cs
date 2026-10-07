#nullable enable

using System.Text;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Split3D.Services;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// QR matrix for the 3D QR plate designer (studio-designs.js): builds the payload of the chosen kind (link, WiFi,
/// VietQR bank transfer, Zalo, phone, text) and returns the modules as rows of '0' / '1'. Encoded with the
/// Barcoder library Smartstore uses for its own barcodes. A pure function without state; POST keeps WiFi passwords
/// out of URLs and logs.
/// </summary>
[Route("studio/qr")]
public class StudioQrController : PublicController
{
    private const int MaxPayloadLength = 1200;

    [HttpPost("")]
    [IgnoreAntiforgeryToken]
    public IActionResult Matrix(QrRequest request)
    {
        var payload = BuildPayload(request, out var error);
        if (payload == null)
        {
            return Json(new { error });
        }

        if (payload.Length > MaxPayloadLength)
        {
            return Json(new { error = "Nội dung quá dài cho một mã QR in được (tối đa " + MaxPayloadLength + " ký tự)." });
        }

        var level = request.Ecc switch
        {
            "L" => Barcoder.Qr.ErrorCorrectionLevel.L,
            "Q" => Barcoder.Qr.ErrorCorrectionLevel.Q,
            "H" => Barcoder.Qr.ErrorCorrectionLevel.H,
            _ => Barcoder.Qr.ErrorCorrectionLevel.M
        };

        var code = Barcoder.Qr.QrEncoder.Encode(payload, level, Barcoder.Qr.Encoding.Auto);
        var size = code.Bounds.X;
        var rows = new string[code.Bounds.Y];
        var sb = new StringBuilder(size);
        for (var y = 0; y < rows.Length; y++)
        {
            sb.Clear();
            for (var x = 0; x < size; x++)
            {
                sb.Append(code.At(x, y) ? '1' : '0');
            }

            rows[y] = sb.ToString();
        }

        return Json(new { size, rows });
    }

    private static string? BuildPayload(QrRequest r, out string? error)
    {
        error = null;
        switch (r.Type)
        {
            case "wifi":
                if (r.Ssid.IsEmpty())
                {
                    error = "Nhập tên WiFi.";
                    return null;
                }

                var security = r.Security is "WEP" or "nopass" ? r.Security : "WPA";
                return "WIFI:T:" + security + ";S:" + WifiEscape(r.Ssid!) + ";"
                    + (security == "nopass" ? string.Empty : "P:" + WifiEscape(r.Password ?? string.Empty) + ";")
                    + (r.Hidden ? "H:true;" : string.Empty) + ";";
            case "bank":
                var bin = BankQrService.GetBankBin(r.Bank);
                if (bin == null)
                {
                    error = "Không nhận ra ngân hàng — ghi tên ngân hàng (VD: MB, Vietcombank) hoặc mã BIN 6 số.";
                    return null;
                }

                var account = new string((r.Account ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
                if (account.Length == 0)
                {
                    error = "Nhập số tài khoản.";
                    return null;
                }

                return new VietQRPayload(bin, account, r.Amount > 0 ? r.Amount : null, r.Text.NullEmpty()).Serialize();
            case "zalo":
            case "phone":
                var digits = new string((r.Text ?? string.Empty).Where(c => char.IsDigit(c) || c == '+').ToArray());
                if (digits.Length < 8)
                {
                    error = "Nhập số điện thoại.";
                    return null;
                }

                return r.Type == "zalo" ? "https://zalo.me/" + digits.TrimStart('+') : "tel:" + digits;
            case "url":
                var url = r.Text?.Trim();
                if (url.IsEmpty())
                {
                    error = "Nhập đường link.";
                    return null;
                }

                return url!.Contains("://", StringComparison.Ordinal) ? url : "https://" + url;
            default:
                if (r.Text.IsEmpty())
                {
                    error = "Nhập nội dung.";
                    return null;
                }

                return r.Text!.Trim();
        }
    }

    // Special characters of the WiFi QR format.
    private static string WifiEscape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '\\' or ';' or ',' or ':' or '"')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    public class QrRequest
    {
        /// <summary>
        /// url, wifi, bank, zalo, phone or text.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Link, phone number, text, or the transfer content of a bank QR.
        /// </summary>
        public string? Text { get; set; }

        public string? Ssid { get; set; }
        public string? Password { get; set; }

        /// <summary>
        /// WPA, WEP or nopass.
        /// </summary>
        public string? Security { get; set; }

        public bool Hidden { get; set; }
        public string? Bank { get; set; }
        public string? Account { get; set; }
        public long? Amount { get; set; }

        /// <summary>
        /// Error correction: L, M, Q or H (H when an icon covers the middle).
        /// </summary>
        public string? Ecc { get; set; }
    }
}
