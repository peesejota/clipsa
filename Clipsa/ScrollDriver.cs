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
    public bool AtEnd
    {
        get { try { return pattern is not null && pattern.Current.VerticalScrollPercent >= 99.9; } catch (Exception ex) when (ex is InvalidOperationException or COMException) { return false; } }
    }
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
        cancellation.ThrowIfCancellationRequested();
        GetWindowThreadProcessId(window, out var currentProcess);
        if (!IsWindow(window) || process != currentProcess) throw new InvalidOperationException("La ventana de captura ya no está disponible.");
        if (GetAncestor(GetForegroundWindow(), 2) != window) throw new InvalidOperationException("Has cambiado de ventana. El scroll guiado se ha pausado.");
        FindPattern();
        if (pattern is not null)
        {
            try { pattern.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement); return; }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException) { pattern = null; }
        }
        var coordinates = new IntPtr(unchecked((point.Y & 0xFFFF) << 16 | (point.X & 0xFFFF)));
        var wheel = new IntPtr(unchecked(-30 << 16));
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
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out UIntPtr result);
}
