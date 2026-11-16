#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

// Adapted for Clipsa by Francisco Serrano Jiménez, 2026-11-16.
// ShareX v22.4.0 / ShareX.HelpersLib/Helpers/WindowsImageInterop.cs, Capture/DrawNative.
// System.Drawing output, no cursor, explicit physical bounds and deterministic cleanup.
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Clipsa.Capture;

public static class DesktopCapture
{
    public static unsafe Bitmap Capture(Rectangle rectangle)
    {
        if (rectangle.Width < 1 || rectangle.Height < 1 || (long)rectangle.Width * rectangle.Height > 60_000_000)
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        if (!System.Windows.Forms.SystemInformation.VirtualScreen.Contains(rectangle))
            throw new ArgumentException("Capture is outside the virtual desktop.", nameof(rectangle));
        var window = GetDesktopWindow();
        var source = GetWindowDC(window);
        if (source == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr dc = IntPtr.Zero, handle = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var header = new BitmapHeader
            {
                Size = 40, Width = rectangle.Width, Height = -rectangle.Height,
                Planes = 1, BitCount = 32, ImageSize = checked((uint)rectangle.Width * (uint)rectangle.Height * 4)
            };
            handle = CreateDIBSection(dc, ref header, 0, out var pixels, IntPtr.Zero, 0);
            if (handle == IntPtr.Zero || pixels == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            previous = SelectObject(dc, handle);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!BitBlt(dc, 0, 0, rectangle.Width, rectangle.Height, source, rectangle.X, rectangle.Y, 0x40CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            GdiFlush();
            // BitBlt can clear alpha: screen pixels are opaque before leaving this buffer.
            var count = checked(rectangle.Width * rectangle.Height);
            var start = (uint*)pixels;
            for (var i = 0; i < count; i++) start[i] |= 0xFF000000;
            var result = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppArgb);
            try
            {
                var target = result.LockBits(new Rectangle(Point.Empty, rectangle.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (var y = 0; y < rectangle.Height; y++)
                        Buffer.MemoryCopy((byte*)pixels + y * rectangle.Width * 4, (target.Scan0 + y * target.Stride).ToPointer(), rectangle.Width * 4, rectangle.Width * 4);
                }
                finally { result.UnlockBits(target); }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1) && dc != IntPtr.Zero) SelectObject(dc, previous);
            if (handle != IntPtr.Zero) DeleteObject(handle);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            ReleaseDC(window, source);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapHeader
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, ImageSize; public int XPelsPerMeter, YPelsPerMeter; public uint ColorsUsed, ColorsImportant;
    }
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetWindowDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapHeader info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
}

