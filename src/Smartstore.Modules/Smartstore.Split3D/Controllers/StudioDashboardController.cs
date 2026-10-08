#nullable enable

using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// The studio "workbench": one calm page with what needs doing today, how the shop is doing and what the
/// print room is working on. Replaces the core widget dashboard as the admin start page
/// (<see cref="Filters.DashboardRedirectFilter"/>); the core dashboard stays reachable via <c>/admin?full=1</c>.
/// </summary>
public class StudioDashboardController : AdminController
{
    // Days of revenue history sent to the page (chart ranges and the activity heatmap).
    private const int HistoryDays = 371;
    private const int OnlineMinutes = 15;
    private const int ExpiringKeyDays = 14;

    private static readonly CultureInfo _vi = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly int[] _openJobStatuses =
    [
        (int)PrintOrderStatus.Paid,
        (int)PrintOrderStatus.Confirmed,
        (int)PrintOrderStatus.Printing,
        (int)PrintOrderStatus.Ready
    ];

    private readonly SmartDbContext _db;
    private readonly IDateTimeHelper _dateTimeHelper;

    public StudioDashboardController(SmartDbContext db, IDateTimeHelper dateTimeHelper)
    {
        _db = db;
        _dateTimeHelper = dateTimeHelper;
    }

    [Permission(Permissions.Order.Read)]
    public async Task<IActionResult> Index(
        [FromServices] Split3DOrderQuery orderQuery,
        [FromServices] PrintQuoteFollowUpService followUpService,
        [FromServices] Split3DLicenseService licenseService)
    {
        var cancelToken = HttpContext.RequestAborted;
        var tz = _dateTimeHelper.CurrentTimeZone;
        var nowUtc = DateTime.UtcNow;
        var now = _dateTimeHelper.ConvertToUserTime(nowUtc, DateTimeKind.Utc);
        var today = now.Date;
        var firstDay = today.AddDays(1 - HistoryDays);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);

        DateTime ToUtc(DateTime local) => _dateTimeHelper.ConvertToUtcTime(local, tz);

        var customer = Services.WorkContext.CurrentCustomer;
        var model = new StudioDashboardModel
        {
            Now = now,
            UserName = (customer.FirstName.NullEmpty() ?? customer.FullName.NullEmpty() ?? customer.Username)?.Trim()
        };

        // ---- Orders of the last year, bucketed per local day ----
        var cancelled = (int)OrderStatus.Cancelled;
        var historyStartUtc = ToUtc(firstDay);
        var orders = await _db.Orders
            .AsNoTracking()
            .Where(x => !x.Deleted && x.OrderStatusId != cancelled && x.CreatedOnUtc >= historyStartUtc)
            .Select(x => new { x.Id, x.CreatedOnUtc, x.OrderTotal })
            .ToListAsync(cancelToken);

        var days = new Dictionary<DateTime, StudioDashboardDay>();
        for (var d = firstDay; d <= today; d = d.AddDays(1))
        {
            days[d] = new StudioDashboardDay { Date = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        }

        var hoursFrom = today.AddDays(-89);
        foreach (var order in orders)
        {
            var local = _dateTimeHelper.ConvertToUserTime(order.CreatedOnUtc, DateTimeKind.Utc);
            if (days.TryGetValue(local.Date, out var day))
            {
                day.Revenue += order.OrderTotal;
                day.Orders++;
            }
            if (local.Date >= hoursFrom)
            {
                model.Hours[local.Hour]++;
            }
        }

        model.Days = [.. days.Values];

        decimal Revenue(DateTime from, DateTime to) => days.Where(x => x.Key >= from && x.Key <= to).Sum(x => x.Value.Revenue);
        int OrderCount(DateTime from, DateTime to) => days.Where(x => x.Key >= from && x.Key <= to).Sum(x => x.Value.Orders);

        model.TodayRevenue = days[today].Revenue;
        model.TodayOrders = days[today].Orders;
        model.YesterdayRevenue = days.GetValueOrDefault(today.AddDays(-1))?.Revenue ?? 0;
        model.MonthRevenue = Revenue(monthStart, today);
        model.LastMonthRevenue = Revenue(lastMonthStart, monthStart.AddDays(-1));

        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        model.MonthForecast = Math.Round(model.MonthRevenue / today.Day * daysInMonth, 0);

        // Same span of last month (1st up to today's day number) for a fair comparison.
        var lastMonthSameSpan = Revenue(lastMonthStart, lastMonthStart.AddDays(Math.Min(today.Day, DateTime.DaysInMonth(lastMonthStart.Year, lastMonthStart.Month)) - 1));

        var from30 = today.AddDays(-29);
        var from60 = today.AddDays(-59);
        var orders30 = OrderCount(from30, today);
        var orders60 = OrderCount(from60, from30.AddDays(-1));
        var revenue30 = Revenue(from30, today);
        var revenue60 = Revenue(from60, from30.AddDays(-1));
        var last30 = model.Days.TakeLast(30).ToList();

        // ---- New registered customers ----
        var signupStartUtc = ToUtc(from60);
        var signups = (await _db.Customers
            .AsNoTracking()
            .Where(x => !x.Deleted && !x.IsSystemAccount && x.Email != null && x.Email != "" && x.CreatedOnUtc >= signupStartUtc)
            .Select(x => x.CreatedOnUtc)
            .ToListAsync(cancelToken))
            .Select(x => _dateTimeHelper.ConvertToUserTime(x, DateTimeKind.Utc).Date)
            .ToList();

        var signups30 = signups.Count(x => x >= from30);

        model.Kpis.Add(new StudioDashboardKpi
        {
            Label = "Doanh thu tháng " + today.Month,
            Value = model.MonthRevenue,
            Previous = lastMonthSameSpan,
            IsMoney = true,
            Caption = "so với cùng kỳ tháng trước",
            Icon = "bi-cash-coin",
            Tone = "pink",
            Spark = [.. last30.Select(x => x.Revenue)]
        });
        model.Kpis.Add(new StudioDashboardKpi
        {
            Label = "Đơn hàng 30 ngày",
            Value = orders30,
            Previous = orders60,
            Caption = "so với 30 ngày trước đó",
            Icon = "bi-bag-check",
            Tone = "yellow",
            Url = Url.Action("List", "Order"),
            Spark = [.. last30.Select(x => (decimal)x.Orders)]
        });
        model.Kpis.Add(new StudioDashboardKpi
        {
            Label = "Giá trị mỗi đơn",
            Value = orders30 > 0 ? Math.Round(revenue30 / orders30, 0) : 0,
            Previous = orders60 > 0 ? Math.Round(revenue60 / orders60, 0) : null,
            IsMoney = true,
            Caption = "trung bình 30 ngày",
            Icon = "bi-receipt",
            Tone = "mint",
            Spark = [.. last30.Select(x => x.Orders > 0 ? x.Revenue / x.Orders : 0)]
        });
        model.Kpis.Add(new StudioDashboardKpi
        {
            Label = "Khách mới 30 ngày",
            Value = signups30,
            Previous = signups.Count - signups30,
            Caption = "tài khoản đăng ký",
            Icon = "bi-person-plus",
            Tone = "lilac",
            Url = Url.Action("List", "Customer"),
            Spark = [.. Enumerable.Range(0, 30).Select(i => (decimal)signups.Count(x => x == from30.AddDays(i)))]
        });

        // ---- Revenue mix and best sellers of the last 30 days ----
        var from30Utc = ToUtc(from30);
        var items = await _db.OrderItems
            .AsNoTracking()
            .Where(x => !x.Order.Deleted && x.Order.OrderStatusId != cancelled && x.Order.CreatedOnUtc >= from30Utc)
            .Select(x => new { x.ProductId, x.Quantity, x.PriceInclTax })
            .ToListAsync(cancelToken);

        var productIds = items.Select(x => x.ProductId).Distinct().ToArray();
        var products = await _db.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.Sku, x.IsSystemProduct })
            .ToDictionaryAsync(x => x.Id, cancelToken);

        var keyProductIds = (await licenseService.GetProductPlansAsync(cancelToken)).Keys.ToHashSet();

        string LineOf(int productId)
        {
            var sku = products.GetValueOrDefault(productId)?.Sku;
            if (keyProductIds.Contains(productId) || sku.EqualsNoCase(Split3DUpgradeService.UpgradeProductSku))
            {
                return "keys";
            }

            return sku.EqualsNoCase(PrintOrderService.PrintProductSku) ? "print" : "goods";
        }

        var mix = items.GroupBy(x => LineOf(x.ProductId)).ToDictionary(x => x.Key, x => x.Sum(i => i.PriceInclTax));
        model.Mix =
        [
            new() { Label = "In 3D theo yêu cầu", Value = mix.GetValueOrDefault("print"), Tone = "pink" },
            new() { Label = "Sản phẩm studio", Value = mix.GetValueOrDefault("goods"), Tone = "yellow" },
            new() { Label = "Công cụ 3D (key)", Value = mix.GetValueOrDefault("keys"), Tone = "lilac" }
        ];

        model.TopProducts = [.. items
            .Where(x => products.TryGetValue(x.ProductId, out var p) && !p.IsSystemProduct && !p.Sku.EqualsNoCase(PrintOrderService.PrintProductSku))
            .GroupBy(x => x.ProductId)
            .Select(x => new StudioDashboardProduct
            {
                Name = products[x.Key].Name,
                Quantity = x.Sum(i => i.Quantity),
                Revenue = x.Sum(i => i.PriceInclTax),
                Url = Url.Action("Edit", "Product", new { id = x.Key })!
            })
            .OrderByDescending(x => x.Revenue)
            .ThenByDescending(x => x.Quantity)
            .Take(5)];

        // ---- Latest orders ----
        var latest = await _db.Orders
            .AsNoTracking()
            .Where(x => !x.Deleted)
            .OrderByDescending(x => x.CreatedOnUtc)
            .Take(7)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                x.OrderTotal,
                x.CreatedOnUtc,
                x.OrderStatusId,
                x.PaymentStatusId,
                x.BillingAddress.FirstName,
                x.BillingAddress.LastName,
                x.BillingAddress.Email
            })
            .ToListAsync(cancelToken);

        var latestIds = latest.Select(x => x.Id).ToArray();
        var latestItems = (await _db.OrderItems
            .AsNoTracking()
            .Where(x => latestIds.Contains(x.OrderId))
            .Select(x => new { x.OrderId, x.ProductId, x.Product.Sku })
            .ToListAsync(cancelToken))
            .ToLookup(x => x.OrderId);

        foreach (var o in latest)
        {
            var lines = latestItems[o.Id]
                .Select(x => keyProductIds.Contains(x.ProductId) || x.Sku.EqualsNoCase(Split3DUpgradeService.UpgradeProductSku)
                    ? "Công cụ 3D"
                    : x.Sku.EqualsNoCase(PrintOrderService.PrintProductSku) ? "In 3D" : "Sản phẩm")
                .Distinct();

            var name = $"{o.LastName} {o.FirstName}".Trim();
            model.LatestOrders.Add(new StudioDashboardOrder
            {
                Number = o.OrderNumber.NullEmpty() ?? o.Id.ToString(),
                Customer = name.NullEmpty() ?? o.Email ?? "Khách",
                Total = o.OrderTotal,
                CreatedOn = _dateTimeHelper.ConvertToUserTime(o.CreatedOnUtc, DateTimeKind.Utc),
                StatusKey = ((OrderStatus)o.OrderStatusId).ToString().ToLowerInvariant(),
                Status = (OrderStatus)o.OrderStatusId switch
                {
                    OrderStatus.Pending => "Chờ xử lý",
                    OrderStatus.Processing => "Đang xử lý",
                    OrderStatus.Complete => "Hoàn tất",
                    OrderStatus.Cancelled => "Đã huỷ",
                    _ => ((OrderStatus)o.OrderStatusId).ToString()
                },
                Paid = o.PaymentStatusId == (int)PaymentStatus.Paid,
                Kind = string.Join(" + ", lines).NullEmpty(),
                Url = Url.Action("Edit", "Order", new { id = o.Id })!
            });
        }

        // ---- Print room ----
        var jobs = await _db.PrintOrders()
            .AsNoTracking()
            .Where(x => _openJobStatuses.Contains(x.StatusId))
            .OrderBy(x => x.DesiredOnUtc == null)
            .ThenBy(x => x.DesiredOnUtc)
            .ThenBy(x => x.CreatedOnUtc)
            .Take(60)
            .ToListAsync(cancelToken);

        model.Jobs = [.. jobs.Select(x => new StudioDashboardJob
        {
            Id = x.Id,
            Code = x.Code,
            Customer = x.RecipientName,
            StatusId = x.StatusId,
            Grams = x.TotalGrams,
            Pieces = x.Pieces,
            Material = x.Material,
            DesiredOn = x.DesiredOnUtc.HasValue ? _dateTimeHelper.ConvertToUserTime(x.DesiredOnUtc.Value, DateTimeKind.Utc) : null,
            Url = Url.Action("Edit", "PrintJob", new { id = x.Id })!
        })];

        var doneStatuses = new[] { (int)PrintOrderStatus.Ready, (int)PrintOrderStatus.Completed };
        var monthStartUtc = ToUtc(monthStart);
        var lastMonthStartUtc = ToUtc(lastMonthStart);
        var finished = await _db.PrintOrders()
            .AsNoTracking()
            .Where(x => doneStatuses.Contains(x.StatusId) && (x.CompletedOnUtc ?? x.UpdatedOnUtc) >= lastMonthStartUtc)
            .Select(x => new { x.TotalGrams, On = x.CompletedOnUtc ?? x.UpdatedOnUtc })
            .ToListAsync(cancelToken);

        model.GramsThisMonth = finished.Where(x => x.On >= monthStartUtc).Sum(x => x.TotalGrams);
        model.GramsLastMonth = finished.Where(x => x.On < monthStartUtc).Sum(x => x.TotalGrams);

        // ---- Blender tools: keys and devices ----
        model.ActiveKeys = await _db.Split3DLicenses()
            .CountAsync(x => !x.Blocked && (x.ExpiresOnUtc == null || x.ExpiresOnUtc > nowUtc), cancelToken);

        var onlineSince = nowUtc.AddMinutes(-OnlineMinutes);
        var todayUtc = ToUtc(today);
        var devices = _db.Split3DDevices().AsNoTracking().Where(x => x.DeactivatedOnUtc == null);
        model.DevicesActive = await devices.CountAsync(cancelToken);
        model.DevicesOnline = await devices.CountAsync(x => x.LastSeenOnUtc >= onlineSince, cancelToken);
        model.DevicesToday = await devices.CountAsync(x => x.LastSeenOnUtc >= todayUtc, cancelToken);
        model.AddonVersions = [.. (await devices
            .GroupBy(x => x.AddonVersion)
            .Select(x => new { Version = x.Key, Count = x.Count() })
            .ToListAsync(cancelToken))
            .OrderByDescending(x => x.Count)
            .Take(4)
            .Select((x, i) => new StudioDashboardShare
            {
                Label = x.Version.NullEmpty() ?? "?",
                Value = x.Count,
                Tone = i switch { 0 => "mint", 1 => "yellow", 2 => "lilac", _ => "sky" }
            })];

        var expiringUntil = nowUtc.AddDays(ExpiringKeyDays);
        model.ExpiringKeys = [.. (await _db.Split3DLicenses()
            .AsNoTracking()
            .Where(x => !x.Blocked && x.ExpiresOnUtc != null && x.ExpiresOnUtc > nowUtc && x.ExpiresOnUtc <= expiringUntil)
            .OrderBy(x => x.ExpiresOnUtc)
            .Take(6)
            .Select(x => new { x.CustomerName, x.Email, x.Phone, x.ProductCode, x.ExpiresOnUtc })
            .ToListAsync(cancelToken))
            .Select(x => new StudioDashboardKey
            {
                Customer = x.CustomerName.NullEmpty() ?? x.Email ?? "Khách",
                Email = x.Email,
                Phone = x.Phone,
                Product = x.ProductCode,
                DaysLeft = Math.Max(0, (int)Math.Ceiling((x.ExpiresOnUtc!.Value - nowUtc).TotalDays))
            })];

        // ---- To-do: everything that waits for a human, most urgent first ----
        var pending = (int)OrderStatus.Pending;
        var dueSoonUtc = ToUtc(today.AddDays(3));
        var queued = new[] { (int)PrintOrderStatus.Paid, (int)PrintOrderStatus.Confirmed, (int)PrintOrderStatus.Printing };

        model.Todos =
        [
            new()
            {
                Title = "Đơn hàng chờ xử lý",
                Hint = "Kiểm tra thanh toán, xác nhận đơn",
                Count = await _db.Orders.CountAsync(x => !x.Deleted && x.OrderStatusId == pending, cancelToken),
                Url = Url.Action("List", "Order")!,
                Icon = "bi-bag",
                Tone = "pink"
            },
            new()
            {
                Title = "Đơn in chờ xác nhận",
                Hint = "Khách đã cọc, xem file và nhận đơn",
                Count = jobs.Count(x => x.StatusId == (int)PrintOrderStatus.Paid),
                Url = Url.Action("List", "PrintJob")!,
                Icon = "bi-printer",
                Tone = "mint"
            },
            new()
            {
                Title = "Đơn in sắp đến hạn",
                Hint = "Khách cần trong 3 ngày tới, chưa in xong",
                Count = jobs.Count(x => queued.Contains(x.StatusId) && x.DesiredOnUtc != null && x.DesiredOnUtc < dueSoonUtc),
                Url = Url.Action("List", "PrintJob")!,
                Icon = "bi-alarm",
                Tone = "sky"
            },
            new()
            {
                Title = "Yêu cầu báo giá cần gọi",
                Hint = "Yêu cầu mới và lịch hẹn gọi lại đã tới",
                Count = await followUpService.CountDueAsync(cancelToken),
                Url = Url.Action("List", "PrintQuote")!,
                Icon = "bi-telephone",
                Tone = "yellow"
            },
            new()
            {
                Title = "Đơn key chờ cấp",
                Hint = "Đã chuyển khoản, cần gửi key",
                Count = (await orderQuery.GetOrdersAsync(null, true, 100, cancelToken)).Count,
                Url = Url.Action("List", "Split3D")!,
                Icon = "bi-key",
                Tone = "lilac"
            }
        ];

        if (model.ExpiringKeys.Count > 0)
        {
            model.Todos.Add(new()
            {
                Title = "Key sắp hết hạn",
                Hint = $"Nhắc gia hạn trong {ExpiringKeyDays} ngày tới",
                Count = model.ExpiringKeys.Count,
                Url = Url.Action("List", "Split3D")!,
                Icon = "bi-hourglass-split",
                Tone = "sky"
            });
        }

        return View(model);
    }

    /// <summary>
    /// Opens an order by its number (or id) typed into the quick-jump palette, or a print job by its code ("IN00012").
    /// </summary>
    [Permission(Permissions.Order.Read)]
    public async Task<IActionResult> Find(string? q)
    {
        var term = q?.Trim().TrimStart('#');
        if (term.IsEmpty())
        {
            return RedirectToAction("List", "Order");
        }

        var jobId = await _db.PrintOrders()
            .Where(x => x.Code == term.ToUpper())
            .Select(x => x.Id)
            .FirstOrDefaultAsync();
        if (jobId != 0)
        {
            return RedirectToAction("Edit", "PrintJob", new { id = jobId });
        }

        var orderId = await _db.Orders
            .Where(x => !x.Deleted && x.OrderNumber == term)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();
        if (orderId == 0 && int.TryParse(term, out var id) && await _db.Orders.AnyAsync(x => x.Id == id && !x.Deleted))
        {
            orderId = id;
        }

        if (orderId != 0)
        {
            return RedirectToAction("Edit", "Order", new { id = orderId });
        }

        NotifyWarning($"Không tìm thấy đơn \"{term}\".");
        return RedirectToAction("Index");
    }

    internal static string FormatMoney(decimal value)
        => value.ToString("#,##0", _vi) + "đ";
}
