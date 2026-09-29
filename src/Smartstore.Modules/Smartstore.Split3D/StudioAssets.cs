namespace Smartstore.Split3D;

/// <summary>
/// Static files of the studio storefront. Bump <see cref="Version"/> after changing them so browsers reload.
/// </summary>
public static class StudioAssets
{
    public const string Version = "6";

    public const string StyleSheet = "~/Modules/Smartstore.Split3D/studio/studio.css?v=" + Version;

    public const string Script = "~/Modules/Smartstore.Split3D/studio/studio.js?v=" + Version;

    /// <summary>
    /// "Pop Studio" skin for the admin area (see <see cref="Filters.AdminStyleFilter"/>).
    /// </summary>
    public const string AdminStyleSheet = "~/Modules/Smartstore.Split3D/studio/admin.css?v=" + Version;

    public const string Favicon = "~/Modules/Smartstore.Split3D/studio/favicon.svg?v=" + Version;
}
