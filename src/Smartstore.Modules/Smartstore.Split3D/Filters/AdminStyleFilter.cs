using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Widgets;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Gives the admin area the TT Minimal "Pop Studio" look by adding the studio admin stylesheet,
/// the brand font and the tt favicon to every full admin page (registered in Startup).
/// </summary>
public class AdminStyleFilter : IResultFilter
{
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly Lazy<IUrlHelper> _urlHelper;

    public AdminStyleFilter(Lazy<IWidgetProvider> widgetProvider, Lazy<IUrlHelper> urlHelper)
    {
        _widgetProvider = widgetProvider;
        _urlHelper = urlHelper;
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ViewResult || !context.HttpContext.Request.IsAdminArea())
        {
            return;
        }

        var url = _urlHelper.Value;
        var html =
            "<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin />" +
            "<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700;800;900&display=swap\" />" +
            $"<link rel=\"stylesheet\" href=\"{url.Content(StudioAssets.AdminStyleSheet)}\" />" +
            $"<link rel=\"icon\" type=\"image/svg+xml\" href=\"{url.Content(StudioAssets.Favicon)}\" />" +
            "<meta name=\"color-scheme\" content=\"light dark\" />" +
            "<meta name=\"theme-color\" content=\"#20201f\" />";

        _widgetProvider.Value.RegisterWidget("head_links", new HtmlWidget(html));
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
