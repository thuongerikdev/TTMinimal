using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// The account's address list lives on the customer info page (see <see cref="CustomerInfoAddressesFilter"/>):
/// the separate address page, where adding, editing and deleting an address returns to, redirects there.
/// Registered for Customer/Addresses only, see Startup.
/// </summary>
public class CustomerAddressesRedirectFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        context.Result = new RedirectToActionResult("Info", "Customer", new { area = string.Empty });
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
