using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace Clipsa;

// Visual guide only: it neither takes focus nor receives mouse/trackpad input.
internal sealed class ScrollRegionFrame : Window
{
    private readonly Border outline = new() { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(3) };
    private bool? waitingForJoin;
    public ScrollRegionFrame(Rectangle region)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false; Content = outline;
        var screen = System.Windows.Forms.Screen.FromRectangle(region);
        var scale = MonitorService.DpiScale(screen);
        var bounds = region; bounds.Inflate(3, 3); bounds.Intersect(screen.Bounds);
        Left = bounds.Left / scale.X; Top = bounds.Top / scale.Y; Width = bounds.Width / scale.X; Height = bounds.Height / scale.Y;
        SetGap(false);
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(handle, -20).ToInt64();
            SetWindowLongPtr(handle, -20, new IntPtr(style | 0x20 | 0x08000000 | 0x80));
            MonitorService.PlaceWindow(this, bounds);
        };
    }
    public bool ShowGuide()
    {
        Show();
        if (Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11)) return true;
        // Do not risk painting a guide into the saved image on unsupported desktops.
        Close(); return false;
    }
    public void SetGap(bool waiting)
    {
        if (waitingForJoin == waiting) return;
        waitingForJoin = waiting;
        outline.BorderBrush = new SolidColorBrush(waiting ? Color.FromRgb(255, 195, 105) : Color.FromRgb(185, 158, 255));
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
