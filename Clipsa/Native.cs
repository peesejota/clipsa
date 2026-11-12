using System.Runtime.InteropServices;
namespace Clipsa;
internal static class Native
{
    public const int WM_HOTKEY = 0x0312, MOD_ALT = 0x0001, MOD_CONTROL = 0x0002;
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
}
