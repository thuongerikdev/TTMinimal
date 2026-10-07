namespace Smartstore.Split3D;

/// <summary>
/// Static files of the studio storefront. Bump <see cref="Version"/> after changing them so browsers reload.
/// </summary>
public static class StudioAssets
{
    public const string Version = "50";

    public const string StyleSheet = "~/Modules/Smartstore.Split3D/studio/studio.css?v=" + Version;

    public const string Script = "~/Modules/Smartstore.Split3D/studio/studio.js?v=" + Version;

    /// <summary>
    /// 3D model reader (STL, OBJ, 3MF) and preview, loaded on demand by the price calculator.
    /// </summary>
    public const string MeshScript = "~/Modules/Smartstore.Split3D/studio/studio-mesh.js?v=" + Version;

    /// <summary>
    /// Excel (.xlsx) reader and writer for the name list import / template, loaded on demand on personalized product pages.
    /// </summary>
    public const string XlsxScript = "~/Modules/Smartstore.Split3D/studio/studio-xlsx.js?v=" + Version;

    /// <summary>
    /// 3D preview of the products with the designer (name plate, class board, keycap), loaded on their pages.
    /// </summary>
    public const string NameplateScript = "~/Modules/Smartstore.Split3D/studio/studio-nameplate.js?v=" + Version;

    /// <summary>
    /// Design panels (options, presets, order summary) of the products with the designer, loaded with the 3D preview.
    /// </summary>
    public const string DesignsScript = "~/Modules/Smartstore.Split3D/studio/studio-designs.js?v=" + Version;

    /// <summary>
    /// Folder of the module's wwwroot holding the shop's own font files for the name plate designer.
    /// </summary>
    public const string FontFolder = "studio/fonts";

    /// <summary>
    /// "Pop Studio" skin for the admin area (see <see cref="Filters.AdminStyleFilter"/>).
    /// </summary>
    public const string AdminStyleSheet = "~/Modules/Smartstore.Split3D/studio/admin.css?v=" + Version;

    /// <summary>
    /// Count badges on the admin top menu (polls <see cref="Controllers.AdminBadgesController"/>).
    /// </summary>
    public const string AdminScript = "~/Modules/Smartstore.Split3D/studio/admin.js?v=" + Version;

    public const string Favicon = "~/Modules/Smartstore.Split3D/studio/favicon.svg?v=" + Version;

    public const string FaviconPng = "~/Modules/Smartstore.Split3D/studio/favicon-32.png?v=" + Version;

    public const string AppleTouchIcon = "~/Modules/Smartstore.Split3D/studio/apple-touch-icon.png?v=" + Version;
}
