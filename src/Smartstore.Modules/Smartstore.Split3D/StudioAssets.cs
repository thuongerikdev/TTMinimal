namespace Smartstore.Split3D;

/// <summary>
/// Static files of the studio storefront. Bump <see cref="Version"/> after changing them so browsers reload.
/// </summary>
public static class StudioAssets
{
    public const string Version = "14";

    public const string StyleSheet = "~/Modules/Smartstore.Split3D/studio/studio.css?v=" + Version;

    public const string Script = "~/Modules/Smartstore.Split3D/studio/studio.js?v=" + Version;

    /// <summary>
    /// 3D model reader (STL, OBJ, 3MF) and preview, loaded on demand by the price calculator.
    /// </summary>
    public const string MeshScript = "~/Modules/Smartstore.Split3D/studio/studio-mesh.js?v=" + Version;

    /// <summary>
    /// "Pop Studio" skin for the admin area (see <see cref="Filters.AdminStyleFilter"/>).
    /// </summary>
    public const string AdminStyleSheet = "~/Modules/Smartstore.Split3D/studio/admin.css?v=" + Version;

    public const string Favicon = "~/Modules/Smartstore.Split3D/studio/favicon.svg?v=" + Version;

    public const string FaviconPng = "~/Modules/Smartstore.Split3D/studio/favicon-32.png?v=" + Version;

    public const string AppleTouchIcon = "~/Modules/Smartstore.Split3D/studio/apple-touch-icon.png?v=" + Version;
}
