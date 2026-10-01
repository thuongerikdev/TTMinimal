using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Product pages of tool packages and the tools category page redirect to the tools page (package preselected), where the customer
/// picks a package and buys it directly (no quantity). Registered for Product/ProductDetails and Catalog/Category, see Startup.
/// </summary>
public class ToolPackageRedirectFilter : IAsyncActionFilter
{
    private readonly SmartDbContext _db;

    public ToolPackageRedirectFilter(SmartDbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments.TryGetValue("productId", out var value)
            && value is int productId
            && await _db.Split3DAddonProducts().AnyAsync(x => x.ProductId == productId))
        {
            context.Result = new RedirectToRouteResult(StudioStorefrontSetup.ToolsRouteName, new { goi = productId });
            return;
        }

        if (context.ActionArguments.TryGetValue("categoryId", out value)
            && value is int categoryId
            && await _db.Categories.AnyAsync(x => x.Id == categoryId && Split3DStorefrontContent.CategoryNames.Contains(x.Name)))
        {
            context.Result = new RedirectToRouteResult(StudioStorefrontSetup.ToolsRouteName, null);
            return;
        }

        await next();
    }
}
