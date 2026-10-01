using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Seo;
using Smartstore.Web.Components;

namespace Smartstore.Split3D.Components;

/// <summary>
/// Main navigation of the studio header: printing service, shop (top-level categories), 3D tools, design service.
/// Invoked by the TTMinimal theme's ShopBar view.
/// </summary>
public class StudioNavViewComponent : SmartViewComponent
{
    private readonly SmartDbContext _db;
    private readonly IMediaService _mediaService;

    public StudioNavViewComponent(SmartDbContext db, IMediaService mediaService)
    {
        _db = db;
        _mediaService = mediaService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var path = HttpContext.Request.Path.Value ?? "/";
        var customer = Services.WorkContext.CurrentCustomer;
        var storeId = Services.StoreContext.CurrentStore.Id;
        var homeUrl = Url.RouteUrl("Homepage");

        var categories = await _db.Categories
            .AsNoTracking()
            .ApplyStandardFilter(false, customer.GetRoleIds(), storeId)
            .Where(x => x.ParentId == null || x.ParentId == 0)
            .ToListAsync();

        var addonCategory = categories.FirstOrDefault(x => Split3DStorefrontContent.CategoryNames.Contains(x.Name));
        var model = new StudioNavModel();

        model.Items.Add(new StudioNavItem
        {
            Text = "In 3D",
            Url = Url.RouteUrl(StudioStorefrontSetup.PrintServiceRouteName),
            IsActive = path.StartsWith("/in-3d", StringComparison.OrdinalIgnoreCase)
        });

        var shop = new StudioNavItem { Text = "Cửa hàng", Url = homeUrl + "#tt-products" };
        foreach (var category in categories.Where(x => x != addonCategory))
        {
            var url = Url.RouteUrl("Category", new { SeName = await category.GetActiveSlugAsync() });
            shop.Children.Add(new StudioNavItem
            {
                Text = category.GetLocalized(x => x.Name),
                Url = url,
                ImageUrl = category.MediaFileId > 0 ? await _mediaService.GetUrlAsync(category.MediaFileId, 256, null, false) : null,
                IsActive = path.EqualsNoCase(url)
            });
        }

        shop.IsActive = shop.Children.Any(x => x.IsActive);
        model.Items.Add(shop);

        if (addonCategory != null)
        {
            var categoryUrl = Url.RouteUrl("Category", new { SeName = await addonCategory.GetActiveSlugAsync() });
            model.Items.Add(new StudioNavItem
            {
                Text = "Công cụ 3D",
                Url = Url.RouteUrl(StudioStorefrontSetup.ToolsRouteName),
                IsActive = path.StartsWith("/cong-cu-3d", StringComparison.OrdinalIgnoreCase) || path.EqualsNoCase(categoryUrl)
            });
        }

        model.Items.Add(new StudioNavItem
        {
            Text = "Thiết kế",
            Url = Url.RouteUrl(StudioStorefrontSetup.DesignServiceRouteName),
            IsActive = path.StartsWith("/thiet-ke", StringComparison.OrdinalIgnoreCase)
        });

        return View(model);
    }
}
