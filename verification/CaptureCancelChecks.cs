using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Clipsa;

internal static class CaptureCancelChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Clipsa-cancel-" + Guid.NewGuid().ToString("N")));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        var host = new Window { Title = "Clipsa cancellation QA", Width = 480, Height = 330, Left = 80, Top = 180, Topmost = true, Content = new TextBlock { Text = "Cancellation test target", FontSize = 20 } };
        GetCursorPos(out var originalCursor);
        try
        {
            host.Show(); ScrollChecks.Pump(150);
            var directory = AppPaths.SessionFolder;
            var before = System.IO.Directory.Exists(directory) ? System.IO.Directory.GetFiles(directory, "*.png").Length : 0;
            foreach (var right in new[] { false, true })
            {
                foreach (var dragging in new[] { false, true })
                {
                    var region = new RegionWindow();
                    RunDialog(region, () =>
                    {
                        if (dragging)
                        {
                            var point = region.PointToScreen(new Point(180, 220)); SetCursorPos((int)point.X, (int)point.Y);
                            mouse_event(2, 0, 0, 0, UIntPtr.Zero); ScrollChecks.Pump(80);
                            Require(region.IsMouseCaptured, "selection drag really started");
                        }
                        Gesture(right);
                    });
                    mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                    Require(region.SelectedRectangle is null && !region.IsMouseCaptured, "selection cancelled without rectangle or retained mouse capture");
                }
                var a = host.PointToScreen(new Point(25, 45));
                var b = host.PointToScreen(new Point(225, 205));
                var type = typeof(MainWindow).Assembly.GetType("Clipsa.ScrollCaptureWindow")!;
                var scroll = (Window)Activator.CreateInstance(type, new object[] { new System.Drawing.Rectangle((int)a.X, (int)a.Y, (int)(b.X - a.X), (int)(b.Y - a.Y)), 250 })!;
                RunDialog(scroll, () =>
                {
                    host.Activate(); Keyboard.Focus(host); ScrollChecks.Pump(80);
                    Require(!scroll.IsActive, "scroll cancellation tested with another window focused");
                    Gesture(right);
                });
                Require(type.GetProperty("ResultPath")!.GetValue(scroll) is null, "cancelled scroll has no result");
                ScrollChecks.Pump(100);
            }
            CheckPreparation();
            var after = System.IO.Directory.Exists(directory) ? System.IO.Directory.GetFiles(directory, "*.png").Length : 0;
            Require(before == after, "cancellation creates no PNG");
            // Hooks must disappear after cancellation: input goes to the target again.
            int escapeReceived = 0, rightReceived = 0;
            host.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) escapeReceived++; };
            host.PreviewMouseRightButtonUp += (_, _) => rightReceived++;
            host.Activate(); Keyboard.Focus(host);
            var target = host.PointToScreen(new Point(70, 80)); SetCursorPos((int)target.X, (int)target.Y);
            Gesture(false); Gesture(true); ScrollChecks.Pump(120);
            Require(escapeReceived == 1 && rightReceived == 1, "Escape and right click restored after session ends");
            Console.WriteLine("Escape/right-click: selection before/during drag, scroll outside panel focus, preparation and input cleanup OK. No cancelled PNG created.");
        }
        finally { mouse_event(4, 0, 0, 0, UIntPtr.Zero); SetCursorPos(originalCursor.X, originalCursor.Y); host.Close(); CaptureService.Cleanup(); app.Shutdown(); }
    }
    private static void RunDialog(Window window, Action gesture)
    {
        Exception? failure = null; var expired = false;
        var trigger = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        trigger.Tick += (_, _) =>
        {
            trigger.Stop();
            try { gesture(); } catch (Exception ex) { failure = ex; window.Close(); }
        };
        timeout.Tick += (_, _) => { timeout.Stop(); expired = true; window.Close(); };
        window.Loaded += (_, _) => { trigger.Start(); timeout.Start(); };
        try { Require(window.ShowDialog() != true, "cancelled modal result"); }
        finally { trigger.Stop(); timeout.Stop(); if (window.IsVisible) window.Close(); }
        if (failure is not null) throw failure;
        Require(!expired, "cancel gesture closes modal promptly");
    }
    private static void CheckPreparation()
    {
        var main = new MainWindow();
        var settings = (AppSettings)typeof(MainWindow).GetField("settings", Private)!.GetValue(main)!;
        settings.CheckUpdatesOnStartup = false; settings.KeepInTray = false;
        var store = (CaptureStore)typeof(MainWindow).GetField("store", Private)!.GetValue(main)!;
        main.Show(); ScrollChecks.Pump(100);
        try
        {
            foreach (var right in new[] { false, true })
            {
                typeof(MainWindow).GetMethod("CaptureFull", Private)!.Invoke(main, null);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
                timer.Tick += (_, _) => { timer.Stop(); Gesture(right); };
                timer.Start(); ScrollChecks.Pump(450); timer.Stop();
                var busy = (bool)typeof(MainWindow).GetField("captureBusy", Private)!.GetValue(main)!;
                Require(store.Items.Count == 0 && main.IsVisible && !busy, $"preparation cancelled and bar ready again (right={right}, items={store.Items.Count}, visible={main.IsVisible}, busy={busy})");
            }
        }
        finally { main.Close(); }
    }
    private static void Gesture(bool right)
    {
        if (right) { mouse_event(8, 0, 0, 0, UIntPtr.Zero); ScrollChecks.Pump(180); mouse_event(16, 0, 0, 0, UIntPtr.Zero); }
        else { keybd_event(0x1B, 0, 0, UIntPtr.Zero); ScrollChecks.Pump(180); keybd_event(0x1B, 0, 2, UIntPtr.Zero); }
    }
    private static void Require(bool value, string name) { if (!value) throw new Exception(name); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
