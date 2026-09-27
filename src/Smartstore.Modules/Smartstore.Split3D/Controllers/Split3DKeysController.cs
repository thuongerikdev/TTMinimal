using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// "My keys" page in the customer account: order status, issued activation keys and the devices they are active on.
/// </summary>
public class Split3DKeysController : PublicController
{
    private readonly SmartDbContext _db;
    private readonly Split3DOrderQuery _orderQuery;
    private readonly Split3DDeviceService _deviceService;
    private readonly Split3DRepoService _repoService;
    private readonly Split3DSettings _settings;

    public Split3DKeysController(
        SmartDbContext db,
        Split3DOrderQuery orderQuery,
        Split3DDeviceService deviceService,
        Split3DRepoService repoService,
        Split3DSettings settings)
    {
        _db = db;
        _orderQuery = orderQuery;
        _deviceService = deviceService;
        _repoService = repoService;
        _settings = settings;
    }

    public async Task<IActionResult> Index()
    {
        var customer = Services.WorkContext.CurrentCustomer;
        if (!customer.IsRegistered())
        {
            return ChallengeOrForbid();
        }

        var now = DateTime.UtcNow;
        var addonNames = await _db.Split3DAddons().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
        var orders = await _orderQuery.GetOrdersAsync(customer.Id, false, 100, HttpContext.RequestAborted);

        // Keys issued manually or imported for the same email address.
        var otherKeys = new List<Split3DLicense>();
        if (customer.Email.HasValue())
        {
            var email = customer.Email.ToLowerInvariant();
            otherKeys = await _db.Split3DLicenses()
                .AsNoTracking()
                .Where(x => x.OrderId == 0 && x.Email == email)
                .OrderByDescending(x => x.IssuedOnUtc)
                .ToListAsync();
        }

        var licenseIds = orders.SelectMany(x => x.Licenses).Concat(otherKeys).Select(x => x.Id).Distinct().ToArray();
        var devices = (await _db.Split3DDevices()
            .AsNoTracking()
            .Where(x => licenseIds.Contains(x.Split3DLicenseId) && x.DeactivatedOnUtc == null)
            .OrderByDescending(x => x.LastSeenOnUtc)
            .ToListAsync())
            .ToLookup(x => x.Split3DLicenseId);

        var model = new MyKeysModel
        {
            BankName = _settings.BankName,
            BankAccountNumber = _settings.BankAccountNumber,
            BankAccountHolder = _settings.BankAccountHolder,
            Orders = orders.Select(x => new MyKeysOrderModel
            {
                OrderId = x.Order.Id,
                OrderNumber = x.Order.GetOrderNumber(),
                CreatedOn = Services.DateTimeHelper.ConvertToUserTime(x.Order.CreatedOnUtc, DateTimeKind.Utc),
                OrderTotal = Split3DLicenseService.FormatPrice(x.Order.OrderTotal),
                State = x.State,
                ExpectedKeys = x.ExpectedKeys,
                Items = x.Items.Select(i => $"{i.Quantity} × {i.Product?.Name}").ToList(),
                Keys = x.Licenses.Select(l => ToModel(l, now, addonNames, devices[l.Id])).ToList()
            })
            .ToList(),
            OtherKeys = otherKeys.Select(x => ToModel(x, now, addonNames, devices[x.Id])).ToList()
        };

        if (_settings.CustomerDeactivationLimit > 0)
        {
            var used = await _deviceService.CountCustomerDeactivationsAsync(licenseIds);
            model.RemainingDeactivations = Math.Max(0, _settings.CustomerDeactivationLimit - used);
        }

        await AddRepositoryLinksAsync(
            orders.SelectMany(x => x.Licenses).Concat(otherKeys),
            model.Orders.SelectMany(x => x.Keys).Concat(model.OtherKeys));

        return View(model);
    }

    /// <summary>
    /// Adds the Blender install/update links to keys whose addon has an extension package.
    /// </summary>
    private async Task AddRepositoryLinksAsync(IEnumerable<Split3DLicense> licenses, IEnumerable<MyKeyModel> keys)
    {
        var valid = licenses.Where(Split3DRepoService.CanUpdate).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        if (valid.Count == 0)
        {
            return;
        }

        var packages = new Dictionary<int, Split3DPackageInfo>();
        foreach (var addonId in valid.Values.Select(x => x.AddonId).Distinct())
        {
            if (await _repoService.GetLatestPackageAsync(addonId, HttpContext.RequestAborted) is { } latest)
            {
                packages[addonId] = latest.Package;
            }
        }

        var needToken = valid.Values.Where(x => packages.ContainsKey(x.AddonId) && x.RepoToken.IsEmpty()).Select(x => x.Id).ToArray();
        if (needToken.Length > 0)
        {
            var tracked = await _db.Split3DLicenses().Where(x => needToken.Contains(x.Id)).ToListAsync();
            foreach (var license in tracked)
            {
                valid[license.Id].RepoToken = Split3DRepoService.EnsureToken(license);
            }

            await _db.SaveChangesAsync();
        }

        foreach (var key in keys)
        {
            if (!valid.TryGetValue(key.Id, out var license) || !packages.TryGetValue(license.AddonId, out var package))
            {
                continue;
            }

            key.PackageVersion = package.Version;
            key.RepoUrl = Split3DRepoService.GetFolderUrl(Request, license.RepoToken) + "index.json";
            key.InstallUrl = Split3DRepoService.GetInstallUrl(Request, license.RepoToken, package);
            key.DownloadUrl = Split3DRepoService.GetFolderUrl(Request, license.RepoToken) + package.FileName;
        }
    }

    /// <summary>
    /// Signs a device out of one of the customer's keys, freeing the slot for another computer.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateDevice(int id)
    {
        var customer = Services.WorkContext.CurrentCustomer;
        if (!customer.IsRegistered())
        {
            return ChallengeOrForbid();
        }

        var ownLicenseIds = await GetOwnLicenseIdsAsync(customer);
        var device = await _db.Split3DDevices().FirstOrDefaultAsync(x => x.Id == id);

        if (device == null || !ownLicenseIds.Contains(device.Split3DLicenseId))
        {
            return NotFound();
        }

        if (device.IsActive)
        {
            if (_settings.CustomerDeactivationLimit > 0
                && await _deviceService.CountCustomerDeactivationsAsync(ownLicenseIds) >= _settings.CustomerDeactivationLimit)
            {
                NotifyError(T("Plugins.Split3D.Device.DeactivationLimitReached", _settings.CustomerDeactivationLimit, Split3DDeviceService.CustomerLimitPeriodDays));
                return RedirectToAction(nameof(Index));
            }

            Split3DDeviceService.Deactivate(device, Split3DDeactivatedBy.Customer);
            await _db.SaveChangesAsync();
        }

        NotifySuccess(T("Plugins.Split3D.Device.Deactivated", device.DeviceName));

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<int>> GetOwnLicenseIdsAsync(Customer customer)
    {
        var email = customer.Email?.ToLowerInvariant();

        return await _db.Split3DLicenses()
            .Where(x => x.CustomerId == customer.Id || (email != null && x.Email == email))
            .Select(x => x.Id)
            .ToListAsync();
    }

    private MyKeyModel ToModel(Split3DLicense license, DateTime now, Dictionary<int, string> addonNames, IEnumerable<Split3DDevice> devices)
    {
        string Format(DateTime utc) => Services.DateTimeHelper
            .ConvertToUserTime(utc, DateTimeKind.Utc)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        var isExpired = license.ExpiresOnUtc.HasValue && license.ExpiresOnUtc <= now;

        return new MyKeyModel
        {
            Id = license.Id,
            AddonName = addonNames.GetValueOrDefault(license.AddonId) ?? license.ProductCode,
            Plan = license.KeyType,
            Token = license.Token,
            IssuedOn = Format(license.IssuedOnUtc),
            ExpiresOn = license.ExpiresOnUtc.HasValue ? Format(license.ExpiresOnUtc.Value) : null,
            IsLifetime = !license.ExpiresOnUtc.HasValue,
            IsExpired = isExpired,
            IsBlocked = license.Blocked,
            Remaining = license.ExpiresOnUtc.HasValue && !isExpired
                ? Split3DDeviceService.FormatDuration(license.ExpiresOnUtc.Value - now, T)
                : null,
            MaxDevices = _deviceService.GetMaxDevices(license),
            Devices = devices.Select(d => new MyDeviceModel
            {
                Id = d.Id,
                DeviceName = d.DeviceName.NullEmpty() ?? d.DeviceId[..8],
                Platform = d.Platform,
                BlenderVersion = d.BlenderVersion,
                ActivatedOn = Format(d.ActivatedOnUtc),
                ActiveFor = Split3DDeviceService.FormatDuration(now - d.ActivatedOnUtc, T),
                LastSeenOn = Format(d.LastSeenOnUtc)
            })
            .ToList()
        };
    }
}
