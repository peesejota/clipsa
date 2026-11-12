using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Clipsa;

// Installed only while a region/scroll capture is active. Only Escape and the
// right button are consumed; no typed text or pointer positions are recorded.
internal sealed class CaptureCancellation : IDisposable
{
    private readonly Thread thread;
    private readonly HookProc keyboardCallback, mouseCallback;
    private readonly Action cancel;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IntPtr keyboardHook, mouseHook;
    private uint threadId;
    private volatile bool disposed;
    private volatile bool escapeHeld, rightHeld;
    private long drainDeadline;
    private bool requested;

    public CaptureCancellation(Action cancel)
    {
        this.cancel = cancel; keyboardCallback = Keyboard; mouseCallback = Mouse;
        thread = new Thread(Run) { IsBackground = true, Name = "Clipsa capture cancellation" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try { ready.Task.GetAwaiter().GetResult(); }
        catch { Dispose(); throw; }
    }
    private void Run()
    {
        try
        {
            threadId = GetCurrentThreadId(); PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            keyboardHook = SetWindowsHookEx(13, keyboardCallback, GetModuleHandle(null), 0);
            if (keyboardHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            mouseHook = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
            if (mouseHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            using var drain = new System.Windows.Forms.Timer { Interval = 25 };
            drain.Tick += (_, _) =>
            {
                if (disposed && ((!escapeHeld && !rightHeld) || Environment.TickCount64 >= drainDeadline))
                    PostThreadMessage(threadId, 0x12, IntPtr.Zero, IntPtr.Zero);
            };
            drain.Start();
            ready.TrySetResult(); System.Windows.Forms.Application.Run();
        }
        catch (Exception ex) { if (!ready.TrySetException(ex)) AppDiagnostics.Log(ex, "Cancelación de captura"); }
        finally
        {
            if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
            if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
        }
    }
    private void Request()
    {
        if (requested) return;
        requested = true; cancel(); // caller queues cancellation to its UI dispatcher
    }
    private IntPtr Keyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && (message.ToInt32() is 0x100 or 0x101 or 0x104 or 0x105) && Marshal.ReadInt32(data) == 0x1B)
        {
            if (!disposed && !requested && (message.ToInt32() is 0x100 or 0x104))
            { escapeHeld = true; Request(); return new IntPtr(1); }
            if (escapeHeld)
            {
                if (message.ToInt32() is 0x101 or 0x105) escapeHeld = false;
                return new IntPtr(1);
            }
        }
        return CallNextHookEx(keyboardHook, code, message, data);
    }
    private IntPtr Mouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && (message.ToInt32() is 0x204 or 0x205))
        {
            if (!disposed && !requested && message.ToInt32() == 0x204)
            { rightHeld = true; Request(); return new IntPtr(1); }
            if (rightHeld)
            {
                if (message.ToInt32() == 0x205) rightHeld = false;
                return new IntPtr(1);
            }
        }
        return CallNextHookEx(mouseHook, code, message, data);
    }
    public void Dispose()
    {
        if (disposed) return;
        // Cancel on DOWN immediately, but retain only that gesture's release
        // after the window closes. Never wait for a held key on the UI thread.
        drainDeadline = Environment.TickCount64 + 2000;
        disposed = true;
        if (escapeHeld || rightHeld) return;
        if (threadId != 0) PostThreadMessage(threadId, 0x12, IntPtr.Zero, IntPtr.Zero);
        if (Thread.CurrentThread != thread) thread.Join(500);
    }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Hwnd; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
