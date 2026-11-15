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
        foreach (var savedVertical in new[] { false, true })
        {
            foreach (var position in new[] { "Top", "Bottom", "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
                Require(!DockGeometry.IsVertical(position, savedVertical), "top/bottom/corners override a saved vertical preference");
            foreach (var position in new[] { "Left", "Right" })
                Require(DockGeometry.IsVertical(position, savedVertical), "left/right are always vertical");
            Require(DockGeometry.IsVertical("Custom", savedVertical) == savedVertical, "free position keeps its orientation preference");
        }
        Require(corner.Right == work.Right && corner.Bottom == work.Bottom, "bottom right anchor");
        Require(DockGeometry.Place(work, 400, 200, "Custom", 1, 1) == corner, "free coordinates reach the chosen corner");
        Console.WriteLine("Dock anchors, free positioning, negative monitor origins, oversized bars and edge tabs: OK.");
    }
    public static void Run(bool layoutOnly = false)
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
            foreach (var vertical in new[] { false, true })
            foreach (var position in DockGeometry.Positions)
            {
                settings.PanelVertical = vertical; settings.PanelPlacement = position; settings.PanelX = .3; settings.PanelY = .7; Place();
                var handle = new System.Windows.Interop.WindowInteropHelper(main).Handle; GetWindowRect(handle, out var actual);
                var screen = System.Windows.Forms.Screen.FromHandle(handle);
                Require(screen.WorkingArea.Contains(actual.Rectangle), "real HWND remains in selected monitor: " + position);
                var expectedVertical = position is "Left" or "Right" || position == "Custom" && vertical;
                Require(expectedVertical ? actual.Rectangle.Height > actual.Rectangle.Width : actual.Rectangle.Width > actual.Rectangle.Height, "expanded shape follows the selected edge despite saved preference: " + position);
                var header = (FrameworkElement)main.FindName("MainHeader"); var cards = (FrameworkElement)main.FindName("CardsScroll");
                var headerBounds = header.TransformToAncestor(main).TransformBounds(new Rect(header.RenderSize));
                var cardBounds = cards.TransformToAncestor(main).TransformBounds(new Rect(cards.RenderSize));
                var full = (Rectangle)typeof(MainWindow).GetField("fullDockBounds", Private)!.GetValue(main)!;
                var edge = DockGeometry.Edge(position, full, screen.WorkingArea, expectedVertical);
                Require(edge switch { "Left" => headerBounds.Right <= cardBounds.Left + 1, "Right" => headerBounds.Left >= cardBounds.Right - 1, "Bottom" => headerBounds.Top >= cardBounds.Bottom - 1, _ => headerBounds.Bottom <= cardBounds.Top + 1 }, "header follows attached edge: " + position);
                if (Environment.GetEnvironmentVariable("CLIPSA_QA_PREVIEW_DIR") is { Length: > 0 } previews && position is "Bottom" or "Left" or "Right")
                {
                    System.IO.Directory.CreateDirectory(previews);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(main.ActualWidth), (int)Math.Ceiling(main.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(main); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(previews, position + ".png")); encoder.Save(stream);
                }
            }
            foreach (var position in new[] { "Top", "Bottom", "Left", "Right" })
            {
                settings.PanelVertical = true;
                settings.PanelPlacement = position; settings.AutoHideCollapsed = !layoutOnly; settings.EdgeRevealDelay = 150; settings.EdgeHideDelay = 600;
                settings.PanelCollapsed = true;
                typeof(MainWindow).GetField("collapsed", Private)!.SetValue(main, true);
                typeof(MainWindow).GetField("autoHidden", Private)!.SetValue(main, !layoutOnly);
                var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(main).Handle);
                if (!layoutOnly) SetCursorPos(screen.WorkingArea.Left + screen.WorkingArea.Width / 2, screen.WorkingArea.Top + screen.WorkingArea.Height / 2);
                Place();
                if (!layoutOnly)
                {
                Require((bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "starts as edge tab");
                var tab = (Rectangle)typeof(MainWindow).GetField("hiddenDockBounds", Private)!.GetValue(main)!;
                SetCursorPos(tab.Left + tab.Width / 2, tab.Top + tab.Height / 2);
                ScrollChecks.Pump(80);
                Require((bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "brief crossing does not reveal");
                var revealDeadline = Environment.TickCount64 + 1200;
                while ((bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)! && Environment.TickCount64 < revealDeadline) ScrollChecks.Pump(40);
                Require(!(bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, $"edge dwell reveals bar: {position}; cursor={System.Windows.Forms.Cursor.Position}; tab={tab}; full={typeof(MainWindow).GetField("fullDockBounds", Private)!.GetValue(main)}; entered={typeof(MainWindow).GetField("edgeEntered", Private)!.GetValue(main)}; now={Environment.TickCount64}; visible={main.IsVisible}");
                }
                var compactBounds = (Rectangle)typeof(MainWindow).GetField("fullDockBounds", Private)!.GetValue(main)!;
                var isSide = position is "Left" or "Right";
                Require(isSide ? compactBounds.Height > compactBounds.Width : compactBounds.Width > compactBounds.Height, "collapsed shape ignores stale orientation on fixed edges: " + position);
                Require(((FrameworkElement)main.FindName("CompactScroll")).IsVisible == !isSide, "correct compact thumbnail strip: " + position);
                if (position is "Left" or "Right") Require(((FrameworkElement)main.FindName("SideCompactPanel")).IsVisible, "vertical compact controls are visible");
                if (layoutOnly) continue;
                SetCursorPos(screen.WorkingArea.Right - 150, screen.WorkingArea.Top + screen.WorkingArea.Height / 2);
                ScrollChecks.Pump(250);
                Require(!(bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "hide delay preserves the bar briefly: " + position);
                ScrollChecks.Pump(650);
                Require((bool)typeof(MainWindow).GetField("autoHidden", Private)!.GetValue(main)!, "bar hides after leaving: " + position);
            }
            settings.PanelPlacement = "BottomRight"; settings.PanelX = .2; settings.PanelY = .8;
            settings.AutoHideCollapsed = true;
            SettingsStore.Save(settings); var loaded = SettingsStore.Load();
            Require(loaded.PanelPlacement == "BottomRight" && loaded.PanelCollapsed && loaded.AutoHideCollapsed && loaded.EdgeRevealDelay == 150 && loaded.EdgeHideDelay == 600 && loaded.PanelX == .2, "dock preferences survive a settings roundtrip");
            var dialog = new SettingsWindow(settings);
            Require(((FrameworkElement)dialog.FindName("DockMonitorSection")).Visibility == (System.Windows.Forms.Screen.AllScreens.Length > 1 ? Visibility.Visible : Visibility.Collapsed), "monitor selector only appears for multiple screens");
            Require(((Slider)dialog.FindName("DockHideDelaySlider")).Value == 600 && ((Slider)dialog.FindName("DockDelaySlider")).Value == 150, "independent hide and reveal slider values");
            var positionBox = (ComboBox)dialog.FindName("DockPositionBox");
            foreach (var position in DockGeometry.Positions)
            {
                positionBox.SelectedValue = position;
                Require(((CheckBox)dialog.FindName("DockVerticalBox")).IsEnabled == (position == "Custom"), "manual orientation only available in free position");
            }
            dialog.Close();
            Console.WriteLine(layoutOnly ? "Real expanded/compact dock orientation with old vertical preferences, edge headers and settings controls: OK. Pointer-dependent hide/reveal checks skipped." : "Real dock positions, vertical compact layout, dwell reveal, automatic hiding and settings persistence: OK.");
        }
        finally { if (!layoutOnly) SetCursorPos(original.X, original.Y); main.Close(); CaptureService.Cleanup(); app.Shutdown(); }
    }
    private static void Require(bool value, string name) { if (!value) throw new Exception(name); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
}
