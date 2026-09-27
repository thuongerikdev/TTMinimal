using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Widgets;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Shows bank transfer instructions and a link to "My keys" on the order completed page
/// (registered for Checkout/Completed only, see Startup).
/// </summary>
public class CheckoutCompletedFilter : IAsyncActionFilter
{
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly Split3DSettings _settings;

    public CheckoutCompletedFilter(Lazy<IWidgetProvider> widgetProvider, Split3DSettings settings)
    {
        _widgetProvider = widgetProvider;
        _settings = settings;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        {
            _widgetProvider.Value.RegisterWidget("checkout_completed_top",
                new PartialViewWidget("_Split3DCheckoutCompleted", _settings, "Smartstore.Split3D"));
        }

        await next();
    }
}
