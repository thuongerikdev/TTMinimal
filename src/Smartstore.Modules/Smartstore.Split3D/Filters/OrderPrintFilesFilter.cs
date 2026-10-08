#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Widgets;
using Smartstore.Split3D.Components;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Puts the workflow card (<see cref="OrderWorkflowViewComponent"/>) and the "File in 3D" card
/// (<see cref="OrderPrintFilesViewComponent"/>) at the top of the admin order page.
/// Registered for Order/Edit, see Startup; the card renders nothing for orders without designer products.
/// </summary>
public class OrderPrintFilesFilter : IResultFilter
{
    private readonly Lazy<IWidgetProvider> _widgetProvider;

    public OrderPrintFilesFilter(Lazy<IWidgetProvider> widgetProvider)
    {
        _widgetProvider = widgetProvider;
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ViewResult
            || !context.HttpContext.Request.IsAdminArea()
            || !int.TryParse(context.RouteData.Values["id"]?.ToString(), out var orderId)
            || orderId <= 0)
        {
            return;
        }

        _widgetProvider.Value.RegisterWidget("order_edit_top", new ComponentWidget<OrderWorkflowViewComponent>(new { orderId }) { Order = -10 });
        _widgetProvider.Value.RegisterWidget("order_edit_top", new ComponentWidget<OrderPrintFilesViewComponent>(new { orderId }));
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
