using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipsa;

internal static class GuidedScrollChecks
{
    private sealed record HostState(int X, int Y, int Width, int Height, double Offset, double Scale, long Handle, bool Arrow, double Target);
    public static void Host(string report, bool smooth = false)
    {
        var app = new Application();
        var pixels = new byte[360 * 2200 * 4]; new Random(77).NextBytes(pixels);
        for (var n = 3; n < pixels.Length; n += 4) pixels[n] = 255;
        var source = BitmapSource.Create(360, 2200, 96, 96, PixelFormats.Bgra32, null, pixels, 1440); source.Freeze();
        var image = new Image { Source = source, Width = 360, Height = 2200, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        ScrollViewer scroller = smooth ? new SmoothScroller() : new ScrollViewer();
        scroller.Content = image; scroller.Focusable = true; scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden; scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        var window = new Window { Title = "Clipsa guided scroll QA target", Content = scroller, Width = 400, Height = 400, Left = 30, Top = 200, Topmost = true };
        var arrow = false;
        scroller.PreviewKeyDown += (_, e) => { if (e.Key == Key.Down) arrow = true; };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            var a = scroller.PointToScreen(new Point(20, 20)); var b = scroller.PointToScreen(new Point(300, 260));
            var state = new HostState((int)a.X, (int)a.Y, (int)(b.X - a.X), (int)(b.Y - a.Y), scroller.VerticalOffset,
                PresentationSource.FromVisual(window)!.CompositionTarget!.TransformToDevice.M22, new System.Windows.Interop.WindowInteropHelper(window).Handle.ToInt64(), arrow, scroller is SmoothScroller animated ? animated.Target : scroller.VerticalOffset);
            try { File.WriteAllText(report + ".tmp", JsonSerializer.Serialize(state)); File.Move(report + ".tmp", report, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        };
        window.ContentRendered += (_, _) => { window.Activate(); Keyboard.Focus(scroller); timer.Start(); };
        window.Closed += (_, _) => timer.Stop();
        app.Run(window);
    }
    public static void Run(bool smooth = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "Clipsa-guided-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", root);
        var report = Path.Combine(root, "target.json");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add(smooth ? "--guided-scroll-host-smooth" : "--guided-scroll-host"); start.ArgumentList.Add(report);
        using var host = Process.Start(start)!;
        for (var n = 0; !File.Exists(report) && n < 100 && !host.HasExited; n++) Thread.Sleep(50);
        if (!File.Exists(report)) throw new Exception("Separate scroll QA target did not start.");
        HostState Read() { using var stream = new FileStream(report, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return JsonSerializer.Deserialize<HostState>(stream)!; }
        var initial = Read();
        GetWindowThreadProcessId(new IntPtr(initial.Handle), out var owner);
        if (owner != host.Id) throw new Exception("QA target window does not belong to the child.");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        var bounds = new System.Drawing.Rectangle(initial.X, initial.Y, initial.Width, initial.Height);
        var type = typeof(MainWindow).Assembly.GetType("Clipsa.ScrollCaptureWindow")!; var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var session = (Window)Activator.CreateInstance(type, new object[] { bounds, 250 })!;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        var stage = 0; var sawArrow = false; var sawWheel = false; var completing = false;
        double lastOffset = 0; byte[]? baseline = null;
        GetCursorPos(out var original);
        // Establish the same user-selected target as the real region gesture.
        SetCursorPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        mouse_event(2, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(4, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(100);
        session.ContentRendered += (_, _) => { baseline = (byte[]?)type.GetProperty("InitialPixels")!.GetValue(session); timer.Start(); };
        timer.Tick += (_, _) =>
        {
            var state = Read();
            if (stage == 0)
            {
                SetCursorPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                keybd_event(0x28, 0x50, 1, UIntPtr.Zero); Thread.Sleep(40); keybd_event(0x28, 0x50, 3, UIntPtr.Zero); stage = 1;
            }
            else if (stage == 1)
            {
                sawArrow = state.Offset > 0 && state.Arrow; lastOffset = state.Offset;
                mouse_event(0x800, 0, 0, unchecked((uint)-120), UIntPtr.Zero); stage = 2;
            }
            else if (stage == 2)
            {
                sawWheel = state.Offset > lastOffset;
                type.GetMethod("StartAutomatic", flags)!.Invoke(session, null); stage = 3;
            }
            else if (stage == 3 && state.Offset >= 160 && !completing)
            { completing = true; timer.Stop(); type.GetMethod("Finish", flags)!.Invoke(session, null); }
        };
        timeout.Tick += (_, _) => { timeout.Stop(); session.Close(); };
        string? path = null;
        try
        {
            timeout.Start(); type.GetMethod("ShowCapture")!.Invoke(session, null);
            path = (string?)type.GetProperty("ResultPath")!.GetValue(session);
            var status = ((TextBlock)type.GetField("status", flags)!.GetValue(session)!).Text;
            if (!sawArrow || !sawWheel || !completing || path is null) throw new Exception($"Guided/manual input failed: arrow={sawArrow}, wheel={sawWheel}, done={completing}, status={status}");
            var final = Read();
            if (smooth && Math.Abs(final.Offset - final.Target) > 1) throw new Exception("Capture finished before the smooth animation settled.");
            using var bitmap = new System.Drawing.Bitmap(path);
            if (Math.Abs(bitmap.Height - bounds.Height - final.Offset * final.Scale) > 2) throw new Exception($"Guided scroll output is truncated: actual={bitmap.Height}, initial={bounds.Height}, offset={final.Offset}, target={final.Target}, scale={final.Scale}.");
            var joined = Clipsa.Capture.CapturePixels.Read(bitmap);
            if (!baseline!.AsSpan(0, bounds.Width * 20 * 4).SequenceEqual(joined.AsSpan(0, bounds.Width * 20 * 4))) throw new Exception("Guided scroll lost its initial rows.");
            Console.WriteLine($"Separate target{(smooth ? " with animated scrolling" : "")}: arrow keys, mouse wheel, guided steps, stop/finish and complete output with initial rows OK ({bitmap.Width} x {bitmap.Height}).");
        }
        finally
        {
            timer.Stop(); timeout.Stop(); if (session.IsVisible) session.Close();
            SetCursorPos(original.X, original.Y);
            PostMessage(new IntPtr(initial.Handle), 0x10, IntPtr.Zero, IntPtr.Zero); host.WaitForExit(3000);
            if (path is not null) File.Delete(path); CaptureService.Cleanup(); app.Shutdown();
        }
    }
    private sealed class SmoothScroller : ScrollViewer
    {
        private readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromMilliseconds(16) };
        public double Target { get; private set; }
        public SmoothScroller()
        {
            animation.Tick += (_, _) => { var difference = Target - VerticalOffset; if (Math.Abs(difference) < .1) { animation.Stop(); return; } ScrollToVerticalOffset(VerticalOffset + Math.Clamp(difference, -2, 2)); };
            Unloaded += (_, _) => animation.Stop();
        }
        public void Move(double delta) { Target = Math.Clamp(Math.Max(Target, VerticalOffset) + delta, 0, ScrollableHeight); animation.Start(); }
        protected override void OnMouseWheel(MouseWheelEventArgs e) { Move(-e.Delta * .8); e.Handled = true; }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.Key == Key.Down) { Move(16); e.Handled = true; } else base.OnKeyDown(e); }
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new SmoothPeer(this);
    }
    private sealed class SmoothPeer(SmoothScroller viewer) : System.Windows.Automation.Peers.FrameworkElementAutomationPeer(viewer), System.Windows.Automation.Provider.IScrollProvider
    {
        protected override string GetClassNameCore() => "Clipsa QA animated scroll";
        protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => System.Windows.Automation.Peers.AutomationControlType.Pane;
        public override object? GetPattern(System.Windows.Automation.Peers.PatternInterface pattern) => pattern == System.Windows.Automation.Peers.PatternInterface.Scroll ? this : base.GetPattern(pattern);
        public bool HorizontallyScrollable => false;
        public bool VerticallyScrollable => viewer.ScrollableHeight > 0;
        public double HorizontalScrollPercent => -1;
        public double VerticalScrollPercent => viewer.ScrollableHeight > 0 ? viewer.VerticalOffset * 100 / viewer.ScrollableHeight : 0;
        public double HorizontalViewSize => 100;
        public double VerticalViewSize => viewer.ExtentHeight > 0 ? viewer.ViewportHeight * 100 / viewer.ExtentHeight : 100;
        public void Scroll(System.Windows.Automation.ScrollAmount horizontalAmount, System.Windows.Automation.ScrollAmount verticalAmount) => viewer.Move(verticalAmount switch { System.Windows.Automation.ScrollAmount.SmallIncrement => 32, System.Windows.Automation.ScrollAmount.LargeIncrement => 160, System.Windows.Automation.ScrollAmount.SmallDecrement => -32, System.Windows.Automation.ScrollAmount.LargeDecrement => -160, _ => 0 });
        public void SetScrollPercent(double horizontalPercent, double verticalPercent) { if (verticalPercent >= 0) viewer.Move(viewer.ScrollableHeight * verticalPercent / 100 - viewer.Target); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
