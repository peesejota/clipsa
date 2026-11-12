using System.IO;
using System.Text.Json;

namespace Clipsa;

public static class AppDiagnostics
{
    private static readonly object sync = new();
    public static void Log(Exception error, string context)
    {
        try
        {
            lock (sync)
            {
                Directory.CreateDirectory(AppPaths.LogsDirectory);
                var path = Path.Combine(AppPaths.LogsDirectory, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
                if (File.Exists(path) && new FileInfo(path).Length > 5_000_000) return;
                File.AppendAllText(path, JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, version = UpdateService.CurrentVersion.ToString(), context, exception = error.ToString() }) + Environment.NewLine);
            }
        }
        catch { System.Diagnostics.Debug.WriteLine(error); }
    }
}
