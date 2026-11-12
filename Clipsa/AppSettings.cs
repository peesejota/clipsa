using System.Text.Json;
using System.IO;
using System.Windows.Input;

namespace Clipsa;

public sealed class HotkeySetting
{
    public uint Modifiers { get; set; } = 0x0002 | 0x0001;
    public uint Key { get; set; } = 0x54;
    public uint ChordKey { get; set; }
    public string Display { get; set; } = "Ctrl + Alt + T";
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 2;
    public HotkeySetting Full { get; set; } = new();
    public HotkeySetting Region { get; set; } = new() { Key = 0x52, Display = "Ctrl + Alt + R" };
    public HotkeySetting Scroll { get; set; } = new() { Key = 0x53, Display = "Ctrl + Alt + S" };
    public int ScrollSteps { get; set; } = 5;
    public int ScrollDelay { get; set; } = 250;
    public double ScrollOverlap { get; set; } = .72;
    public double PanelScale { get; set; } = 1.0;
    public int GlassOpacity { get; set; } = 68;
    public string SaveFolder { get; set; } = AppPaths.IsPortable ? "." : AppPaths.DefaultSaveFolder;
    public bool KeepInTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool RecoverAfterCrash { get; set; }
    public string CaptureMonitor { get; set; } = "";
    public string UpdateFeedUrl { get; set; } = UpdateService.DefaultFeed;
    public bool CheckUpdatesOnStartup { get; set; } = true;
}

public static class SettingsStore
{
    public static string? LastLoadNotice { get; private set; }
    public static bool UsedFallbackDefaults { get; private set; }
    public static AppSettings Load()
    {
        LastLoadNotice = null;
        UsedFallbackDefaults = true;
        foreach (var file in new[] { AppPaths.SettingsFile, AppPaths.SettingsFile + ".bak" })
        {
            if (!File.Exists(file)) continue;
            try
            {
                var value = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? throw new JsonException("Los ajustes contienen un valor nulo.");
                Normalize(value);
                UsedFallbackDefaults = false;
                if (file.EndsWith(".bak")) LastLoadNotice = "Se recuperaron los ajustes desde su copia de seguridad.";
                return value;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
            {
                AppDiagnostics.Log(ex, "Leer ajustes");
                LastLoadNotice = "No se pudieron leer los ajustes anteriores. Comprueba la carpeta de ajustes; se usarán valores seguros.";
            }
        }
        return new();
    }

    private static void Normalize(AppSettings value)
    {
        value.Full ??= new(); value.Region ??= new() { Key = 0x52, Display = "Ctrl + Alt + R" }; value.Scroll ??= new() { Key = 0x53, Display = "Ctrl + Alt + S" };
        value.PanelScale = double.IsFinite(value.PanelScale) ? Math.Clamp(value.PanelScale, .7, 1.4) : 1;
        value.GlassOpacity = Math.Clamp(value.GlassOpacity, 0, 100);
        value.ScrollDelay = Math.Clamp(value.ScrollDelay, 100, 400);
        value.CaptureMonitor ??= ""; value.UpdateFeedUrl ??= "";
        if (string.IsNullOrWhiteSpace(value.UpdateFeedUrl)) { value.UpdateFeedUrl = UpdateService.DefaultFeed; if (value.SchemaVersion < 2) value.CheckUpdatesOnStartup = true; }
        if (string.IsNullOrWhiteSpace(value.SaveFolder)) value.SaveFolder = AppPaths.IsPortable ? "." : AppPaths.DefaultSaveFolder;
        if (AppPaths.IsPortable && Path.IsPathFullyQualified(value.SaveFolder) && File.Exists(Path.Combine(value.SaveFolder, "portable.mode"))) value.SaveFolder = ".";
        if (!Path.IsPathFullyQualified(value.SaveFolder) && !AppPaths.IsPortable) value.SaveFolder = AppPaths.DefaultSaveFolder;
        value.SchemaVersion = 2;
        var keys = new HashSet<(uint, uint, uint)>();
        foreach (var hotkey in new[] { value.Full, value.Region, value.Scroll })
        {
            if (!IsValid(hotkey) || !keys.Add((hotkey.Modifiers, hotkey.Key, hotkey.ChordKey))) { hotkey.Key = 0; hotkey.ChordKey = 0; hotkey.Display = "Sin asignar"; }
            else hotkey.Display = DescribeVirtual(hotkey);
        }
    }

    public static bool IsValid(HotkeySetting key) => key.Key is > 0 and <= 254 && (key.Modifiers & ~15u) == 0 &&
        (key.ChordKey == 0 || key.Key == 0x2C && key.ChordKey is >= 0x31 and <= 0x33);
    public static string DescribeVirtual(HotkeySetting key) => Describe(KeyInterop.KeyFromVirtualKey((int)key.Key),
        ToModifiers(key.Modifiers)) + (key.ChordKey == 0 ? "" : " + " + (key.ChordKey - 0x30));
    private static ModifierKeys ToModifiers(uint modifiers) => (ModifierKeys)(((modifiers & 1) != 0 ? (int)ModifierKeys.Alt : 0) |
        ((modifiers & 2) != 0 ? (int)ModifierKeys.Control : 0) | ((modifiers & 4) != 0 ? (int)ModifierKeys.Shift : 0) | ((modifiers & 8) != 0 ? (int)ModifierKeys.Windows : 0));

    public static void Save(AppSettings value)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var temp = AppPaths.SettingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(AppPaths.SettingsFile)) File.Replace(temp, AppPaths.SettingsFile, AppPaths.SettingsFile + ".bak");
            else File.Move(temp, AppPaths.SettingsFile);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static string Describe(Key key, ModifierKeys modifiers) => string.Join(" + ", new[] {
        modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl" : "", modifiers.HasFlag(ModifierKeys.Alt) ? "Alt" : "",
        modifiers.HasFlag(ModifierKeys.Shift) ? "Shift" : "", modifiers.HasFlag(ModifierKeys.Windows) ? "Win" : "", key == Key.Snapshot ? "Impr Pant" : key.ToString() }.Where(x => x.Length > 0));
    public static uint ModifierValue(ModifierKeys modifiers) => (uint)((modifiers.HasFlag(ModifierKeys.Alt) ? 1 : 0) | (modifiers.HasFlag(ModifierKeys.Control) ? 2 : 0) | (modifiers.HasFlag(ModifierKeys.Shift) ? 4 : 0) | (modifiers.HasFlag(ModifierKeys.Windows) ? 8 : 0));
}
