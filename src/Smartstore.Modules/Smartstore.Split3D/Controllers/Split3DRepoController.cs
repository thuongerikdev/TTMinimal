using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Identity;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Blender extension repository of one key (Extension Listing API v1). Blender reads index.json to offer
/// "Update" in Preferences > Get Extensions and downloads the zip from the same folder.
/// Anonymous by design: the random token in the URL is the credential.
/// </summary>
[Route("split3d/repo/{token}")]
public class Split3DRepoController : Controller
{
    private static readonly JsonWriterOptions _jsonOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly Split3DRepoService _repoService;

    public Split3DRepoController(Split3DRepoService repoService)
    {
        _repoService = repoService;
    }

    [HttpGet("index.json"), WebhookEndpoint]
    public async Task<IActionResult> Index(string token)
    {
        var license = await _repoService.FindByTokenAsync(token, HttpContext.RequestAborted);
        if (license == null)
        {
            return NotFound();
        }

        // Expired or blocked keys keep what is installed but get no new versions.
        var latest = Split3DRepoService.CanUpdate(license)
            ? await _repoService.GetLatestPackageAsync(license.AddonId, HttpContext.RequestAborted)
            : null;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, _jsonOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("version", "v1");
            writer.WriteStartArray("blocklist");
            writer.WriteEndArray();
            writer.WriteStartArray("data");

            if (latest is { } entry)
            {
                var package = entry.Package;
                writer.WriteStartObject();
                foreach (var (key, value) in package.Fields)
                {
                    WriteValue(writer, key, value);
                }
                // Relative to index.json, like "blender --command extension server-generate" writes it.
                writer.WriteString("archive_url", "./" + package.FileName);
                writer.WriteNumber("archive_size", package.Size);
                writer.WriteString("archive_hash", package.Hash);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        Response.Headers.CacheControl = "no-store";
        return File(stream.ToArray(), "application/json");
    }

    [HttpGet("{fileName}"), WebhookEndpoint]
    public async Task<IActionResult> Download(string token, string fileName)
    {
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var license = await _repoService.FindByTokenAsync(token, HttpContext.RequestAborted);
        if (license == null)
        {
            return NotFound();
        }

        if (!Split3DRepoService.CanUpdate(license))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        // "latest.zip" (download button of the key email) always serves the newest version under its real file name.
        var latest = await _repoService.GetLatestPackageAsync(license.AddonId, HttpContext.RequestAborted);
        if (latest is not { } entry
            || (!fileName.Equals(Split3DRepoService.LatestFileName, StringComparison.OrdinalIgnoreCase)
                && !entry.Package.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
        {
            return NotFound();
        }

        var stream = await _repoService.OpenAsync(entry.Download);
        if (stream == null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return File(stream, "application/zip", entry.Package.FileName);
    }

    private static void WriteValue(Utf8JsonWriter writer, string key, object value)
    {
        switch (value)
        {
            case string s:
                writer.WriteString(key, s);
                break;
            case List<string> list:
                writer.WriteStartArray(key);
                foreach (var item in list)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
                break;
            case List<KeyValuePair<string, object>> table:
                writer.WriteStartObject(key);
                foreach (var (k, v) in table)
                {
                    WriteValue(writer, k, v);
                }
                writer.WriteEndObject();
                break;
        }
    }
}
