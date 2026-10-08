using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Content.Media;
using Smartstore.Web.Components;

namespace Smartstore.Split3D.Components;

/// <summary>
/// Header logo: image plus two-colored word mark and a small line below, all set in
/// TT Minimal Studio › Settings. Invoked by the TTMinimal theme's ShopLogo view.
/// </summary>
public partial class StudioBrandViewComponent : SmartViewComponent
{
    public const string DefaultLogoPath = "~/Modules/Smartstore.Split3D/studio/studio-logo.webp";

    private readonly StudioSettings _settings;
    private readonly IMediaService _mediaService;

    public StudioBrandViewComponent(StudioSettings settings, IMediaService mediaService)
    {
        _settings = settings;
        _mediaService = mediaService;
    }

    /// <param name="title">Link title, usually the store name.</param>
    public async Task<IViewComponentResult> InvokeAsync(string title = null)
    {
        var defaults = new StudioSettings();
        var logoUrl = _settings.LogoMediaFileId > 0
            ? await _mediaService.GetUrlAsync(_settings.LogoMediaFileId, 256, null, false)
            : null;

        var font = _settings.LogoFont?.Trim();
        if (font.HasValue() && !FontRegex().IsMatch(font))
        {
            font = null;
        }

        var model = new StudioBrandModel
        {
            Title = title.NullEmpty() ?? _settings.BrandName,
            LogoUrl = logoUrl.NullEmpty() ?? Url.Content(DefaultLogoPath + "?v=" + StudioAssets.Version),
            Text1 = _settings.LogoText1,
            Color1 = SafeColor(_settings.LogoColor1, defaults.LogoColor1),
            Text2 = _settings.LogoText2,
            Color2 = SafeColor(_settings.LogoColor2, defaults.LogoColor2),
            Subline = _settings.LogoSubline,
            Font = font,
            ShowRegistered = _settings.LogoShowRegistered
        };

        if (!model.Text1.HasValue() && !model.Text2.HasValue())
        {
            model.Text2 = _settings.BrandName;
        }

        return View(model);
    }

    private static string SafeColor(string color, string fallback)
        => color.HasValue() && ColorRegex().IsMatch(color.Trim()) ? color.Trim() : fallback;

    [GeneratedRegex("^#([0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex ColorRegex();

    [GeneratedRegex(@"^[\p{L}0-9 ]{1,60}$")]
    private static partial Regex FontRegex();
}
