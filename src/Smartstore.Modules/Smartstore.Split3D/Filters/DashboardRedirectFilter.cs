using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Security;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Sends the admin start page to the studio workbench of <see cref="Controllers.StudioDashboardController"/>.
/// Any query string (e.g. <c>?full=1</c>) keeps the core widget dashboard, and so does a missing order permission.
/// Registered for Home/Index, see Startup.
/// </summary>
public class DashboardRedirectFilter : IAsyncActionFilter
{
    private readonly IPermissionService _permissionService;

    public DashboardRedirectFilter(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.IsAdminArea() && !request.QueryString.HasValue && await _permissionService.AuthorizeAsync(Permissions.Order.Read))
        {
            context.Result = new RedirectToActionResult("Index", "StudioDashboard", new { area = "Admin" });
            return;
        }

        await next();
    }
}
