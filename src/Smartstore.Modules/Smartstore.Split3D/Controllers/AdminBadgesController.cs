using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Web.Controllers;

namespace Smartstore.Split3D.Controllers;

/// <summary>
/// Counts of work waiting in the admin, shown as badges on the top menu items (wwwroot/studio/admin.js polls this).
/// Keys are the menu item ids rendered as <c>li[data-id="nav-{id}"]</c>.
/// </summary>
public class AdminBadgesController : AdminController
{
    private readonly SmartDbContext _db;

    public AdminBadgesController(SmartDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromServices] Split3DOrderQuery orderQuery,
        [FromServices] PrintQuoteFollowUpService followUpService)
    {
        var cancelToken = HttpContext.RequestAborted;
        var pending = (int)OrderStatus.Pending;

        // Key orders still waiting for payment confirmation or a key/upgrade (same list as "Pending orders" on the key page).
        var keyOrders = (await orderQuery.GetOrdersAsync(null, true, 100, cancelToken)).Count;

        var badges = new Dictionary<string, int>
        {
            ["orders"] = await _db.Orders.CountAsync(x => !x.Deleted && x.OrderStatusId == pending, cancelToken),
            ["split3d-licenses"] = keyOrders,
            // New requests plus those whose follow-up date has passed: everything that needs a call today.
            ["tt-studio-quotes"] = await followUpService.CountDueAsync(cancelToken),
            // Print jobs that are paid and wait for the studio to confirm them.
            ["tt-studio-jobs"] = await _db.PrintOrders().CountAsync(x => x.StatusId == (int)PrintOrderStatus.Paid, cancelToken)
        };

        Response.Headers.CacheControl = "no-store";
        return Json(badges);
    }
}
