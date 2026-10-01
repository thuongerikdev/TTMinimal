#nullable enable

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Turns an add-on as the author uploads it (features only, a Blender extension with blender_manifest.toml)
/// into the package the shop sells: injects the marketplace licensing module (<c>Marketplace/_ttlicense.py</c>),
/// wraps the add-on's register()/unregister() with it and optionally obfuscates everything with PyArmor.
/// </summary>
public static partial class MarketplacePackager
{
    private const string ManifestName = "blender_manifest.toml";
    private const string LicenseModule = "_ttlicense.py";
    private const string LicenseKeyFile = "_ttlicense_key.json";
    private const string TemplateResource = "Smartstore.Split3D.Marketplace._ttlicense.py";

    /// <summary>
    /// Marks an __init__.py that already carries the wrapper (re-upload of a packaged add-on).
    /// </summary>
    private const string WrapMarker = "# --- TT Minimal marketplace licensing";

    [GeneratedRegex(@"^\s*def\s+register\s*\(", RegexOptions.Multiline)]
    private static partial Regex RegisterRegex();

    [GeneratedRegex(@"^\s*def\s+unregister\s*\(", RegexOptions.Multiline)]
    private static partial Regex UnregisterRegex();

    [GeneratedRegex(@"^\[permissions\][^\[]*", RegexOptions.Multiline)]
    private static partial Regex PermissionsSectionRegex();

    [GeneratedRegex(@"^\s*network\s*=", RegexOptions.Multiline)]
    private static partial Regex NetworkPermissionRegex();

    public sealed class Options
    {
        public required string ProductCode { get; init; }
        public required string AddonName { get; init; }
        public required string ServerUrl { get; init; }
        public required string PublicKeyJson { get; init; }

        /// <summary>
        /// Python executable with PyArmor. <c>null</c> = no obfuscation.
        /// </summary>
        public string? PyArmorPython { get; init; }

        public string? PyArmorPlatforms { get; init; }
    }

    /// <summary>
    /// Packages <paramref name="zip"/>. Messages for the admin are appended to <paramref name="log"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The zip is no valid Blender extension.</exception>
    /// <exception cref="InvalidOperationException">Obfuscation failed.</exception>
    public static async Task<byte[]> PackageAsync(byte[] zip, Options options, List<string> log, CancellationToken cancelToken = default)
    {
        Guard.NotNull(zip);
        Guard.NotNull(options);

        var work = Path.Combine(Path.GetTempPath(), "ttminimal-pack-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(work, "src");

        try
        {
            try
            {
                ZipFile.ExtractToDirectory(new MemoryStream(zip), source);
            }
            catch (InvalidDataException)
            {
                throw new ArgumentException("The file is not a valid zip archive.");
            }

            var root = FindPackageRoot(source)
                ?? throw new ArgumentException($"{ManifestName} not found. Upload the add-on as a Blender extension (blender_manifest.toml at the root).");

            var initPath = Path.Combine(root, "__init__.py");
            if (!File.Exists(initPath))
            {
                throw new ArgumentException("__init__.py not found next to blender_manifest.toml.");
            }

            var init = await File.ReadAllTextAsync(initPath, Encoding.UTF8, cancelToken);
            if (!init.Contains(WrapMarker))
            {
                if (!RegisterRegex().IsMatch(init) || !UnregisterRegex().IsMatch(init))
                {
                    throw new ArgumentException("__init__.py must define register() and unregister().");
                }

                // Appended last, so it wraps the final register/unregister of the module.
                init = init.TrimEnd() + "\n\n\n" + WrapMarker + " (added by the shop, do not edit) ---\n"
                    + "from . import _ttlicense as _tt_license\n"
                    + "register, unregister = _tt_license.wrap(register, unregister)\n";
                await File.WriteAllTextAsync(initPath, init, new UTF8Encoding(false), cancelToken);
            }

            await File.WriteAllTextAsync(Path.Combine(root, LicenseModule), RenderModule(options), new UTF8Encoding(false), cancelToken);
            await File.WriteAllTextAsync(Path.Combine(root, LicenseKeyFile), options.PublicKeyJson.Trim(), new UTF8Encoding(false), cancelToken);
            await EnsureNetworkPermissionAsync(Path.Combine(root, ManifestName), cancelToken);

            log.Add($"Marketplace: licensing added for product \"{options.ProductCode}\" (key file drop, online check, update button).");

            var output = root;
            if (options.PyArmorPython.HasValue())
            {
                output = await ObfuscateAsync(root, Path.Combine(work, "out"), options, log, cancelToken);
            }
            else
            {
                log.Add("WARNING: obfuscation is off (Split3D settings > PyArmor): the source code is readable in the package.");
            }

            return CreateZip(output);
        }
        finally
        {
            try
            {
                Directory.Delete(work, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// A [a-z0-9_] id unique per product, used for operator and panel names inside Blender.
    /// </summary>
    public static string GetUid(string productCode)
    {
        var slug = Regex.Replace(productCode.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(productCode)))[..6];
        return (slug.Length > 20 ? slug[..20] : slug) + "_" + hash;
    }

    private static string RenderModule(Options options)
    {
        using var stream = typeof(MarketplacePackager).Assembly.GetManifestResourceStream(TemplateResource)
            ?? throw new InvalidOperationException($"Embedded resource {TemplateResource} is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var template = reader.ReadToEnd();

        return template
            .Replace("__TT_PRODUCT__", PythonString(options.ProductCode))
            .Replace("__TT_ADDON_NAME__", PythonString(options.AddonName))
            .Replace("__TT_SERVER_URL__", PythonString(options.ServerUrl.TrimEnd('/')))
            .Replace("__TT_UID__", GetUid(options.ProductCode));
    }

    // The values go inside single-quoted Python literals.
    private static string PythonString(string value)
        => value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", " ").Replace("\n", " ");

    private static string? FindPackageRoot(string folder)
    {
        if (File.Exists(Path.Combine(folder, ManifestName)))
        {
            return folder;
        }

        // Blender also accepts the files inside one top-level folder.
        var dirs = Directory.GetDirectories(folder);
        return dirs.Length == 1 && File.Exists(Path.Combine(dirs[0], ManifestName)) ? dirs[0] : null;
    }

    private static async Task EnsureNetworkPermissionAsync(string manifestPath, CancellationToken cancelToken)
    {
        var toml = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8, cancelToken);
        const string line = "network = \"Kiểm tra key bản quyền và bản cập nhật với shop\"";

        var section = PermissionsSectionRegex().Match(toml);
        if (section.Success)
        {
            if (NetworkPermissionRegex().IsMatch(section.Value))
            {
                return;
            }

            var headerEnd = toml.IndexOf('\n', section.Index);
            toml = headerEnd < 0 ? toml + "\n" + line + "\n" : toml.Insert(headerEnd + 1, line + "\n");
        }
        else
        {
            toml = toml.TrimEnd() + "\n\n[permissions]\n" + line + "\n";
        }

        await File.WriteAllTextAsync(manifestPath, toml, new UTF8Encoding(false), cancelToken);
    }

    /// <summary>
    /// Runs <c>python -m pyarmor.cli gen</c> on the package and copies the non-Python files next to the result.
    /// </summary>
    private static async Task<string> ObfuscateAsync(string root, string outFolder, Options options, List<string> log, CancellationToken cancelToken)
    {
        Directory.CreateDirectory(outFolder);

        var psi = new ProcessStartInfo
        {
            FileName = options.PyArmorPython!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(root)!
        };

        // -i: the runtime package goes inside the add-on package, as Blender loads it as one package.
        foreach (var arg in new[] { "-m", "pyarmor.cli", "gen", "-O", outFolder, "-r", "-i" })
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var platform in (options.PyArmorPlatforms ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            psi.ArgumentList.Add("--platform");
            psi.ArgumentList.Add(platform);
        }

        psi.ArgumentList.Add(root);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException($"PyArmor: cannot start \"{options.PyArmorPython}\": {ex.Message}");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancelToken);
        var stderr = process.StandardError.ReadToEndAsync(cancelToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            throw new InvalidOperationException("PyArmor did not finish within 5 minutes.");
        }

        var output = (await stdout + "\n" + await stderr).Trim();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"PyArmor failed (exit code {process.ExitCode}): {output.Truncate(1500)}");
        }

        // pyarmor gen -r writes <out>/<package name>/ with the obfuscated .py files and the runtime.
        var packaged = Path.Combine(outFolder, Path.GetFileName(root));
        if (!Directory.Exists(packaged))
        {
            throw new InvalidOperationException($"PyArmor output folder not found: {packaged}. {output.Truncate(500)}");
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase) || file.Contains("__pycache__"))
            {
                continue;
            }

            var target = Path.Combine(packaged, Path.GetRelativePath(root, file));
            if (!File.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }

        log.Add($"Marketplace: obfuscated with PyArmor ({options.PyArmorPlatforms}).");
        return packaged;
    }

    private static byte[] CreateZip(string folder)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (file.Contains("__pycache__"))
                {
                    continue;
                }

                var name = Path.GetRelativePath(folder, file).Replace('\\', '/');
                archive.CreateEntryFromFile(file, name, CompressionLevel.Optimal);
            }
        }

        return buffer.ToArray();
    }
}
