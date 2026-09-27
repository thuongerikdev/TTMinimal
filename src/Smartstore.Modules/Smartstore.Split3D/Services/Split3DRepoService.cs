#nullable enable

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Per-key Blender extension repository: Blender reads /split3d/repo/{token}/index.json and
/// installs or updates the addon of the key from there, as long as the key is valid.
/// </summary>
public partial class Split3DRepoService
{
    [GeneratedRegex("^[a-f0-9]{32}$")]
    private static partial Regex TokenRegex();

    private readonly SmartDbContext _db;
    private readonly IDownloadService _downloadService;
    private readonly IMemoryCache _cache;

    public Split3DRepoService(SmartDbContext db, IDownloadService downloadService, IMemoryCache cache)
    {
        _db = db;
        _downloadService = downloadService;
        _cache = cache;
    }

    /// <summary>
    /// Gets the repository token of <paramref name="license"/>, creating one if needed (without committing).
    /// </summary>
    public static string EnsureToken(Split3DLicense license)
    {
        Guard.NotNull(license);

        if (license.RepoToken.IsEmpty())
        {
            license.RepoToken = NewToken();
        }

        return license.RepoToken!;
    }

    public static string NewToken()
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Absolute URL of the repository folder, ending with "/". Built by hand on purpose: Smartstore's URL
    /// generation may append a trailing slash ("…/index.json/", "…zip/"), and Blender only accepts a dropped
    /// install URL whose path ends in ".zip" and resolves "./file.zip" against the index URL.
    /// </summary>
    public static string GetFolderUrl(HttpRequest request, string token)
    {
        Guard.NotNull(request);
        Guard.NotEmpty(token);

        return $"{request.Scheme}://{request.Host}{request.PathBase}/split3d/repo/{token}/";
    }

    /// <summary>
    /// Link to drag into Blender: installs the package and adds the repository in one step.
    /// </summary>
    public static string GetInstallUrl(HttpRequest request, string token, Split3DPackageInfo package)
    {
        Guard.NotNull(package);

        var minVersion = package.Fields.FirstOrDefault(x => x.Key == "blender_version_min").Value as string ?? "4.2.0";
        return GetFolderUrl(request, token) + package.FileName
            + "?repository=.%2Findex.json&blender_version_min=" + Uri.EscapeDataString(minVersion);
    }

    public Task<Split3DLicense?> FindByTokenAsync(string? token, CancellationToken cancelToken = default)
    {
        if (token == null || !TokenRegex().IsMatch(token))
        {
            return Task.FromResult<Split3DLicense?>(null);
        }

        return _db.Split3DLicenses().AsNoTracking().FirstOrDefaultAsync(x => x.RepoToken == token, cancelToken);
    }

    /// <summary>
    /// Only valid keys receive new versions.
    /// </summary>
    public static bool CanUpdate(Split3DLicense license)
        => !license.Blocked && (license.ExpiresOnUtc == null || license.ExpiresOnUtc > DateTime.UtcNow);

    /// <summary>
    /// The newest uploaded file of <paramref name="addonId"/> that is a Blender extension package.
    /// Package metadata is cached per download, so the zip is only read once.
    /// </summary>
    public async Task<(Download Download, Split3DPackageInfo Package)?> GetLatestPackageAsync(int addonId, CancellationToken cancelToken = default)
    {
        var productIds = await _db.Split3DAddonProducts()
            .Where(x => x.AddonId == addonId)
            .Select(x => x.ProductId)
            .ToListAsync(cancelToken);

        if (productIds.Count == 0)
        {
            return null;
        }

        // Every plan product of an addon gets the same file; one download per version is enough.
        var downloads = (await _db.Downloads
            .AsNoTracking()
            .Include(x => x.MediaFile)
            .Where(x => x.EntityName == nameof(Product) && productIds.Contains(x.EntityId) && x.MediaFileId != null)
            .ToListAsync(cancelToken))
            .GroupBy(x => x.FileVersion ?? string.Empty)
            .Select(g => g.OrderByDescending(x => x.UpdatedOnUtc).First())
            .OrderByDescending(x => x.FileVersion, Comparer<string?>.Create(Split3DAddonPackage.CompareVersions))
            .ThenByDescending(x => x.UpdatedOnUtc);

        foreach (var download in downloads)
        {
            var package = await GetPackageInfoAsync(download);
            if (package != null)
            {
                return (download, package);
            }
        }

        return null;
    }

    public Task<Stream> OpenAsync(Download download)
        => _downloadService.OpenDownloadStreamAsync(download);

    private async Task<Split3DPackageInfo?> GetPackageInfoAsync(Download download)
    {
        var key = $"split3d:package:{download.Id}:{download.UpdatedOnUtc.Ticks}:{download.MediaFile?.Size}";
        if (_cache.TryGetValue(key, out Split3DPackageInfo? cached))
        {
            return cached;
        }

        Split3DPackageInfo? package = null;
        try
        {
            await using var stream = await OpenAsync(download);
            if (stream != null)
            {
                package = Split3DAddonPackage.Read(await Split3DStorefrontSetup.ReadAllBytesAsync(stream, default));
            }
        }
        catch (ArgumentException)
        {
            // Not a valid extension package: treated like a legacy add-on.
        }

        _cache.Set(key, package, TimeSpan.FromHours(6));
        return package;
    }
}
