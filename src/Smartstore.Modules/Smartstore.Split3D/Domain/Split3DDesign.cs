#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using Smartstore.Domain;

namespace Smartstore.Split3D.Domain;

/// <summary>
/// A design made in the 3D designer of a product page (name plate, class board, QR plate): the full spec the
/// preview is built from, saved when the customer adds it to the cart. Its code travels with the cart line in the
/// "Thiết kế" attribute (<see cref="DisplayPrefix"/> + code), so the admin order page can rebuild the print files.
/// The code is derived from the spec, so the same design is stored once.
/// </summary>
[Table("Split3DDesign")]
[Index(nameof(Code), IsUnique = true)]
public class Split3DDesign : BaseEntity
{
    /// <summary>Marker in front of the code in the design attribute, e.g. "TT3D-ABCDE23456".</summary>
    public const string DisplayPrefix = "TT3D-";

    public const int CodeLength = 10;

    /// <summary>Base32 letters of the SHA-256 of <see cref="SpecJson"/>.</summary>
    [Required, StringLength(20)]
    public string Code { get; set; } = default!;

    /// <summary>Product kind of the designer: nameplate, classboard, qr, keycap.</summary>
    [StringLength(30)]
    public string? Kind { get; set; }

    /// <summary>The spec as sent by the designer (studio-designs.js, Panel.spec).</summary>
    [Required]
    public string SpecJson { get; set; } = default!;

    public DateTime CreatedOnUtc { get; set; }

    /// <summary>The code of a spec: the first <see cref="CodeLength"/> base32 letters of its SHA-256.</summary>
    public static string CodeOf(string specJson)
    {
        Guard.NotEmpty(specJson);

        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(specJson));
        var sb = new StringBuilder(CodeLength);
        int buffer = 0, bits = 0, i = 0;
        while (sb.Length < CodeLength)
        {
            if (bits < 5)
            {
                buffer = (buffer << 8) | hash[i++];
                bits += 8;
            }

            sb.Append(alphabet[(buffer >> (bits - 5)) & 31]);
            bits -= 5;
        }

        return sb.ToString();
    }
}
