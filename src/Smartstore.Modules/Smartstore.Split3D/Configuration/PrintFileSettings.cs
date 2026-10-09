namespace Smartstore.Split3D.Configuration;

/// <summary>
/// Defaults of the print files built on the admin order page ("File in 3D", see
/// <see cref="Components.OrderPrintFilesViewComponent"/>): every design is split into a frame with pockets and the
/// content pieces pressed into them, printed apart one color at a time. Changed on the card itself.
/// </summary>
public class PrintFileSettings : ISettings
{
    /// <summary>
    /// Gap in mm on every side between a content piece and its pocket in the frame. The studio's reference prints
    /// use none (see PRINT-STANDARD.md).
    /// </summary>
    public double Clearance { get; set; }

    /// <summary>
    /// Depth of the pockets in percent of the frame thickness: 50 = 2 mm pockets in a 4 mm frame (the standard).
    /// 100 cuts them through the frame (the islands inside letters such as o, a, d then come loose).
    /// </summary>
    public double PocketPercent { get; set; } = 50;

    /// <summary>File format: <c>3mf</c>, <c>stl</c> or <c>glb</c> (Blender, with colors).</summary>
    public string Format { get; set; } = "3mf";

    /// <summary>
    /// How "Cả bộ" and "Tải tất cả" pack a design: <c>assembled</c> (frame and content in one file, the content in its
    /// pockets, for a printer with several filaments), <c>beside</c> (one file, the content laid out next to the frame)
    /// or <c>split</c> (a file for the frame and one per content color).
    /// </summary>
    public string Layout { get; set; } = "assembled";
}
