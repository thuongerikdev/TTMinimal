using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Content.Menus;
using Smartstore.Core.Data;
using Smartstore.Core.DataExchange.Csv;
using Smartstore.Core.DataExchange.Import;
using Smartstore.Core.Security;
using Smartstore.Web.Controllers;
using Smartstore.Web.Models.DataGrid;

namespace Smartstore.Split3D.Controllers;

public class Split3DController : AdminController
{
    private readonly SmartDbContext _db;
    private readonly Split3DLicenseService _licenseService;
    private readonly Split3DSettings _settings;
    private readonly IMenuService _menuService;

    public Split3DController(SmartDbContext db, Split3DLicenseService licenseService, Split3DSettings settings, IMenuService menuService)
    {
        _menuService = menuService;
        _db = db;
        _licenseService = licenseService;
        _settings = settings;
    }

    #region Configuration

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Configure()
    {
        var model = new ConfigurationModel
        {
            AutoIssueEnabled = _settings.AutoIssueEnabled,
            SendEmail = _settings.SendEmail,
            ProductCode = _settings.ProductCode,
            PublicKeyJson = _settings.PublicKeyJson,
            HasPrivateKey = _settings.PrivateKeyJson.HasValue(),
            OneYearProductId = _settings.OneYearProductId,
            SixMonthsProductId = _settings.SixMonthsProductId,
            ThreeMonthsProductId = _settings.ThreeMonthsProductId,
            LifetimeProductId = _settings.LifetimeProductId,
            DefaultMaxDevices = _settings.DefaultMaxDevices,
            LeaseDays = _settings.LeaseDays,
            CustomerDeactivationLimit = _settings.CustomerDeactivationLimit,
            EmailSubject = _settings.EmailSubject,
            EmailBody = _settings.EmailBody,
            SimplifyAdminMenu = _settings.SimplifyAdminMenu,
            HiddenAdminMenuItems = _settings.HiddenAdminMenuItems,
            BankName = _settings.BankName,
            BankAccountNumber = _settings.BankAccountNumber,
            BankAccountHolder = _settings.BankAccountHolder
        };

        (model.KeyValid, model.KeyStatus) = GetKeyStatus();
        await PrepareProductsAsync();

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        var privateKeyJson = model.PrivateKeyJson.HasValue() ? model.PrivateKeyJson.Trim() : _settings.PrivateKeyJson;

        try
        {
            if (model.PublicKeyJson.HasValue() || privateKeyJson.HasValue())
            {
                Split3DKeySigner.Create(model.PublicKeyJson, privateKeyJson);
            }
        }
        catch (Split3DKeyException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        if (!ModelState.IsValid)
        {
            model.HasPrivateKey = _settings.PrivateKeyJson.HasValue();
            model.PrivateKeyJson = null;
            (model.KeyValid, model.KeyStatus) = GetKeyStatus();
            await PrepareProductsAsync();

            return View(model);
        }

        _settings.AutoIssueEnabled = model.AutoIssueEnabled;
        _settings.SendEmail = model.SendEmail;
        _settings.DefaultMaxDevices = model.DefaultMaxDevices;
        _settings.LeaseDays = model.LeaseDays;
        _settings.CustomerDeactivationLimit = model.CustomerDeactivationLimit;
        _settings.PublicKeyJson = model.PublicKeyJson?.Trim();
        _settings.PrivateKeyJson = privateKeyJson;
        _settings.EmailSubject = model.EmailSubject?.Trim();
        _settings.EmailBody = model.EmailBody;
        _settings.SimplifyAdminMenu = model.SimplifyAdminMenu;
        _settings.HiddenAdminMenuItems = model.HiddenAdminMenuItems.NullEmpty() ?? Split3DSettings.DefaultHiddenAdminMenuItems;
        _settings.BankName = model.BankName?.Trim();
        _settings.BankAccountNumber = model.BankAccountNumber?.Trim();
        _settings.BankAccountHolder = model.BankAccountHolder?.Trim();

        await Services.SettingFactory.SaveSettingsAsync(_settings);
        await _menuService.ClearCacheAsync("Admin");

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Configure));
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> SetupStorefront([FromServices] Split3DStorefrontSetup setup)
    {
        var file = Request.Form.Files["addonfile"];
        if (file != null && file.Length > 0 && !file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            NotifyError(T("Plugins.Split3D.Setup.ZipRequired"));
            return RedirectToAction(nameof(Configure));
        }

        try
        {
            List<string> log;
            if (file != null && file.Length > 0)
            {
                await using var stream = file.OpenReadStream();
                log = await setup.RunAsync(stream, Path.GetFileName(file.FileName), HttpContext.RequestAborted);
            }
            else
            {
                log = await setup.RunAsync(null, null, HttpContext.RequestAborted);
            }

            await Services.Cache.ClearAsync();

            foreach (var line in log)
            {
                if (line.StartsWith("WARNING", StringComparison.Ordinal))
                {
                    NotifyWarning(line);
                }
            }

            NotifySuccess(T("Plugins.Split3D.Setup.Done") + "<br>" + string.Join("<br>", log.Where(x => !x.StartsWith("WARNING", StringComparison.Ordinal)).Select(System.Net.WebUtility.HtmlEncode)));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Split3D storefront setup failed.");
            NotifyError(ex.Message);
        }

        return RedirectToAction(nameof(Configure));
    }

    private async Task PrepareProductsAsync()
    {
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Sku })
            .ToListAsync();

        var items = products
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(CultureInfo.InvariantCulture),
                Text = x.Sku.HasValue() ? $"{x.Name} ({x.Sku})" : x.Name
            })
            .ToList();

        items.Insert(0, new SelectListItem { Value = "0", Text = T("Plugins.Split3D.NoProduct") });

        ViewBag.AvailableProducts = items;
    }

    private (bool Valid, string Status) GetKeyStatus()
    {
        try
        {
            var signer = _licenseService.CreateSigner(false);
            return signer.CanSign
                ? (true, T("Plugins.Split3D.KeyStatus.Valid"))
                : (false, T("Plugins.Split3D.KeyStatus.NoPrivateKey"));
        }
        catch (Split3DKeyException ex)
        {
            return (false, ex.Message);
        }
    }

    #endregion

    #region Licenses

    public IActionResult Index()
        => RedirectToAction(nameof(List));

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> List([FromServices] Split3DOrderQuery orderQuery)
    {
        var now = DateTime.UtcNow;
        var query = _db.Split3DLicenses().AsNoTracking();

        var model = new LicenseListModel
        {
            TotalCount = await query.CountAsync(),
            CustomerCount = await query.Select(x => x.Email).Distinct().CountAsync(),
            TotalRevenue = Split3DLicenseService.FormatPrice(await query.SumAsync(x => (decimal?)x.Price) ?? 0),
            ActiveCount = await query.CountAsync(x => x.ExpiresOnUtc == null || x.ExpiresOnUtc > now)
        };

        (model.KeyValid, model.KeyStatus) = GetKeyStatus();
        model.AutoIssueEnabled = _settings.AutoIssueEnabled;

        var pending = await orderQuery.GetOrdersAsync(null, true, 100, HttpContext.RequestAborted);
        var customerIds = pending.Select(x => x.Order.CustomerId).Distinct().ToArray();
        var customers = await _db.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email ?? x.Username ?? x.Id.ToString(CultureInfo.InvariantCulture));

        model.PendingOrders = pending.Select(x => new PendingOrderModel
        {
            OrderId = x.Order.Id,
            OrderNumber = x.Order.GetOrderNumber(),
            CreatedOn = Services.DateTimeHelper.ConvertToUserTime(x.Order.CreatedOnUtc, DateTimeKind.Utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            Customer = customers.GetValueOrDefault(x.Order.CustomerId),
            Items = string.Join(", ", x.Items.Select(i => $"{i.Quantity} × {i.Product?.Name}")),
            OrderTotal = Split3DLicenseService.FormatPrice(x.Order.OrderTotal),
            State = x.State,
            IssuedKeys = x.Licenses.Count,
            ExpectedKeys = x.ExpectedKeys,
            EditUrl = Url.Action("Edit", "Order", new { id = x.Order.Id, area = "Admin" })
        })
        .ToList();

        ViewBag.AvailableKeyTypes = Split3DPlans.All.Select(x => new SelectListItem { Value = x, Text = x }).ToList();
        ViewBag.AvailableAddons = await _db.Split3DAddons().AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
            .ToListAsync();

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> LicenseList(GridCommand command, LicenseListModel model, [FromServices] Split3DDeviceService deviceService)
    {
        var licenses = await ApplySearch(_db.Split3DLicenses().AsNoTracking(), model)
            .OrderByDescending(x => x.IssuedOnUtc)
            .ApplyGridCommand(command)
            .ToPagedList(command)
            .LoadAsync();

        var now = DateTime.UtcNow;
        var addonNames = await GetAddonNamesAsync();
        var licenseIds = licenses.Select(x => x.Id).ToArray();
        var deviceCounts = await _db.Split3DDevices()
            .Where(x => licenseIds.Contains(x.Split3DLicenseId) && x.DeactivatedOnUtc == null)
            .GroupBy(x => x.Split3DLicenseId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var rows = licenses.Select(x =>
        {
            var row = ToModel(x, now, addonNames);
            row.ActiveDevices = deviceCounts.GetValueOrDefault(x.Id);
            row.MaxDevices = deviceService.GetMaxDevices(x);
            row.DevicesUrl = Url.Action("List", "Split3DDevice", new { licenseId = x.Id, area = "Admin" });
            return row;
        })
        .ToList();

        return Json(new GridModel<LicenseModel>
        {
            Rows = rows,
            Total = licenses.TotalCount
        });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Issue(IssueLicenseModel model)
    {
        try
        {
            var addon = await _db.Split3DAddons().FindByIdAsync(model.AddonId, false);
            var license = _licenseService.Issue(new Split3DIssueRequest
            {
                Addon = addon,
                Email = model.Email,
                CustomerName = model.CustomerName,
                Phone = model.Phone,
                KeyType = model.KeyType,
                Days = model.Days,
                Price = model.Price,
                PurchaseDate = model.PurchaseDate,
                Notes = model.Notes
            });

            var emailQueued = model.SendEmailNow && _licenseService.QueueEmail(license);

            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                token = license.Token,
                message = emailQueued
                    ? T("Plugins.Split3D.Issued.EmailQueued", license.Email).Value
                    : T("Plugins.Split3D.Issued").Value
            });
        }
        catch (Exception ex) when (ex is ArgumentException or Split3DKeyException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Confirms the bank transfer (marks the order as paid if needed) and issues the keys of the order.
    /// </summary>
    [HttpPost]
    [Permission(Permissions.Order.Update)]
    public async Task<IActionResult> ProcessOrder(int orderId, [FromServices] IOrderProcessingService orderProcessingService)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId && !x.Deleted);
        if (order == null)
        {
            return Json(new { success = false, message = T("Plugins.Split3D.Pending.NotFound").Value });
        }

        try
        {
            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                if (!order.CanMarkOrderAsPaid())
                {
                    return Json(new { success = false, message = T("Plugins.Split3D.Pending.CannotMarkPaid").Value });
                }

                await orderProcessingService.MarkOrderAsPaidAsync(order);
            }

            var issued = await _licenseService.IssueForOrderAsync(order, HttpContext.RequestAborted);

            return Json(new { success = true, message = T("Plugins.Split3D.Pending.Processed", order.GetOrderNumber(), issued.Count).Value });
        }
        catch (Exception ex) when (ex is ArgumentException or Split3DKeyException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> ResendEmail(int id)
    {
        var license = await _db.Split3DLicenses().FindByIdAsync(id);
        if (license == null)
        {
            return NotFound();
        }

        if (!_licenseService.QueueEmail(license))
        {
            return Json(new { success = false, message = T("Plugins.Split3D.NoEmailAccount").Value });
        }

        await _db.SaveChangesAsync();

        return Json(new { success = true, message = T("Plugins.Split3D.Issued.EmailQueued", license.Email).Value });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> LicenseDelete(GridSelection selection)
    {
        var ids = selection.GetEntityIds();
        var numDeleted = 0;

        if (ids.Any())
        {
            var licenses = await _db.Split3DLicenses().GetManyAsync(ids, true);
            var licenseIds = licenses.Select(x => x.Id).ToArray();
            var devices = await _db.Split3DDevices().Where(x => licenseIds.Contains(x.Split3DLicenseId)).ToListAsync();

            _db.Split3DDevices().RemoveRange(devices);
            _db.Split3DLicenses().RemoveRange(licenses);
            await _db.SaveChangesAsync();
            numDeleted = licenses.Count;
        }

        return Json(new { Success = true, Count = numDeleted });
    }

    [Permission(Permissions.Configuration.Module.Read)]
    public async Task<IActionResult> Export(LicenseListModel model)
    {
        var licenses = await ApplySearch(_db.Split3DLicenses().AsNoTracking(), model)
            .OrderByDescending(x => x.IssuedOnUtc)
            .ToListAsync();

        var now = DateTime.UtcNow;
        string[] headers =
        [
            "Addon", "Email", "Buyer", "KeyType", "PurchaseDate", "Price", "IssuedOn", "ExpiresOn", "State",
            "Days", "LicenseId", "OrderId", "Phone", "Notes", "Key"
        ];

        var exportAddonNames = await GetAddonNamesAsync();
        var rows = licenses.Select(x => new object[]
        {
            exportAddonNames.GetValueOrDefault(x.AddonId) ?? x.ProductCode ?? string.Empty,
            x.Email,
            x.CustomerName,
            x.KeyType,
            x.PurchaseDateUtc?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
            x.Price,
            x.IssuedOnUtc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC",
            Split3DLicenseService.FormatExpiry(x.ExpiresOnUtc),
            GetState(x, now),
            x.Days?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            x.LicenseId,
            x.OrderId > 0 ? x.OrderId.ToString(CultureInfo.InvariantCulture) : string.Empty,
            x.Phone ?? string.Empty,
            x.Notes ?? string.Empty,
            x.Token
        });

        double[] widths = [22, 30, 24, 12, 13, 14, 20, 20, 12, 8, 28, 10, 16, 30, 60];
        var bytes = SimpleXlsxWriter.Write("Split3D licenses", headers, rows, widths);
        var fileName = $"split3d-licenses-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Module.Update)]
    public async Task<IActionResult> Import()
    {
        var file = Request.Form.Files["importfile"];
        if (file == null || file.Length == 0)
        {
            NotifyError(T("Plugins.Split3D.Import.NoFile"));
            return RedirectToAction(nameof(List));
        }

        try
        {
            IDataTable table;
            await using (var stream = file.OpenReadStream())
            {
                var csvConfig = new CsvConfiguration { Delimiter = DetectDelimiter(file) };
                table = LightweightDataTable.FromFile(file.FileName, stream, file.Length, csvConfig);
            }

            var result = await _licenseService.ImportAsync(table);

            NotifySuccess(T("Plugins.Split3D.Import.Result", result.Imported, result.Skipped));
            foreach (var error in result.Errors.Take(10))
            {
                NotifyWarning(error);
            }
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }

        return RedirectToAction(nameof(List));
    }

    private static char DetectDelimiter(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ',';
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var header = reader.ReadLine() ?? string.Empty;

        return header.Count(c => c == ';') > header.Count(c => c == ',') ? ';' : ',';
    }

    private static IQueryable<Split3DLicense> ApplySearch(IQueryable<Split3DLicense> query, LicenseListModel model)
    {
        if (model.SearchTerm.HasValue())
        {
            var term = model.SearchTerm.Trim();
            query = query.Where(x => x.Email.Contains(term) || x.CustomerName.Contains(term) || x.LicenseId.Contains(term) || x.Phone.Contains(term));
        }

        if (model.SearchAddonId > 0)
        {
            query = query.Where(x => x.AddonId == model.SearchAddonId);
        }

        if (model.SearchKeyType.HasValue())
        {
            query = query.Where(x => x.KeyType == model.SearchKeyType);
        }

        return query;
    }

    private string GetState(Split3DLicense license, DateTime now)
    {
        if (license.Blocked)
        {
            return T("Plugins.Split3D.State.Blocked");
        }

        if (license.ExpiresOnUtc == null)
        {
            return T("Plugins.Split3D.State.Lifetime");
        }

        return license.ExpiresOnUtc <= now
            ? T("Plugins.Split3D.State.Expired")
            : T("Plugins.Split3D.State.Active");
    }

    private Task<Dictionary<int, string>> GetAddonNamesAsync()
        => _db.Split3DAddons().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);

    private LicenseModel ToModel(Split3DLicense x, DateTime now, Dictionary<int, string> addonNames)
    {
        var dtHelper = Services.DateTimeHelper;

        return new LicenseModel
        {
            Id = x.Id,
            LicenseId = x.LicenseId,
            AddonId = x.AddonId,
            AddonName = addonNames.GetValueOrDefault(x.AddonId) ?? x.ProductCode,
            Email = x.Email,
            CustomerName = x.CustomerName,
            Phone = x.Phone,
            KeyType = x.KeyType,
            Price = x.Price,
            PriceString = Split3DLicenseService.FormatPrice(x.Price),
            PurchaseDate = x.PurchaseDateUtc,
            PurchaseDateString = x.PurchaseDateUtc?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            IssuedOn = dtHelper.ConvertToUserTime(x.IssuedOnUtc, DateTimeKind.Utc),
            ExpiresOn = x.ExpiresOnUtc.HasValue ? dtHelper.ConvertToUserTime(x.ExpiresOnUtc.Value, DateTimeKind.Utc) : null,
            ExpiresOnString = x.ExpiresOnUtc.HasValue
                ? dtHelper.ConvertToUserTime(x.ExpiresOnUtc.Value, DateTimeKind.Utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : Split3DPlans.Lifetime,
            State = GetState(x, now),
            IsExpired = x.ExpiresOnUtc.HasValue && x.ExpiresOnUtc <= now,
            Token = x.Token,
            Notes = x.Notes,
            OrderId = x.OrderId,
            OrderUrl = x.OrderId > 0 ? Url.Action("Edit", "Order", new { id = x.OrderId, area = "Admin" }) : null,
            EmailSent = x.EmailSent,
            Blocked = x.Blocked
        };
    }

    #endregion
}
