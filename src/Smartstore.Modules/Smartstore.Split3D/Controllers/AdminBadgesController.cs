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
    public async Task<IActionResult> Index([FromServices] Split3DOrderQuery orderQuery)
    {
        var cancelToken = HttpContext.RequestAborted;
        var pending = (int)OrderStatus.Pending;

        // Key orders still waiting for payment confirmation or a key/upgrade (same list as "Pending orders" on the key page).
        var keyOrders = (await orderQuery.GetOrdersAsync(null, true, 100, cancelToken)).Count;

        var badges = new Dictionary<string, int>
        {
            ["orders"] = await _db.Orders.CountAsync(x => !x.Deleted && x.OrderStatusId == pending, cancelToken),
            ["split3d-licenses"] = keyOrders,
            ["tt-studio-quotes"] = await _db.PrintQuoteRequests().CountAsync(x => x.StatusId == (int)PrintQuoteStatus.New, cancelToken)
        };

        Response.Headers.CacheControl = "no-store";
        return Json(badges);
    }
}
