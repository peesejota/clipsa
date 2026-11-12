using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace Clipsa;

public static class MonitorService
{
    public static Forms.Screen Resolve(string deviceName = "") =>
        Forms.Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
        ?? Forms.Screen.FromPoint(Forms.Cursor.Position);
    public static IReadOnlyList<Forms.Screen> Screens => Forms.Screen.AllScreens;
    public static Rect WorkArea(Forms.Screen screen) => ToDip(screen.WorkingArea, screen);
    public static Rect Bounds(Forms.Screen screen) => ToDip(screen.Bounds, screen);
    public static Vector DpiScale(Forms.Screen screen)
    {
        var point = new NativePoint { X = screen.Bounds.Left + screen.Bounds.Width / 2, Y = screen.Bounds.Top + screen.Bounds.Height / 2 };
        try
        {
            if (GetDpiForMonitor(MonitorFromPoint(point, 2), 0, out var x, out var y) == 0)
                return new Vector(x / 96d, y / 96d);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return new Vector(1, 1);
    }
    /// <summary>Target monitor DIP coordinates, including negative origins.</summary>
    public static Rect ToDip(System.Drawing.Rectangle pixels, Forms.Screen screen)
    {
        var scale = DpiScale(screen);
        return new Rect(pixels.X / scale.X, pixels.Y / scale.Y, pixels.Width / scale.X, pixels.Height / scale.Y);
    }
    public static Point ToDip(System.Drawing.Point pixels, Forms.Screen screen)
    {
        var scale = DpiScale(screen);
        return new Point(pixels.X / scale.X, pixels.Y / scale.Y);
    }
    /// <summary>Use after HWND creation for exact physical placement with mixed monitor DPI.</summary>
    public static void PlaceWindow(Window window, System.Drawing.Rectangle physicalBounds)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) throw new InvalidOperationException("The window must have an HWND before placement.");
        if (!SetWindowPos(handle, IntPtr.Zero, physicalBounds.X, physicalBounds.Y, physicalBounds.Width, physicalBounds.Height, 0x14))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
