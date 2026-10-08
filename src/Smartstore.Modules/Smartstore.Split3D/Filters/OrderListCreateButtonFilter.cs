using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Localization;
using Smartstore.Core.Security;
using Smartstore.Core.Widgets;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Adds a "create order" button to the toolbar of the admin order list, leading to
/// <see cref="Controllers.StudioOrderController"/>. Registered for Order/List, see Startup.
/// </summary>
public class OrderListCreateButtonFilter : IAsyncResultFilter
{
    private readonly IWidgetProvider _widgetProvider;
    private readonly IPermissionService _permissionService;
    private readonly IUrlHelper _urlHelper;
    private readonly Localizer T;

    public OrderListCreateButtonFilter(
        IWidgetProvider widgetProvider,
        IPermissionService permissionService,
        IUrlHelper urlHelper,
        Localizer localizer)
    {
        _widgetProvider = widgetProvider;
        _permissionService = permissionService;
        _urlHelper = urlHelper;
        T = localizer;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult
            && context.HttpContext.Request.IsAdminArea()
            && await _permissionService.AuthorizeAsync(Permissions.Order.Create))
        {
            var url = _urlHelper.Action("Create", "StudioOrder", new { area = "Admin" });
            var html =
                $"<a href=\"{url}\" class=\"btn btn-primary mr-2\">" +
                "<i class=\"fa fa-plus\"></i>" +
                $"<span>{WebUtility.HtmlEncode(T("Plugins.Split3D.OrderCreate.Button").Value)}</span></a>";

            _widgetProvider.RegisterWidget("admin_button_toolbar_before", new HtmlWidget(html));
        }

        await next();
    }
}
