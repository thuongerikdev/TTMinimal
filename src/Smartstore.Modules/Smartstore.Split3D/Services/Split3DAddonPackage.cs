#nullable enable

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Metadata of a Blender extension package (zip with blender_manifest.toml).
/// </summary>
public sealed class Split3DPackageInfo
{
    /// <summary>
    /// Manifest "id". Blender replaces an installed extension with a package of the same id.
    /// </summary>
    public required string Id { get; init; }

    public required string Version { get; init; }

    /// <summary>
    /// Top-level manifest values in file order (string, list of strings or a table of strings),
    /// without the [build] table. These are the fields of an extension listing entry.
    /// </summary>
    public required List<KeyValuePair<string, object>> Fields { get; init; }

    public long Size { get; init; }

    /// <summary>
    /// "sha256:&lt;hex&gt;" of the package bytes.
    /// </summary>
    public required string Hash { get; init; }

    public string FileName => $"{Id}-{Version}.zip";
}

/// <summary>
/// Reads and prepares Blender addon packages uploaded in the admin area.
/// </summary>
public static partial class Split3DAddonPackage
{
    private const string ManifestName = "blender_manifest.toml";

    [GeneratedRegex(@"^SERVER_URL\s*=\s*['""][^'""]*['""]", RegexOptions.Multiline)]
    private static partial Regex ServerUrlRegex();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex SemVerRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex IdRegex();

    /// <summary>
    /// Reads the manifest of <paramref name="zip"/>. <c>null</c> for legacy add-ons without a manifest.
    /// </summary>
    /// <exception cref="ArgumentException">The file is no zip or the manifest is invalid.</exception>
    public static Split3DPackageInfo? Read(byte[] zip)
    {
        Guard.NotNull(zip);

        string? toml;
        try
        {
            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            // Blender accepts the manifest at the root or inside one top-level folder.
            var entry = archive.Entries
                .Where(x => x.Name == ManifestName && x.FullName.Count(c => c == '/') <= 1)
                .OrderBy(x => x.FullName.Length)
                .FirstOrDefault();

            if (entry == null)
            {
                return null;
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            toml = reader.ReadToEnd();
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("The file is not a valid zip archive.");
        }

        var fields = ParseManifest(toml);
        var id = fields.FirstOrDefault(x => x.Key == "id").Value as string;
        var version = fields.FirstOrDefault(x => x.Key == "version").Value as string;

        if (id == null || !IdRegex().IsMatch(id))
        {
            throw new ArgumentException($"{ManifestName}: \"id\" is missing or invalid.");
        }

        if (version == null || !SemVerRegex().IsMatch(version))
        {
            throw new ArgumentException($"{ManifestName}: \"version\" must look like 1.2.3.");
        }

        return new Split3DPackageInfo
        {
            Id = id,
            Version = version,
            Fields = fields,
            Size = zip.LongLength,
            Hash = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(zip))
        };
    }

    /// <summary>
    /// Writes <paramref name="serverUrl"/> as SERVER_URL into the addon's online.py, so the addon talks to this shop.
    /// </summary>
    /// <returns>The patched zip, or the original bytes if the zip has no online.py with SERVER_URL.</returns>
    public static byte[] SetServerUrl(byte[] zip, string serverUrl, out bool patched)
    {
        Guard.NotNull(zip);
        Guard.NotEmpty(serverUrl);

        patched = false;
        serverUrl = serverUrl.Trim().TrimEnd('/');
        if (serverUrl.Contains('\'') || serverUrl.Contains('\\'))
        {
            throw new ArgumentException("Invalid server URL.");
        }

        using var source = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var online = source.Entries.FirstOrDefault(x => x.Name == "online.py" && x.FullName.Count(c => c == '/') <= 1);
        if (online == null)
        {
            return zip;
        }

        string code;
        using (var reader = new StreamReader(online.Open(), Encoding.UTF8))
        {
            code = reader.ReadToEnd();
        }

        var line = $"SERVER_URL = '{serverUrl}'";
        if (!ServerUrlRegex().IsMatch(code))
        {
            return zip;
        }

        var newCode = ServerUrlRegex().Replace(code, line, 1);
        if (newCode == code)
        {
            patched = true;
            return zip;
        }

        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                copy.LastWriteTime = entry.LastWriteTime;

                using var to = copy.Open();
                if (entry == online)
                {
                    to.Write(new UTF8Encoding(false).GetBytes(newCode));
                }
                else if (!entry.FullName.EndsWith('/'))
                {
                    using var from = entry.Open();
                    from.CopyTo(to);
                }
            }
        }

        patched = true;
        return output.ToArray();
    }

    /// <summary>
    /// Compares "1.2.3" style versions. Unparsable versions sort first.
    /// </summary>
    public static int CompareVersions(string? a, string? b)
    {
        var va = System.Version.TryParse(a, out var x) ? x : new System.Version(0, 0);
        var vb = System.Version.TryParse(b, out var y) ? y : new System.Version(0, 0);
        return va.CompareTo(vb);
    }

    /// <summary>
    /// Parses the subset of TOML used by blender_manifest.toml: strings, string arrays (also multi-line)
    /// and tables of strings. The [build] table is skipped because it is not part of a listing.
    /// </summary>
    private static List<KeyValuePair<string, object>> ParseManifest(string toml)
    {
        var result = new List<KeyValuePair<string, object>>();
        List<KeyValuePair<string, object>>? table = null;
        var skipTable = false;
        var lines = toml.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']') && !line.Contains('='))
            {
                var name = line.Trim('[', ']').Trim();
                skipTable = name == "build";
                table = null;
                if (!skipTable)
                {
                    table = [];
                    result.Add(new(name, table));
                }
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                throw new ArgumentException($"{ManifestName}: cannot read line {i + 1}.");
            }

            var key = line[..eq].Trim().Trim('"');
            var raw = line[(eq + 1)..].Trim();

            // Multi-line arrays continue until the closing bracket.
            if (raw.StartsWith('[') && !raw.Contains(']'))
            {
                var sb = new StringBuilder(raw);
                while (++i < lines.Length)
                {
                    var next = StripComment(lines[i]).Trim();
                    sb.Append(' ').Append(next);
                    if (next.Contains(']'))
                    {
                        break;
                    }
                }
                raw = sb.ToString();
            }

            if (skipTable)
            {
                continue;
            }

            object value = raw.StartsWith('[') ? ParseArray(raw) : ParseString(raw);
            (table ?? result).Add(new(key, value));
        }

        return result;
    }

    private static List<string> ParseArray(string raw)
    {
        var inner = raw.Trim().TrimStart('[').TrimEnd(']');
        var items = new List<string>();
        foreach (Match m in Regex.Matches(inner, "\"((?:[^\"\\\\]|\\\\.)*)\"|'([^']*)'"))
        {
            items.Add(m.Groups[1].Success ? Unescape(m.Groups[1].Value) : m.Groups[2].Value);
        }

        return items;
    }

    private static string ParseString(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            return Unescape(raw[1..^1]);
        }

        if (raw.Length >= 2 && raw[0] == '\'' && raw[^1] == '\'')
        {
            return raw[1..^1];
        }

        return raw;
    }

    private static string Unescape(string value)
        => value.Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static string StripComment(string line)
    {
        var inString = false;
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inString)
            {
                if (c == '\\' && quote == '"')
                {
                    i++;
                }
                else if (c == quote)
                {
                    inString = false;
                }
            }
            else if (c is '"' or '\'')
            {
                inString = true;
                quote = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }

        return line;
    }
}
