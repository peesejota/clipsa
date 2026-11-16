using System.Windows;
using System.Windows.Input;
namespace Clipsa;
public partial class RegionWindow : Window
{
    private readonly System.Drawing.Rectangle physicalBounds;
    private System.Windows.Point? start;
    private CaptureCancellation? cancelInput;
    private readonly bool scrollGesture;
    private bool excludedFromCapture;
    private System.Drawing.Bitmap? scrollBaseline;
    internal System.Drawing.Bitmap? TakeScrollBaseline() { var frame = scrollBaseline; scrollBaseline = null; return frame; }
    public bool ScrollWhileHeld { get; private set; }
    public int InitialWheelDelta { get; private set; }
    public int InitialScrollKey { get; private set; }
    public Rect? SelectedRectangle { get; private set; }
    public RegionWindow(string monitorDeviceName = "", bool scrollGesture = false)
    {
        InitializeComponent();
        this.scrollGesture = scrollGesture;
        if (scrollGesture) SelectionHint.Text = "Arrastra y suelta: scroll automático · O desplaza sin soltar para el modo manual · Esc cancela";
        PreviewMouseWheel += (_, e) =>
        {
            if (!scrollGesture || start is null || e.LeftButton != MouseButtonState.Pressed) return;
            if (!CompleteSelection(e.GetPosition(this), true)) return;
            InitialWheelDelta = e.Delta; e.Handled = true;
        };
        var screen = MonitorService.Resolve(monitorDeviceName);
        physicalBounds = screen.Bounds;
        var dip = MonitorService.Bounds(screen);
        Left = dip.Left; Top = dip.Top; Width = dip.Width; Height = dip.Height;
        SourceInitialized += (_, _) => MonitorService.PlaceWindow(this, physicalBounds);
        Loaded += (_, _) =>
        {
            MonitorService.PlaceWindow(this, physicalBounds);
            if (scrollGesture) excludedFromCapture = Native.SetWindowDisplayAffinity(new System.Windows.Interop.WindowInteropHelper(this).Handle, 0x11);
            try { cancelInput = new CaptureCancellation(() => Dispatcher.BeginInvoke(Cancel)); }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Activar cancelación de selección"); }
        };
        PreviewMouseRightButtonUp += (_, e) => { e.Handled = true; Cancel(); };
        Closed += (_, _) => { cancelInput?.Dispose(); cancelInput = null; ReleaseMouseCapture(); };
    }
    private System.Windows.Point Clamp(System.Windows.Point p) => new(Math.Clamp(p.X, 0, ActualWidth), Math.Clamp(p.Y, 0, ActualHeight));
    private void Mouse_Down(object s, MouseButtonEventArgs e) { start = Clamp(e.GetPosition(this)); CaptureMouse(); }
    private void Mouse_Move(object s, System.Windows.Input.MouseEventArgs e) { if (start is not System.Windows.Point a || e.LeftButton != MouseButtonState.Pressed) return; var r = new Rect(a, Clamp(e.GetPosition(this))); System.Windows.Controls.Canvas.SetLeft(Selection, r.Left); System.Windows.Controls.Canvas.SetTop(Selection, r.Top); Selection.Width = r.Width; Selection.Height = r.Height; }
    private void Mouse_Up(object s, MouseButtonEventArgs e)
    {
        CompleteSelection(e.GetPosition(this), false);
    }
    private bool CompleteSelection(System.Windows.Point end, bool held)
    {
        if (start is not System.Windows.Point a) return false;
        var r = new Rect(PointToScreen(a), PointToScreen(Clamp(end)));
        var left = Math.Max(physicalBounds.Left, (int)Math.Floor(r.Left));
        var top = Math.Max(physicalBounds.Top, (int)Math.Floor(r.Top));
        var right = Math.Min(physicalBounds.Right, (int)Math.Ceiling(r.Right));
        var bottom = Math.Min(physicalBounds.Bottom, (int)Math.Ceiling(r.Bottom));
        if (held && (right - left <= 40 || bottom - top <= 80)) return false;
        if (held && excludedFromCapture)
        {
            try
            {
                Native.DwmFlush();
                scrollBaseline = Capture.DesktopCapture.Capture(new System.Drawing.Rectangle(left, top, right - left, bottom - top));
            }
            catch (Exception ex) { scrollBaseline?.Dispose(); scrollBaseline = null; AppDiagnostics.Log(ex, "Guardar inicio de scroll antes de cerrar selección"); }
        }
        ReleaseMouseCapture(); start = null;
        SelectedRectangle = right - left > 4 && bottom - top > 4 ? new Rect(left, top, right - left, bottom - top) : null;
        ScrollWhileHeld = held;
        DialogResult = SelectedRectangle.HasValue;
        return SelectedRectangle.HasValue;
    }
    private void Cancel()
    {
        if (!IsVisible) return;
        start = null; SelectedRectangle = null; ReleaseMouseCapture(); DialogResult = false;
    }
    private void Window_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Cancel(); return; }
        if (!scrollGesture || start is null || Mouse.LeftButton != MouseButtonState.Pressed || e.Key is not (Key.Down or Key.Up or Key.PageDown or Key.PageUp)) return;
        var key = KeyInterop.VirtualKeyFromKey(e.Key);
        if (CompleteSelection(Mouse.GetPosition(this), true)) { InitialScrollKey = key; e.Handled = true; }
    }
}
