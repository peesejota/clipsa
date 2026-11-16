using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using Clipsa.Capture;
namespace Clipsa;
public static class CaptureService
{
    public static string TempFolder => AppPaths.SessionFolder;
    public static string CaptureScreen(string monitorDeviceName = "") { using var bmp = DesktopCapture.Capture(MonitorService.Resolve(monitorDeviceName).Bounds); return Save(bmp); }
    public static string? CaptureRegion(string monitorDeviceName = "") { var overlay = new RegionWindow(monitorDeviceName); overlay.ShowDialog(); Native.DwmFlush(); return overlay.SelectedRectangle is { Width: > 4, Height: > 4 } r ? CaptureRectangle(r) : null; }
    public static string? CaptureScroll(int steps, int delay, double overlap, string monitorDeviceName = "")
    {
        var screen = MonitorService.Resolve(monitorDeviceName);
        var overlay = new RegionWindow(screen.DeviceName, true); overlay.ShowDialog();
        using var initial = overlay.TakeScrollBaseline();
        if (overlay.SelectedRectangle is not { Width: > 40, Height: > 80 } rect) return null;
        Native.DwmFlush(); // Wait for the closed selection overlay to leave the desktop composition.
        var session = new ScrollCaptureWindow(new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), delay);
        session.StartAutomatically = !overlay.ScrollWhileHeld;
        if (initial is not null) session.PrepareBaseline(initial);
        if (overlay.ScrollWhileHeld) session.UseHoldGesture(overlay.InitialWheelDelta, overlay.InitialScrollKey);
        session.ShowCapture(); return session.ResultPath;
    }
    private static string CaptureRectangle(Rect r) { using var bmp = DesktopCapture.Capture(new System.Drawing.Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height)); return Save(bmp); }
    internal static string Save(Bitmap bmp)
    {
        string? path = null;
        var created = false;
        try
        {
            AppPaths.InitializeSession();
            path = Path.Combine(TempFolder, $"Captura_{Guid.NewGuid():N}.png");
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            bmp.Save(stream, ImageFormat.Png);
            return path;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Save capture");
            if (created && path is not null) try { File.Delete(path); } catch (Exception cleanup) { AppDiagnostics.Log(cleanup, "Remove incomplete capture"); }
            throw;
        }
    }
    public static void Cleanup()
    {
        try
        {
            var session = Path.GetFullPath(AppPaths.SessionFolder);
            var parent = Path.GetFullPath(AppPaths.SessionsDirectory);
            if (!string.Equals(Path.GetDirectoryName(session), parent.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(session), "N", out _))
                throw new InvalidOperationException("Invalid capture session folder.");
            AppPaths.ReleaseSession();
            if (Directory.Exists(session)) Directory.Delete(session, true);
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Clean capture session"); }
    }
}
