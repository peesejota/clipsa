using Microsoft.Win32;

namespace Clipsa;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Clipsa";
    public static string? GetCurrent() { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(ValueName) as string; }
    public static void Restore(string? value) { using var key = Registry.CurrentUser.CreateSubKey(RunKey); if (value is null) key.DeleteValue(ValueName, false); else key.SetValue(ValueName, value); }

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null) return;
        if (enabled)
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path)) key.SetValue(ValueName, $"\"{path}\" --background");
        }
        else if (OwnsCommand(key.GetValue(ValueName) as string, Environment.ProcessPath)) key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
    public static bool OwnsCommand(string? command, string? executable) => !string.IsNullOrWhiteSpace(executable) &&
        new[] { $"\"{executable}\" --background", $"\"{executable}\"", executable }.Contains(command?.Trim(), StringComparer.OrdinalIgnoreCase);
}
