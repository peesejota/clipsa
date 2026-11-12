using System.Runtime.InteropServices;

namespace Clipsa;

public static class HotkeyService
{
    public static string? Validate(IEnumerable<HotkeySetting> shortcuts)
    {
        var keys = new HashSet<(uint, uint, uint)>();
        var registered = new List<int>();
        var id = 29000;
        try
        {
            foreach (var key in shortcuts)
            {
                if (key.Key == 0) continue;
                if (!SettingsStore.IsValid(key)) return "El atajo no es válido.";
                if (!keys.Add((key.Modifiers, key.Key, key.ChordKey))) return "Dos funciones no pueden utilizar el mismo atajo.";
                if (key.Key == 0x2C) continue; // Print Screen uses its scoped shortcut listener, not RegisterHotKey.
                if (!Native.RegisterHotKey(IntPtr.Zero, ++id, key.Modifiers | 0x4000, key.Key)) return $"El atajo {key.Display} ya está ocupado o reservado por Windows. Elige otro.";
                registered.Add(id);
            }
            return null;
        }
        finally { foreach (var registeredId in registered) Native.UnregisterHotKey(IntPtr.Zero, registeredId); }
    }
}
