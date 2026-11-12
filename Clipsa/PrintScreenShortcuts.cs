using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Clipsa;

// Tracks only configured Print Screen gestures. Does not store or transmit typed text.
public sealed class PrintScreenChordTracker
{
    private readonly HotkeySetting[] bindings;
    private readonly HashSet<uint> swallowedUps = new();
    private bool armed;
    private bool pending;
    private long deadline;
    private readonly Func<long> clock;
    public const int SequenceTimeoutMilliseconds = 750;
    private uint modifiers, selected;
    public PrintScreenChordTracker(IEnumerable<HotkeySetting> shortcuts, Func<long>? clock = null)
    { bindings = shortcuts.ToArray(); this.clock = clock ?? (() => Environment.TickCount64); }
    public bool Expire(out HotkeySetting? match)
    {
        match = null;
        if (!pending || clock() < deadline) return false;
        pending = false;
        match = bindings.FirstOrDefault(b => b.Modifiers == modifiers && b.ChordKey == 0);
        return match is not null;
    }
    private void ReleasePrefix(uint currentModifiers, out HotkeySetting? match)
    {
        modifiers = currentModifiers; selected = 0; match = null;
        if (bindings.Any(b => b.Modifiers == modifiers && b.ChordKey != 0))
        { pending = true; deadline = clock() + SequenceTimeoutMilliseconds; }
        else match = bindings.FirstOrDefault(b => b.Modifiers == modifiers && b.ChordKey == 0);
    }
    public bool Process(uint key, bool down, uint currentModifiers, out HotkeySetting? match)
    {
        match = null;
        if (pending && clock() >= deadline) pending = false;
        if (key is >= 0x61 and <= 0x63) key -= 0x30; // numeric keypad
        if (!down && swallowedUps.Remove(key)) return true;
        if (down && swallowedUps.Contains(key)) return true;
        if (key == 0x2C)
        {
            if (down)
            {
                if (armed) return true; // no repeat
                if (!bindings.Any(b => b.Modifiers == currentModifiers)) return false;
                pending = false; armed = true; modifiers = currentModifiers; selected = 0; return true;
            }
            if (!armed)
            {
                // Laptop/Fn keys may emit a complete pulse or only a key-up.
                if (pending) return true;
                if (!bindings.Any(b => b.Modifiers == currentModifiers)) return false;
                ReleasePrefix(currentModifiers, out match);
                return true;
            }
            armed = false;
            if (selected == 0) { ReleasePrefix(modifiers, out match); return true; }
            match = bindings.FirstOrDefault(b => b.Modifiers == modifiers && b.ChordKey == selected);
            selected = 0; return true;
        }
        if (pending && down)
        {
            pending = false;
            match = bindings.FirstOrDefault(b => b.Modifiers == modifiers && currentModifiers == modifiers && b.ChordKey == key && key != 0);
            if (match is not null) { swallowedUps.Add(key); return true; }
        }
        if (!armed || !bindings.Any(b => b.Modifiers == modifiers && b.ChordKey == key && key != 0)) return false;
        if (down) { if (selected == 0) selected = key; swallowedUps.Add(key); }
        return true;
    }
}

public sealed class PrintScreenShortcuts : IDisposable
{
    private readonly Thread thread;
    private readonly HookProc callback;
    private readonly PrintScreenChordTracker tracker;
    private readonly Action<HotkeySetting> activate;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private uint threadId;
    private volatile bool disposed;
    private IntPtr hook;
    public PrintScreenShortcuts(IEnumerable<HotkeySetting> bindings, Action<HotkeySetting> activate)
    {
        tracker = new(bindings); this.activate = activate; callback = Handle;
        thread = new Thread(Run) { IsBackground = true, Name = "Clipsa Print Screen shortcuts" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try { ready.Task.GetAwaiter().GetResult(); }
        catch { Dispose(); throw; }
    }
    private void Run()
    {
        try
        {
            threadId = GetCurrentThreadId();
            // Create message queue before publishing readiness, so disposal cannot miss WM_QUIT.
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            using var expiry = new System.Windows.Forms.Timer { Interval = 25 };
            expiry.Tick += (_, _) => { if (!disposed && tracker.Expire(out var match) && match is not null) activate(match); };
            expiry.Start();
            ready.TrySetResult();
            System.Windows.Forms.Application.Run();
        }
        catch (Exception ex) { if (!ready.TrySetException(ex)) AppDiagnostics.Log(ex, "Atajo Impr Pant"); }
        finally { if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook); }
    }
    private IntPtr Handle(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && !disposed && message.ToInt32() is 0x100 or 0x101 or 0x104 or 0x105)
        {
            var key = (uint)Marshal.ReadInt32(data);
            if (tracker.Expire(out var expired) && expired is not null) activate(expired);
            var down = message.ToInt32() is 0x100 or 0x104;
            uint modifiers = 0;
            if ((GetAsyncKeyState(0x12) & 0x8000) != 0) modifiers |= 1;
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0) modifiers |= 2;
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) modifiers |= 4;
            if (((GetAsyncKeyState(0x5B) | GetAsyncKeyState(0x5C)) & 0x8000) != 0) modifiers |= 8;
            if (tracker.Process(key, down, modifiers, out var match))
            {
                if (match is not null) activate(match); // callers queue work to their dispatcher
                return new IntPtr(1);
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (threadId != 0) PostThreadMessage(threadId, 0x12, IntPtr.Zero, IntPtr.Zero);
        if (Thread.CurrentThread != thread) thread.Join(500);
    }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Hwnd; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
