using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Clipsa.Capture;

internal static class DesktopCaptureChecks
{
    public static void Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var surface = new Border { Background = new SolidColorBrush(Color.FromRgb(67, 123, 211)) };
        var host = new Window { Title = "Clipsa native capture QA", Width = 240, Height = 180, Left = 40, Top = 260, Topmost = true, Content = surface };
        try
        {
            host.Show(); host.Activate(); host.UpdateLayout(); ScrollChecks.Pump(180); DwmFlush();
            var origin = surface.PointToScreen(new Point(20, 20));
            var rectangle = new System.Drawing.Rectangle((int)origin.X, (int)origin.Y, 80, 60);
            using var retained = DesktopCapture.Capture(rectangle);
            var original = CapturePixels.Read(retained);
            for (var i = 0; i < original.Length; i += 4) if (original[i] != 211 || original[i + 1] != 123 || original[i + 2] != 67 || original[i + 3] != 255) throw new Exception("Native BGRA capture has wrong color, alpha or physical coordinates.");
            for (var n = 0; n < 8; n++) using (var frame = DesktopCapture.Capture(rectangle)) { }
            using var process = Process.GetCurrentProcess(); var before = GetGuiResources(process.Handle, 0);
            for (var n = 0; n < 120; n++) using (var frame = DesktopCapture.Capture(rectangle)) { }
            GC.Collect(); GC.WaitForPendingFinalizers();
            if (!original.AsSpan().SequenceEqual(CapturePixels.Read(retained))) throw new Exception("Returned capture refers to released or reused native memory.");
            var after = GetGuiResources(process.Handle, 0);
            if (after > before + 1) throw new Exception($"GDI handles leaked: {before} -> {after}.");
            try { using var invalid = DesktopCapture.Capture(new System.Drawing.Rectangle(0, 0, 0, 1)); throw new Exception("Empty capture accepted."); } catch (ArgumentOutOfRangeException) { }
            Console.WriteLine($"Native capture: physical BGRA pixels, opaque alpha, independent buffer and 120 repeated captures without GDI leaks ({before} -> {after}): OK.");
        }
        finally { host.Close(); app.Shutdown(); }
    }
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
