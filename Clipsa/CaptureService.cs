using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
namespace Clipsa;
public static class CaptureService
{
    public static string TempFolder => AppPaths.SessionFolder;
    public static string CaptureScreen(string monitorDeviceName = "") { var b = MonitorService.Resolve(monitorDeviceName).Bounds; using var bmp = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb); using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(b.Location, System.Drawing.Point.Empty, b.Size); return Save(bmp); }
    public static string? CaptureRegion(string monitorDeviceName = "") { var overlay = new RegionWindow(monitorDeviceName); overlay.ShowDialog(); return overlay.SelectedRectangle is { Width: > 4, Height: > 4 } r ? CaptureRectangle(r) : null; }
    public static string? CaptureScroll(int steps, int delay, double overlap, string monitorDeviceName = "")
    {
        var screen = MonitorService.Resolve(monitorDeviceName);
        var overlay = new RegionWindow(screen.DeviceName); overlay.ShowDialog();
        if (overlay.SelectedRectangle is not { Width: > 40, Height: > 80 } rect) return null;
        Native.DwmFlush(); // Wait for the closed selection overlay to leave the desktop composition.
        var session = new ScrollCaptureWindow(new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), delay);
        session.ShowDialog(); return session.ResultPath;
    }
    private static string CaptureRectangle(Rect r) { using var bmp = new Bitmap((int)r.Width, (int)r.Height, PixelFormat.Format32bppArgb); using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen((int)r.X, (int)r.Y, 0, 0, bmp.Size); return Save(bmp); }
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
