using Microsoft.AspNetCore.Mvc;
using Smartstore.Web.Components;

namespace Smartstore.Split3D.Components;

/// <summary>
/// Studio footer and header contact bar (<c>view = "ContactBar"</c>). Invoked by the TTMinimal theme.
/// </summary>
public class StudioFooterViewComponent : SmartViewComponent
{
    private readonly StudioSettings _settings;

    public StudioFooterViewComponent(StudioSettings settings)
    {
        _settings = settings;
    }

    /// <param name="view">"Default" (footer) or "ContactBar".</param>
    /// <param name="smartstoreHint">The Smartstore copyright hint (<c>FooterModel.SmartStoreHint</c>), which must stay visible.</param>
    public IViewComponentResult Invoke(string view = null, string smartstoreHint = null)
    {
        var model = StudioContactModel.Create(_settings);
        ViewBag.SmartstoreHint = smartstoreHint;
        return View(view.NullEmpty() ?? "Default", model);
    }
}
