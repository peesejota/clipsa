using System.IO;

namespace Clipsa;

public static class SessionRecovery
{
    public static IEnumerable<string> AbandonedSessions()
    {
        if (!Directory.Exists(AppPaths.SessionsDirectory)) yield break;
        foreach (var folder in Directory.EnumerateDirectories(AppPaths.SessionsDirectory))
        {
            if (folder == AppPaths.SessionFolder || !Guid.TryParseExact(Path.GetFileName(folder), "N", out _)) continue;
            var inactive = false;
            try { using var probe = new FileStream(Path.Combine(folder, ".active"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); inactive = true; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (inactive) yield return folder;
        }
    }

    public static List<string> Recover()
    {
        var recovered = new List<string>();
        AppPaths.InitializeSession();
        foreach (var folder in AbandonedSessions().ToArray())
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.png"))
                {
                    var destination = Path.Combine(AppPaths.SessionFolder, "Recuperada_" + Guid.NewGuid().ToString("N") + ".png");
                    File.Copy(file, destination); recovered.Add(destination);
                }
                Directory.Delete(folder, true);
            }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Recuperar sesión"); }
        }
        return recovered;
    }

    public static void DiscardAbandoned()
    {
        foreach (var folder in AbandonedSessions().ToArray())
            try { Directory.Delete(folder, true); } catch (Exception ex) { AppDiagnostics.Log(ex, "Limpiar sesión abandonada"); }
    }
}
