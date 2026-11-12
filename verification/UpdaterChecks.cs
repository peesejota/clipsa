using System.IO.Compression;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Clipsa;

internal static class UpdaterChecks
{
    public static void Run()
    {
        var signed = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "signed-update.json"));
        var installed = UpdateService.ParseManifest(signed, false); var portable = UpdateService.ParseManifest(signed, true);
        Require(installed.SignatureVerified && portable.SignatureVerified && installed.DownloadUrl.EndsWith("installer.exe") && portable.DownloadUrl.EndsWith("portable.zip"), "trusted assets selected by mode");
        var changed = JsonSerializer.Deserialize<Dictionary<string, string>>(signed)!;
        changed["payload"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
        Reject(() => UpdateService.ParseManifest(JsonSerializer.Serialize(changed), false), "tampered signature");
        var unsigned = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "update-payload.json"));
        Require(!UpdateService.ParseManifest(unsigned, false).SignatureVerified, "unsigned feed cannot claim verified");
        var startup = typeof(AppSettings).Assembly.GetType("Clipsa.StartupManager")!.GetMethod("OwnsCommand", BindingFlags.Public | BindingFlags.Static)!;
        Require((bool)startup.Invoke(null, new object?[] { "\"C:\\Installed\\Clipsa.exe\" --background", "C:\\Installed\\Clipsa.exe" })!, "own startup recognized");
        Require(!(bool)startup.Invoke(null, new object?[] { "\"C:\\Portable\\Clipsa.exe\" --background", "C:\\Installed\\Clipsa.exe" })!, "foreign startup preserved");
        var root = Path.Combine(Path.GetTempPath(), "Clipsa-updater-checks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Require(AppPaths.MakePortableSavePath(root, root) == ".", "portable default folder stored relatively");
        Require(AppPaths.MakePortableSavePath(root, Path.Combine(root, "Capturas")) == "Capturas", "portable child folder stored relatively");
        var external = Path.GetFullPath(Path.Combine(root, "..", "external-images"));
        Require(AppPaths.MakePortableSavePath(root, external) == external, "explicit external folder remains absolute");
        foreach (var path in new[] { "../escape", "data/settings.json", "C:\\escape", "file:stream", "folder/../escape" }) Reject(() => UpdateInstaller.SafePath(root, path), "unsafe package path " + path);
        var target = Path.Combine(root, "portable"); Directory.CreateDirectory(Path.Combine(target, "data"));
        File.WriteAllText(Path.Combine(target, "data", "settings.json"), "user settings"); File.WriteAllText(Path.Combine(target, "captura.png"), "user screenshot"); File.WriteAllText(Path.Combine(target, "Clipsa.exe"), "old program");
        var zip = Package(root, "good.zip", false);
        UpdateInstaller.ApplyPortable(zip, target, Path.Combine(root, "success"));
        Require(File.ReadAllText(Path.Combine(target, "Clipsa.exe")) == "new program", "program replaced");
        Require(File.ReadAllText(Path.Combine(target, "data", "settings.json")) == "user settings" && File.ReadAllText(Path.Combine(target, "captura.png")) == "user screenshot", "portable data and exports preserved");
        File.WriteAllText(Path.Combine(target, "Clipsa.exe"), "original before failed update");
        var broken = Package(root, "broken.zip", true);
        Reject(() => UpdateInstaller.ApplyPortable(broken, target, Path.Combine(root, "rollback")), "incomplete update rejected");
        Require(File.ReadAllText(Path.Combine(target, "Clipsa.exe")) == "original before failed update", "transaction rollback restores old program");
        Reject(() => UpdateInstaller.VerifyHash(zip, new string('0', 64)), "corrupt package rejected");
        Console.WriteLine("Pinned signatures, installer/portable selection, unsafe ZIP paths, SHA256, startup ownership, portable data preservation and rollback: OK");
    }
    private static string Package(string root, string name, bool broken)
    {
        var path = Path.Combine(root, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var files = new[] { "Clipsa.exe", "Clipsa.dll", "coreclr.dll", "Clipsa.runtimeconfig.json" };
        foreach (var file in files) { using var writer = new StreamWriter(zip.CreateEntry(file).Open()); writer.Write(file == "Clipsa.exe" ? "new program" : "test runtime"); }
        using (var writer = new StreamWriter(zip.CreateEntry("portable.mode").Open())) writer.Write("");
        using (var writer = new StreamWriter(zip.CreateEntry("app-files.json").Open())) writer.Write(JsonSerializer.Serialize(broken ? files.Append("missing.dll") : files));
        return path;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string message) { try { action(); } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or System.Security.Cryptography.CryptographicException) { return; } throw new Exception(message); }
}
