using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Data;
using Smartstore.Core.Messaging;
using Smartstore.Core.Stores;
using Smartstore.Engine;
using Smartstore.IO;
using Smartstore.Utilities;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Stores 3D printing quote requests with their model files and notifies the studio by email.
/// </summary>
public class PrintQuoteService
{
    /// <summary>
    /// Folder below the tenant root (App_Data/Tenants/{tenant}) holding uploaded model files.
    /// </summary>
    public const string FileFolder = "PrintQuotes";

    /// <summary>
    /// <see cref="PrintQuoteRequest.Technology"/> of a design request (page "Thiết kế"), as opposed to a print request.
    /// </summary>
    public const string DesignTechnology = "Thiết kế 3D";

    public static readonly string[] AllowedExtensions =
    [
        ".stl", ".obj", ".3mf", ".step", ".stp", ".igs", ".iges", ".fbx", ".blend", ".ply", ".amf",
        ".zip", ".rar", ".7z", ".png", ".jpg", ".jpeg", ".webp", ".pdf"
    ];

    private readonly SmartDbContext _db;
    private readonly IApplicationContext _appContext;
    private readonly IEmailAccountService _emailAccountService;
    private readonly IStoreContext _storeContext;
    private readonly StudioSettings _settings;

    public PrintQuoteService(
        SmartDbContext db,
        IApplicationContext appContext,
        IEmailAccountService emailAccountService,
        IStoreContext storeContext,
        StudioSettings settings)
    {
        _db = db;
        _appContext = appContext;
        _emailAccountService = emailAccountService;
        _storeContext = storeContext;
        _settings = settings;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    public long MaxFileSize => Math.Clamp(_settings.QuoteMaxFileSizeMb, 1, 500) * 1024L * 1024L;

    public static bool IsAllowedFile(string fileName)
        => AllowedExtensions.Contains(Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant());

    /// <summary>
    /// Saves a new request, stores its file and queues the notification email.
    /// </summary>
    public Task SubmitAsync(PrintQuoteRequest request, IFormFile file, CancellationToken cancelToken = default)
        => SubmitAsync(request, file != null ? [file] : [], cancelToken);

    /// <summary>
    /// Saves a new request, stores its files and queues the notification email.
    /// Several files are stored together as one ZIP archive.
    /// </summary>
    public Task SubmitAsync(PrintQuoteRequest request, IReadOnlyCollection<IFormFile> files, CancellationToken cancelToken = default)
        => SaveAsync(request, files, true, cancelToken);

    /// <summary>
    /// Saves a request the studio enters itself in the admin area (customer called or messaged).
    /// Stores its files like <see cref="SubmitAsync(PrintQuoteRequest, IReadOnlyCollection{IFormFile}, CancellationToken)"/>
    /// but sends no notification email and keeps the status the studio picked.
    /// </summary>
    public Task CreateAsync(PrintQuoteRequest request, IReadOnlyCollection<IFormFile> files, CancellationToken cancelToken = default)
        => SaveAsync(request, files, false, cancelToken);

    private async Task SaveAsync(PrintQuoteRequest request, IReadOnlyCollection<IFormFile> files, bool notify, CancellationToken cancelToken)
    {
        Guard.NotNull(request);

        request.CreatedOnUtc = request.UpdatedOnUtc = DateTime.UtcNow;
        if (notify)
        {
            request.Status = PrintQuoteStatus.New;
        }

        _db.PrintQuoteRequests().Add(request);
        await _db.SaveChangesAsync(cancelToken);

        var uploads = (files ?? []).Where(x => x != null && x.Length > 0).ToList();
        if (uploads.Count > 0)
        {
            var single = uploads.Count == 1;
            var fileName = single ? Path.GetFileName(uploads[0].FileName) : $"models-{request.Id}.zip";
            var path = PathUtility.Join(FileFolder, request.CreatedOnUtc.ToString("yyyy-MM"), $"{request.Id}-{SafeFileName(fileName)}");
            var target = await _appContext.TenantRoot.GetFileAsync(path);

            if (single)
            {
                using var stream = uploads[0].OpenReadStream();
                await target.CreateAsync(stream, true, cancelToken);
            }
            else
            {
                await using var output = await target.OpenWriteAsync("application/zip", cancelToken);
                using var archive = new ZipArchive(output, ZipArchiveMode.Create);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var upload in uploads)
                {
                    // Same name twice: the later one gets a numeric suffix.
                    var entryName = SafeFileName(upload.FileName);
                    for (var i = 2; !names.Add(entryName); i++)
                    {
                        entryName = $"{Path.GetFileNameWithoutExtension(SafeFileName(upload.FileName))}-{i}{Path.GetExtension(upload.FileName)}";
                    }

                    // Model files are usually already compressed poorly; fastest keeps big uploads quick.
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    using var source = upload.OpenReadStream();
                    await source.CopyToAsync(entryStream, cancelToken);
                }
            }

            request.FileName = fileName.Truncate(400);
            request.FilePath = path;
            request.FileSize = (await _appContext.TenantRoot.GetFileAsync(path)).Length;
            await _db.SaveChangesAsync(cancelToken);
        }

        if (!notify)
        {
            return;
        }

        try
        {
            QueueNotification(request);
            await _db.SaveChangesAsync(cancelToken);
        }
        catch (Exception ex)
        {
            // The request is saved and visible in the admin area, a failed email must not lose it.
            Logger.Error(ex, $"Print quote {request.Id}: notification email failed.");
        }
    }

    /// <summary>
    /// Gets the uploaded model file of a request, or <c>null</c> if there is none.
    /// </summary>
    public async Task<IFile> GetFileAsync(PrintQuoteRequest request)
    {
        if (request?.FilePath.IsEmpty() ?? true)
        {
            return null;
        }

        var file = await _appContext.TenantRoot.GetFileAsync(request.FilePath);
        return file.Exists ? file : null;
    }

    /// <summary>
    /// Deletes requests together with their uploaded files.
    /// </summary>
    public async Task<int> DeleteAsync(IEnumerable<PrintQuoteRequest> requests, CancellationToken cancelToken = default)
    {
        var list = requests.ToList();
        foreach (var request in list)
        {
            var file = await GetFileAsync(request);
            if (file != null)
            {
                await file.DeleteAsync(cancelToken);
            }
        }

        _db.PrintQuoteRequests().RemoveRange(list);
        return await _db.SaveChangesAsync(cancelToken);
    }

    private void QueueNotification(PrintQuoteRequest request)
    {
        var emailAccount = _emailAccountService.GetDefaultEmailAccount();
        if (emailAccount == null)
        {
            Logger.Warn("Print quote: no email account configured, notification not sent.");
            return;
        }

        var to = _settings.QuoteNotifyEmail.NullEmpty() ?? emailAccount.Email;
        var adminUrl = _storeContext.CurrentStore.GetBaseUrl().TrimEnd('/') + "/admin/printquote/edit/" + request.Id;

        string Row(string label, string value) => value.IsEmpty()
            ? string.Empty
            : $"<tr><td style=\"padding:4px 12px 4px 0;color:#64748b\">{label}</td><td style=\"padding:4px 0\"><strong>{WebUtility.HtmlEncode(value)}</strong></td></tr>";

        var body =
            $"<p>Có yêu cầu báo giá in 3D mới <strong>#{request.Id}</strong>.</p>" +
            "<table style=\"border-collapse:collapse\">" +
            Row("Khách hàng", request.Name) +
            Row("Điện thoại", request.Phone) +
            Row("Email", request.Email) +
            Row("Công nghệ", request.Technology) +
            Row("Vật liệu / màu", request.Material) +
            Row("Số lượng", request.Quantity.ToString()) +
            Row("Khối lượng ước tính", request.EstimatedGrams?.ToString("N0") + (request.EstimatedGrams.HasValue ? " g" : string.Empty)) +
            Row("Cần thiết kế / sửa file", request.NeedsDesign ? "Có" : null) +
            Row("File", request.FileName.HasValue() ? $"{request.FileName} ({Prettifier.HumanizeBytes(request.FileSize)})" : null) +
            Row("Link file", request.FileLink) +
            "</table>" +
            (request.Note.HasValue() ? $"<p style=\"white-space:pre-wrap\">{WebUtility.HtmlEncode(request.Note)}</p>" : string.Empty) +
            $"<p><a href=\"{adminUrl}\">Mở yêu cầu trong trang quản trị</a></p>";

        _db.QueuedEmails.Add(new QueuedEmail
        {
            From = emailAccount.ToMailAddress().ToString(),
            To = to,
            ReplyTo = request.Email.NullEmpty(),
            Subject = $"[{_settings.BrandName}] Yêu cầu báo giá in 3D #{request.Id} – {request.Name}",
            Body = $"<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.5\">{body}</div>",
            CreatedOnUtc = DateTime.UtcNow,
            EmailAccountId = emailAccount.Id,
            Priority = 5
        });
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? "model");
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());

        return cleaned.Truncate(120).NullEmpty() ?? "model";
    }
}
