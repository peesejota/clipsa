using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clipsa;

internal static class DockChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Geometry()
    {
        var work = new Rectangle(-1920, 40, 1920, 1040);
        foreach (var position in DockGeometry.Positions)
        foreach (var size in new[] { new System.Drawing.Size(1100, 276), new System.Drawing.Size(56, 400), new System.Drawing.Size(5000, 5000) })
        {
            var full = DockGeometry.Place(work, size.Width, size.Height, position, .3, .7);
            Require(work.Contains(full), "placement stays inside a negative-origin monitor");
            var edge = DockGeometry.Edge(position, full, work);
            var tab = DockGeometry.Tab(full, work, edge, 6, 90);
            Require(work.Contains(tab) && (tab.Width <= 6 || tab.Height <= 6), "edge tab stays in work area");
        }
        var corner = DockGeometry.Place(work, 400, 200, "BottomRight", 0, 0);
        Require(corner.Right == work.Right && corner.Bottom == work.Bottom, "bottom right anchor");
        Require(DockGeometry.Place(work, 400, 200, "Custom", 1, 1) == corner, "free coordinates reach the chosen corner");
        Console.WriteLine("Dock anchors, free positioning, negative monitor origins, oversized bars and edge tabs: OK.");
    }
    public static void Run()
    {
        Geometry();
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Clipsa-dock-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", root);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        var main = new MainWindow();
        var settings = (AppSettings)typeof(MainWindow).GetField("settings", Private)!.GetValue(main)!;
        settings.KeepInTray = false; settings.CheckUpdatesOnStartup = false;
        main.Show(); ScrollChecks.Pump(150);
        GetCursorPos(out var original);
        try
        {
            void Place() { typeof(MainWindow).GetMethod("PositionPanel", Private)!.Invoke(main, null); main.UpdateLayout(); ScrollChecks.Pump(60); }
            foreach (var position in DockGeometry.Positions)
            {
                settings.PanelPlacement = position; settings.PanelX = .3; settings.PanelY = .7; Place();
                var handle = new System.Windows.Interop.WindowInteropHelper(main).Handle; GetWindowRect(handle, out var actual);
                var screen = System.Windows.Forms.Screen.FromHandle(handle);
                Require(screen.WorkingArea.Contains(actual.Rectangle), "real HWND remains in selected monitor: " + position);
            }
            foreach (var position in new[] { "Top", "Bottom", "Left", "Right" })
            {
                settings.PanelPlacement = position; settings.AutoHideCollapsed = true; settings.EdgeRevealDelay = 150;
                settings.PanelCollapsed = true;
                typeof(MainWindow).GetField("collapsed", Private)!.SetValue(main, true);
                typeof(MainWindow).GetField("autoHidden", Private)!.SetValue(main, true);
                var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(main).Handle);
                SetCursorPos(screen.WorkingArea.Left + screen.WorkingArea.Width / 2, screen.WorkingArea.Top + screen.WorkingArea.Height / 2);
                Place();
                Require((bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "starts as edge tab");
                var tab = (Rectangle)typeof(MainWindow).GetField("hiddenDockBounds", Private)!.GetValue(main)!;
                SetCursorPos(tab.Left + tab.Width / 2, tab.Top + tab.Height / 2);
                ScrollChecks.Pump(80);
                Require((bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "brief crossing does not reveal");
                ScrollChecks.Pump(220);
                Require(!(bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "edge dwell reveals bar: " + position);
                if (position is "Left" or "Right") Require(((FrameworkElement)main.FindName("SideCompactPanel")).IsVisible, "vertical compact controls are visible");
                SetCursorPos(screen.WorkingArea.Right - 150, screen.WorkingArea.Top + screen.WorkingArea.Height / 2);
                ScrollChecks.Pump(1050);
                Require((bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "bar hides after leaving: " + position);
            }
            settings.PanelPlacement = "BottomRight"; settings.PanelX = .2; settings.PanelY = .8;
            SettingsStore.Save(settings); var loaded = SettingsStore.Load();
            Require(loaded.PanelPlacement == "BottomRight" && loaded.PanelCollapsed && loaded.AutoHideCollapsed && loaded.EdgeRevealDelay == 150 && loaded.PanelX == .2, "dock preferences survive a settings roundtrip");
            Console.WriteLine("Real dock positions, vertical compact layout, dwell reveal, automatic hiding and settings persistence: OK.");
        }
        finally { SetCursorPos(original.X, original.Y); main.Close(); CaptureService.Cleanup(); app.Shutdown(); }
    }
    private static void Require(bool value, string name) { if (!value) throw new Exception(name); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
}
