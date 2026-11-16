using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Clipsa.Capture;

public static class CapturePixels
{
    public static byte[] Read(Bitmap image)
    {
        var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[checked(image.Width * image.Height * 4)];
            for (var y = 0; y < image.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * image.Width * 4, image.Width * 4);
            return pixels;
        }
        finally { image.UnlockBits(data); }
    }
    public static unsafe bool Equal(Bitmap first, Bitmap second)
    {
        if (first.Size != second.Size) return false;
        var rect = new Rectangle(0, 0, first.Width, first.Height);
        var a = first.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var b = second.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try { for (var y = 0; y < first.Height; y++) if (!new ReadOnlySpan<byte>((a.Scan0 + y * a.Stride).ToPointer(), first.Width * 4).SequenceEqual(new ReadOnlySpan<byte>((b.Scan0 + y * b.Stride).ToPointer(), first.Width * 4))) return false; return true; }
            finally { second.UnlockBits(b); }
        }
        finally { first.UnlockBits(a); }
    }
}
