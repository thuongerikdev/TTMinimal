using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Sends the plain admin product list (menu links, "back to list" after saving or deleting a product) to the grouped
/// overview of <see cref="Controllers.StudioProductsController"/>. Any query string (e.g. <c>?full=1</c> or a category
/// filter) keeps the core grid. Registered for Product/List, see Startup.
/// </summary>
public class ProductListRedirectFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.IsAdminArea() && !request.QueryString.HasValue)
        {
            context.Result = new RedirectToActionResult("Index", "StudioProducts", new { area = "Admin" });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
