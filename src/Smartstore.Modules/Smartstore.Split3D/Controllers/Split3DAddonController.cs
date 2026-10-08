using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Admin pages to manage sellable addons, their plan products and addon files.
/// </summary>
public partial class Split3DAddonController : AdminController
{
    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]{1,98}$")]
    private static partial Regex ProductCodeRegex();

    [GeneratedRegex(@"^\d+(\.\d+){1,3}$")]
    private static partial Regex VersionRegex();

    private readonly SmartDbContext _db;
    private readonly Split3DStorefrontSetup _setup;

    public Split3DAddonController(SmartDbContext db, Split3DStorefrontSetup setup)
    {
        _db = db;
        _setup = setup;
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> List()
    {
        var addons = await _db.Split3DAddons().AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync();
        var productCounts = await _db.Split3DAddonProducts().GroupBy(x => x.AddonId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var licenseCounts = await _db.Split3DLicenses().GroupBy(x => x.AddonId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        var model = addons.Select(x => new AddonModel
        {
            Id = x.Id,
            Name = x.Name,
            ProductCode = x.ProductCode,
            Version = x.Version,
            Active = x.Active,
            DisplayOrder = x.DisplayOrder,
            ComingSoon = x.ComingSoon,
            ProductCount = productCounts.GetValueOrDefault(x.Id),
            LicenseCount = licenseCounts.GetValueOrDefault(x.Id)
        })
        .ToList();

        return View(model);
    }

    [Permission(Permissions.Configuration.Module.Update)]
    public IActionResult Create()
        => View("Edit", new AddonModel { Version = "1.0.0" });

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Edit(int id)
    {
        var addon = await _db.Split3DAddons().FindByIdAsync(id, false);
        if (addon == null)
        {
            return NotFound();
        }

        var model = new AddonModel
        {
            Id = addon.Id,
            Name = addon.Name,
            ProductCode = addon.ProductCode,
            Version = addon.Version,
            Description = addon.Description,
            Active = addon.Active,
            ManagedLicensing = addon.ManagedLicensing,
            DisplayOrder = addon.DisplayOrder,
            ComingSoon = addon.ComingSoon,
            Kind = addon.Kind,
            Icon = addon.Icon,
            PictureId = addon.MediaFileId
        };

        await PrepareEditAsync(model);
        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Save(AddonModel model)
    {
        model.Name = model.Name?.Trim();
        model.ProductCode = model.ProductCode?.Trim().ToLowerInvariant();
        model.Version = model.Version?.Trim();
        model.Kind = model.Kind?.Trim();

        if (model.Name.IsEmpty())
        {
            ModelState.AddModelError(nameof(model.Name), T("Plugins.Split3D.Addon.NameRequired"));
        }
        if (model.ProductCode.IsEmpty() || !ProductCodeRegex().IsMatch(model.ProductCode))
        {
            ModelState.AddModelError(nameof(model.ProductCode), T("Plugins.Split3D.Addon.ProductCodeInvalid"));
        }
        else if (await _db.Split3DAddons().AnyAsync(x => x.ProductCode == model.ProductCode && x.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.ProductCode), T("Plugins.Split3D.Addon.ProductCodeExists"));
        }
        if (model.Version.HasValue() && !VersionRegex().IsMatch(model.Version))
        {
            ModelState.AddModelError(nameof(model.Version), T("Plugins.Split3D.Addon.VersionInvalid"));
        }

        var addon = model.Id > 0 ? await _db.Split3DAddons().FindByIdAsync(model.Id) : null;
        if (model.Id > 0 && addon == null)
        {
            return NotFound();
        }

        // Changing the product code would invalidate every key already issued for the addon.
        if (addon != null && !addon.ProductCode.EqualsNoCase(model.ProductCode)
            && await _db.Split3DLicenses().AnyAsync(x => x.AddonId == addon.Id))
        {
            ModelState.AddModelError(nameof(model.ProductCode), T("Plugins.Split3D.Addon.ProductCodeLocked"));
        }

        if (!ModelState.IsValid)
        {
            await PrepareEditAsync(model);
            return View("Edit", model);
        }

        if (addon == null)
        {
            addon = new Split3DAddon();
            _db.Split3DAddons().Add(addon);
        }

        addon.Name = model.Name;
        addon.ProductCode = model.ProductCode;
        addon.Version = model.Version;
        addon.Description = model.Description;
        addon.Active = model.Active;
        addon.ManagedLicensing = model.ManagedLicensing;
        addon.DisplayOrder = model.DisplayOrder;
        addon.ComingSoon = model.ComingSoon;
        addon.Kind = model.Kind.NullEmpty();
        addon.Icon = model.Icon.NullEmpty();
        addon.MediaFileId = model.PictureId.GetValueOrDefault() > 0 ? model.PictureId : null;

        await _db.SaveChangesAsync();
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Edit), new { id = addon.Id });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Delete(int id)
    {
        var addon = await _db.Split3DAddons().FindByIdAsync(id);
        if (addon == null)
        {
            return NotFound();
        }

        // Issued keys carry the addon id; deleting the addon would leave them unattributable.
        if (await _db.Split3DLicenses().AnyAsync(x => x.AddonId == addon.Id))
        {
            NotifyError(T("Plugins.Split3D.Addon.DeleteLocked"));
            return RedirectToAction(nameof(Edit), new { id = addon.Id });
        }

        var mappings = await _db.Split3DAddonProducts().Where(x => x.AddonId == addon.Id).ToListAsync();
        var productIds = mappings.Select(x => x.ProductId).Distinct().ToArray();

        // The plan products can no longer grant a key, so take them off the storefront
        // instead of selling something that cannot be delivered.
        var products = await _db.Products.Where(x => productIds.Contains(x.Id) && x.Published).ToListAsync();
        products.Each(x => x.Published = false);

        _db.Split3DAddonProducts().RemoveRange(mappings);
        _db.Split3DAddons().Remove(addon);
        await _db.SaveChangesAsync();

        await _setup.RefreshContentAsync();

        NotifySuccess(T("Plugins.Split3D.Addon.Deleted", addon.Name, products.Count));

        return RedirectToAction(nameof(List));
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> CreatePlanProducts(CreatePlanProductsModel model)
    {
        var addon = await _db.Split3DAddons().FindByIdAsync(model.AddonId);
        if (addon == null)
        {
            return NotFound();
        }

        var selected = new List<(string Plan, string Suffix, string Label, decimal Price)>();
        if (model.ThreeMonths) selected.Add((Split3DPlans.ThreeMonths, "3M", "Gói 3 tháng", model.ThreeMonthsPrice));
        if (model.SixMonths) selected.Add((Split3DPlans.SixMonths, "6M", "Gói 6 tháng", model.SixMonthsPrice));
        if (model.OneYear) selected.Add((Split3DPlans.OneYear, "1Y", "Gói 1 năm", model.OneYearPrice));
        if (model.Lifetime) selected.Add((Split3DPlans.Lifetime, "LT", "Vĩnh viễn", model.LifetimePrice));

        if (selected.Count == 0)
        {
            NotifyWarning(T("Plugins.Split3D.Addon.NoPlanSelected"));
            return RedirectToAction(nameof(Edit), new { id = addon.Id });
        }

        try
        {
            var (zipBytes, zipName) = await ReadUploadAsync(Request.Form.Files["addonfile"], ".zip");
            var (imageBytes, imageName) = await ReadUploadAsync(Request.Form.Files["imagefile"], ".png", ".jpg", ".jpeg", ".webp");

            var skuPrefix = Regex.Replace(addon.ProductCode.ToUpperInvariant(), "[^A-Z0-9]+", "-").Trim('-');

            // Packages for several devices get their own products: "…-LT-5PC", "Gói vĩnh viễn · 5 máy".
            var devices = model.MaxDevices is > 1 and <= 100 ? model.MaxDevices : null;
            var deviceSku = devices.HasValue ? $"-{devices}PC" : string.Empty;
            var deviceLabel = devices.HasValue ? $" · {devices} máy" : string.Empty;

            var specs = selected.Select(x => new Split3DPlanProductSpec
            {
                Plan = x.Plan,
                MaxDevices = devices,
                Sku = $"{skuPrefix}-{x.Suffix}{deviceSku}",
                Name = $"{addon.Name} – {x.Label}{deviceLabel}",
                Slug = null,
                Price = Math.Max(0, decimal.Round(x.Price, 0)),
                ShortDescription = FirstParagraph(addon.Description) ?? addon.Name,
                FullDescription = addon.Description,
                ImageBytes = imageBytes,
                ImageFileName = imageName == null ? null : $"{skuPrefix.ToLowerInvariant()}-{x.Suffix.ToLowerInvariant()}{Path.GetExtension(imageName)}",
                ShowOnHomePage = true
            });

            var category = await _setup.GetAddonCategoryAsync();
            var log = new List<string>();

            if (zipBytes != null)
            {
                (zipBytes, var package) = await _setup.PrepareAddonFileAsync(addon, zipBytes, log);
                if (package != null)
                {
                    addon.Version = package.Version;
                    zipName = package.FileName;
                    await _db.SaveChangesAsync();
                }
            }

            await _setup.CreatePlanProductsAsync(addon, specs, category, zipBytes, zipName, log);
            await _setup.RefreshContentAsync();

            NotifySuccess(string.Join("<br>", log.Select(System.Net.WebUtility.HtmlEncode)));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            NotifyError(ex.Message);
        }

        return RedirectToAction(nameof(Edit), new { id = addon.Id });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> UploadVersion(int addonId, string version)
    {
        var addon = await _db.Split3DAddons().FindByIdAsync(addonId);
        if (addon == null)
        {
            return NotFound();
        }

        version = version?.Trim().NullEmpty();
        if (version != null && !VersionRegex().IsMatch(version))
        {
            NotifyError(T("Plugins.Split3D.Addon.VersionInvalid"));
            return RedirectToAction(nameof(Edit), new { id = addon.Id });
        }

        try
        {
            var (zipBytes, zipName) = await ReadUploadAsync(Request.Form.Files["addonfile"], ".zip");
            if (zipBytes == null)
            {
                NotifyError(T("Plugins.Split3D.Setup.ZipRequired"));
                return RedirectToAction(nameof(Edit), new { id = addon.Id });
            }

            var log = new List<string>();
            (zipBytes, var package) = await _setup.PrepareAddonFileAsync(addon, zipBytes, log);

            // An extension package carries its own version; a different typed version would mislabel the download.
            if (package != null && version != null && version != package.Version)
            {
                NotifyError(T("Plugins.Split3D.Addon.VersionMismatch", version, package.Version));
                return RedirectToAction(nameof(Edit), new { id = addon.Id });
            }

            version = package?.Version ?? version;
            if (version == null)
            {
                NotifyError(T("Plugins.Split3D.Addon.VersionInvalid"));
                return RedirectToAction(nameof(Edit), new { id = addon.Id });
            }

            addon.Version = version;
            await _db.SaveChangesAsync();

            var count = await _setup.AttachAddonFileAsync(addon, zipBytes, package?.FileName ?? zipName);

            // The home page shows the addon version.
            await _setup.RefreshContentAsync();
            NotifySuccess(T("Plugins.Split3D.Addon.VersionUploaded", version, count) + "<br>"
                + string.Join("<br>", log.Select(System.Net.WebUtility.HtmlEncode)));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            NotifyError(ex.Message);
        }

        return RedirectToAction(nameof(Edit), new { id = addon.Id });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> MapProduct(MapProductModel model)
    {
        var addon = await _db.Split3DAddons().FindByIdAsync(model.AddonId, false);
        if (addon == null)
        {
            return NotFound();
        }

        if (model.ProductId <= 0 || !Split3DPlans.All.Contains(model.KeyType)
            || (model.KeyType == Split3DPlans.Custom && model.Days is null or < 1 or > Split3DPlans.MaxDays))
        {
            NotifyError(T("Plugins.Split3D.Addon.MappingInvalid"));
            return RedirectToAction(nameof(Edit), new { id = addon.Id });
        }

        var mapping = await _db.Split3DAddonProducts().FirstOrDefaultAsync(x => x.ProductId == model.ProductId);
        if (mapping == null)
        {
            mapping = new Split3DAddonProduct { ProductId = model.ProductId };
            _db.Split3DAddonProducts().Add(mapping);
        }

        mapping.AddonId = addon.Id;
        mapping.KeyType = model.KeyType;
        mapping.Days = model.KeyType == Split3DPlans.Custom ? model.Days : null;
        mapping.MaxDevices = model.MaxDevices is > 0 and <= 100 ? model.MaxDevices : null;
        await _db.SaveChangesAsync();

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Edit), new { id = addon.Id });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> UnmapProduct(int id)
    {
        var mapping = await _db.Split3DAddonProducts().FindByIdAsync(id);
        if (mapping == null)
        {
            return NotFound();
        }

        var addonId = mapping.AddonId;
        _db.Split3DAddonProducts().Remove(mapping);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id = addonId });
    }

    private async Task PrepareEditAsync(AddonModel model)
    {
        if (model.Id > 0)
        {
            var mappings = await _db.Split3DAddonProducts().AsNoTracking().Where(x => x.AddonId == model.Id).ToListAsync();
            var productIds = mappings.Select(x => x.ProductId).ToArray();
            var products = await _db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            var downloads = await _db.Downloads.AsNoTracking()
                .Where(x => x.EntityName == nameof(Product) && productIds.Contains(x.EntityId))
                .Select(x => new { x.EntityId, x.FileVersion })
                .ToListAsync();

            model.Products = mappings
                .Select(m =>
                {
                    var p = products.GetValueOrDefault(m.ProductId);
                    return new AddonProductModel
                    {
                        MappingId = m.Id,
                        ProductId = m.ProductId,
                        ProductName = p?.Name ?? $"#{m.ProductId} (deleted)",
                        Sku = p?.Sku,
                        Price = p != null ? Split3DLicenseService.FormatPrice(p.Price) : null,
                        Published = p?.Published == true && !p.Deleted,
                        KeyType = m.KeyType,
                        Days = m.Days,
                        MaxDevices = m.MaxDevices,
                        DownloadVersions = string.Join(", ", downloads.Where(d => d.EntityId == m.ProductId).Select(d => d.FileVersion).Where(v => v.HasValue())),
                        EditUrl = Url.Action("Edit", "Product", new { id = m.ProductId, area = "Admin" })
                    };
                })
                .OrderBy(x => x.ProductName)
                .ToList();

            model.LicenseCount = await _db.Split3DLicenses().CountAsync(x => x.AddonId == model.Id);
        }

        var mappedIds = await _db.Split3DAddonProducts().Select(x => x.ProductId).ToListAsync();
        ViewBag.AvailableProducts = await _db.Products.AsNoTracking()
            .Where(x => !x.Deleted && !mappedIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name + (x.Sku != null ? " (" + x.Sku + ")" : "") })
            .ToListAsync();
        ViewBag.AvailableKeyTypes = Split3DPlans.All.Select(x => new SelectListItem { Value = x, Text = x }).ToList();
    }

    private static async Task<(byte[] Bytes, string FileName)> ReadUploadAsync(IFormFile file, params string[] extensions)
    {
        if (file == null || file.Length == 0)
        {
            return (null, null);
        }

        var ext = Path.GetExtension(file.FileName);
        if (!extensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"'{file.FileName}': allowed file types are {string.Join(", ", extensions)}.");
        }

        await using var stream = file.OpenReadStream();
        return (await Split3DStorefrontSetup.ReadAllBytesAsync(stream, default), Path.GetFileName(file.FileName));
    }

    private static string FirstParagraph(string html)
    {
        if (html.IsEmpty())
        {
            return null;
        }

        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(Regex.Replace(text, @"\s+", " ")).Trim();
        return text.Truncate(400, "…").NullEmpty();
    }
}
