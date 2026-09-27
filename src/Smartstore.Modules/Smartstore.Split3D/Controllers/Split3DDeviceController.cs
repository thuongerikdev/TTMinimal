using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Web.Controllers;
using Smartstore.Web.Models.DataGrid;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Admin pages for devices keys are activated on: which computer uses which key, since when,
/// signing devices out, and per-key device limit / block.
/// </summary>
public class Split3DDeviceController : AdminController
{
    private readonly SmartDbContext _db;
    private readonly Split3DDeviceService _deviceService;
    private readonly Split3DSettings _settings;

    public Split3DDeviceController(SmartDbContext db, Split3DDeviceService deviceService, Split3DSettings settings)
    {
        _db = db;
        _deviceService = deviceService;
        _settings = settings;
    }

    public IActionResult Index()
        => RedirectToAction(nameof(List));

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> List(int? licenseId)
    {
        var devices = _db.Split3DDevices().AsNoTracking();
        var model = new DeviceListModel
        {
            LicenseId = licenseId,
            ActiveCount = await devices.CountAsync(x => x.DeactivatedOnUtc == null),
            TodayCount = await devices.CountAsync(x => x.DeactivatedOnUtc == null && x.LastSeenOnUtc > DateTime.UtcNow.AddDays(-1))
        };

        if (licenseId > 0)
        {
            var license = await _db.Split3DLicenses().FindByIdAsync(licenseId.Value, false);
            if (license == null)
            {
                return NotFound();
            }

            var addonName = await _db.Split3DAddons().Where(x => x.Id == license.AddonId).Select(x => x.Name).FirstOrDefaultAsync();

            model.SearchActiveOnly = false;
            model.License = new LicenseDevicesModel
            {
                Id = license.Id,
                Email = license.Email,
                CustomerName = license.CustomerName,
                AddonName = addonName ?? license.ProductCode,
                KeyType = license.KeyType,
                ExpiresOn = license.ExpiresOnUtc.HasValue
                    ? Services.DateTimeHelper.ConvertToUserTime(license.ExpiresOnUtc.Value, DateTimeKind.Utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                    : Split3DPlans.Lifetime,
                IsExpired = license.ExpiresOnUtc.HasValue && license.ExpiresOnUtc <= DateTime.UtcNow,
                ActiveDevices = await devices.CountAsync(x => x.Split3DLicenseId == license.Id && x.DeactivatedOnUtc == null),
                DefaultMaxDevices = _settings.DefaultMaxDevices,
                MaxDevices = license.MaxDevices,
                Blocked = license.Blocked
            };
        }

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> DeviceList(GridCommand command, DeviceListModel model)
    {
        var query =
            from d in _db.Split3DDevices().AsNoTracking()
            join l in _db.Split3DLicenses().AsNoTracking() on d.Split3DLicenseId equals l.Id
            select new { Device = d, License = l };

        if (model.LicenseId > 0)
        {
            query = query.Where(x => x.Device.Split3DLicenseId == model.LicenseId.Value);
        }

        if (model.SearchActiveOnly)
        {
            query = query.Where(x => x.Device.DeactivatedOnUtc == null);
        }

        if (model.SearchTerm.HasValue())
        {
            var term = model.SearchTerm.Trim();
            query = query.Where(x =>
                x.Device.DeviceName.Contains(term)
                || x.Device.DeviceId.StartsWith(term)
                || x.Device.LastIpAddress.Contains(term)
                || x.License.Email.Contains(term)
                || x.License.CustomerName.Contains(term)
                || x.License.LicenseId.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(x => x.Device.DeactivatedOnUtc == null)
            .ThenByDescending(x => x.Device.LastSeenOnUtc)
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var addonNames = await _db.Split3DAddons().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
        var dtHelper = Services.DateTimeHelper;

        return Json(new GridModel<DeviceModel>
        {
            Total = total,
            Rows = rows.Select(x => new DeviceModel
            {
                Id = x.Device.Id,
                Split3DLicenseId = x.License.Id,
                DeviceName = x.Device.DeviceName.NullEmpty() ?? x.Device.DeviceId[..8],
                Platform = x.Device.Platform,
                BlenderVersion = x.Device.BlenderVersion,
                AddonVersion = x.Device.AddonVersion,
                LastIpAddress = x.Device.LastIpAddress,
                Email = x.License.Email,
                AddonName = addonNames.GetValueOrDefault(x.License.AddonId) ?? x.License.ProductCode,
                FirstActivatedOn = dtHelper.ConvertToUserTime(x.Device.FirstActivatedOnUtc, DateTimeKind.Utc),
                ActivatedOn = dtHelper.ConvertToUserTime(x.Device.ActivatedOnUtc, DateTimeKind.Utc),
                ActiveFor = x.Device.IsActive
                    ? Split3DDeviceService.FormatDuration(now - x.Device.ActivatedOnUtc, T)
                    : Split3DDeviceService.FormatDuration(x.Device.DeactivatedOnUtc.Value - x.Device.ActivatedOnUtc, T),
                LastSeenOn = dtHelper.ConvertToUserTime(x.Device.LastSeenOnUtc, DateTimeKind.Utc),
                IsActive = x.Device.IsActive,
                Status = x.Device.IsActive
                    ? T("Plugins.Split3D.Device.Status.Active")
                    : T("Plugins.Split3D.Device.Status.Deactivated",
                        T("Plugins.Split3D.Device.By." + x.Device.DeactivatedBy).Value,
                        dtHelper.ConvertToUserTime(x.Device.DeactivatedOnUtc.Value, DateTimeKind.Utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
                DevicesUrl = Url.Action(nameof(List), new { licenseId = x.License.Id })
            })
            .ToList()
        });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> DeactivateSelected(string selectedIds)
    {
        var ids = selectedIds.ToIntArray();
        var devices = await _db.Split3DDevices()
            .Where(x => ids.Contains(x.Id) && x.DeactivatedOnUtc == null)
            .ToListAsync();

        foreach (var device in devices)
        {
            Split3DDeviceService.Deactivate(device, Split3DDeactivatedBy.Admin);
        }

        await _db.SaveChangesAsync();

        return Json(new { success = true, message = T("Plugins.Split3D.Device.DeactivatedCount", devices.Count).Value });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> DeviceDelete(GridSelection selection)
    {
        var ids = selection.GetEntityIds();
        var numDeleted = 0;

        if (ids.Any())
        {
            var devices = await _db.Split3DDevices().GetManyAsync(ids, true);
            _db.Split3DDevices().RemoveRange(devices);
            numDeleted = await _db.SaveChangesAsync();
        }

        return Json(new { Success = true, Count = numDeleted });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> UpdateLicense(LicenseDevicesModel model)
    {
        var license = await _db.Split3DLicenses().FindByIdAsync(model.Id);
        if (license == null)
        {
            return NotFound();
        }

        if (model.MaxDevices is < 1 or > 100)
        {
            NotifyError(T("Plugins.Split3D.License.MaxDevicesInvalid"));
            return RedirectToAction(nameof(List), new { licenseId = license.Id });
        }

        license.MaxDevices = model.MaxDevices;
        license.Blocked = model.Blocked;
        await _db.SaveChangesAsync();

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(List), new { licenseId = license.Id });
    }
}
