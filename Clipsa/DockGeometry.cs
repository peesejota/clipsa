using System.Drawing;

namespace Clipsa;

public static class DockGeometry
{
    public static readonly string[] Positions = ["Top", "Bottom", "Left", "Right", "TopLeft", "TopRight", "BottomLeft", "BottomRight", "Custom"];
    public static bool IsVertical(string position, bool freeVertical) => position switch
    {
        "Left" or "Right" => true,
        "Custom" => freeVertical,
        _ => false
    };
    public static Rectangle Place(Rectangle work, int width, int height, string position, double x, double y)
    {
        width = Math.Clamp(width, 1, work.Width); height = Math.Clamp(height, 1, work.Height);
        var dx = work.Width - width; var dy = work.Height - height;
        var px = position switch { "Left" or "TopLeft" or "BottomLeft" => 0, "Right" or "TopRight" or "BottomRight" => dx, "Custom" => (int)Math.Round(dx * Math.Clamp(x, 0, 1)), _ => dx / 2 };
        var py = position switch { "Top" or "TopLeft" or "TopRight" => 0, "Bottom" or "BottomLeft" or "BottomRight" => dy, "Custom" => (int)Math.Round(dy * Math.Clamp(y, 0, 1)), _ => dy / 2 };
        return new(work.Left + px, work.Top + py, width, height);
    }
    public static string Edge(string position, Rectangle full, Rectangle work, bool? vertical = null)
    {
        if (position.StartsWith("Top", StringComparison.Ordinal)) return "Top";
        if (position.StartsWith("Bottom", StringComparison.Ordinal)) return "Bottom";
        if (position is "Left" or "Right") return position;
        if (vertical is true) return Math.Abs(full.Left - work.Left) <= Math.Abs(work.Right - full.Right) ? "Left" : "Right";
        if (vertical is false) return Math.Abs(full.Top - work.Top) <= Math.Abs(work.Bottom - full.Bottom) ? "Top" : "Bottom";
        return new[] { ("Top", Math.Abs(full.Top - work.Top)), ("Bottom", Math.Abs(work.Bottom - full.Bottom)), ("Left", Math.Abs(full.Left - work.Left)), ("Right", Math.Abs(work.Right - full.Right)) }.MinBy(p => p.Item2).Item1;
    }
    public static Rectangle Tab(Rectangle full, Rectangle work, string edge, int thickness, int length)
    {
        thickness = Math.Max(2, thickness);
        return edge switch
        {
            "Left" => Place(work, thickness, Math.Min(length, full.Height), "Custom", 0, (double)(full.Top + full.Height / 2 - work.Top - Math.Min(length, full.Height) / 2) / Math.Max(1, work.Height - Math.Min(length, full.Height))),
            "Right" => Place(work, thickness, Math.Min(length, full.Height), "Custom", 1, (double)(full.Top + full.Height / 2 - work.Top - Math.Min(length, full.Height) / 2) / Math.Max(1, work.Height - Math.Min(length, full.Height))),
            _ => Place(work, Math.Min(length, full.Width), thickness, "Custom", (double)(full.Left + full.Width / 2 - work.Left - Math.Min(length, full.Width) / 2) / Math.Max(1, work.Width - Math.Min(length, full.Width)), edge == "Top" ? 0 : 1)
        };
    }
}
