using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Clipsa;

internal sealed class ScrollDriver
{
    private readonly IntPtr window, content;
    private readonly Point point;
    private readonly uint process;
    private ScrollPattern? pattern;
    private bool searched;
    public bool AtEnd { get; private set; }
    private readonly CancellationToken cancellation;
    public ScrollDriver(IntPtr window, IntPtr content, Rectangle region, CancellationToken cancellation)
    {
        this.window = window; this.content = content;
        this.cancellation = cancellation;
        point = new Point(region.Left + region.Width / 2, region.Top + region.Height / 2);
        GetWindowThreadProcessId(window, out process);
    }
    public void Focus()
    {
        if (cancellation.IsCancellationRequested || !IsWindow(window)) return;
        SetForegroundWindow(window);
    }
    public void Step()
    {
        ValidateTarget();
        FindPattern();
        if (pattern is not null)
        {
            try { pattern.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement); AtEnd = pattern.Current.VerticalScrollPercent >= 99.9; return; }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException) { pattern = null; AtEnd = false; }
        }
        ForwardInitial(-30, 0);
    }
    private void ValidateTarget()
    {
        cancellation.ThrowIfCancellationRequested();
        GetWindowThreadProcessId(window, out var currentProcess);
        if (!IsWindow(window) || process != currentProcess) throw new InvalidOperationException("La ventana de captura ya no está disponible.");
        if (GetAncestor(GetForegroundWindow(), 2) != window) throw new InvalidOperationException("Has cambiado de ventana. El scroll guiado se ha pausado.");
    }
    public void ForwardInitial(int delta, int key)
    {
        ValidateTarget();
        if (key is 0x26 or 0x28 or 0x21 or 0x22)
        {
            var thread = GetWindowThreadProcessId(window, out _);
            var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
            var destination = GetGUIThreadInfo(thread, ref info) && GetAncestor(info.Focus, 2) == window ? info.Focus : content;
            var bits = 1u | MapVirtualKey((uint)key, 0) << 16 | 0x01000000u;
            PostMessage(destination, 0x100, new IntPtr(key), new IntPtr(unchecked((int)bits)));
            PostMessage(destination, 0x101, new IntPtr(key), new IntPtr(unchecked((int)(bits | 0xC0000000u))));
            return;
        }
        if (delta == 0) return;
        var coordinates = new IntPtr(unchecked((point.Y & 0xFFFF) << 16 | (point.X & 0xFFFF)));
        var wheel = new IntPtr(unchecked(delta << 16));
        if (SendMessageTimeout(content, 0x20A, wheel, coordinates, 2, 200, out _) == IntPtr.Zero)
            throw new InvalidOperationException("La aplicación no ha respondido al scroll guiado. Puedes seguir con el desplazamiento manual.");
    }
    private void FindPattern()
    {
        if (!searched)
        {
            searched = true;
            try
            {
                var element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));
                for (var depth = 0; element is not null && depth < 12 && element.Current.ProcessId == process; depth++)
                {
                    if (element.TryGetCurrentPattern(ScrollPattern.Pattern, out var candidate) && ((ScrollPattern)candidate).Current.VerticallyScrollable)
                    { pattern = (ScrollPattern)candidate; break; }
                    element = TreeWalker.ControlViewWalker.GetParent(element);
                }
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException) { pattern = null; }
        }
    }
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo { public uint Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint key, uint mode);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out UIntPtr result);
}
