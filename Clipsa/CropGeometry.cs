using System.Windows;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Clipsa;

public static class CropGeometry
{
    public static Rect Select(Point anchor, Point pointer, Size bounds, double ratio)
    {
        anchor = new Point(Math.Clamp(anchor.X, 0, bounds.Width), Math.Clamp(anchor.Y, 0, bounds.Height));
        pointer = new Point(Math.Clamp(pointer.X, 0, bounds.Width), Math.Clamp(pointer.Y, 0, bounds.Height));
        var sx = pointer.X >= anchor.X ? 1 : -1;
        var sy = pointer.Y >= anchor.Y ? 1 : -1;
        var width = Math.Abs(pointer.X - anchor.X);
        var height = Math.Abs(pointer.Y - anchor.Y);
        if (ratio > 0)
        {
            height = Math.Max(height, width / ratio);
            width = height * ratio;
            var maxWidth = sx > 0 ? bounds.Width - anchor.X : anchor.X;
            var maxHeight = sy > 0 ? bounds.Height - anchor.Y : anchor.Y;
            var scale = Math.Min(1, Math.Min(maxWidth / Math.Max(width, 1), maxHeight / Math.Max(height, 1)));
            width *= scale; height *= scale;
        }
        return new Rect(anchor, new Point(anchor.X + sx * width, anchor.Y + sy * height));
    }
}
