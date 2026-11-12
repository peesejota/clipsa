using System.Windows;
using System.Windows.Input;
namespace Clipsa;
public partial class RegionWindow : Window
{
    private readonly System.Drawing.Rectangle physicalBounds;
    private System.Windows.Point? start;
    public Rect? SelectedRectangle { get; private set; }
    public RegionWindow(string monitorDeviceName = "")
    {
        InitializeComponent();
        var screen = MonitorService.Resolve(monitorDeviceName);
        physicalBounds = screen.Bounds;
        var dip = MonitorService.Bounds(screen);
        Left = dip.Left; Top = dip.Top; Width = dip.Width; Height = dip.Height;
        SourceInitialized += (_, _) => MonitorService.PlaceWindow(this, physicalBounds);
        Loaded += (_, _) => MonitorService.PlaceWindow(this, physicalBounds);
    }
    private System.Windows.Point Clamp(System.Windows.Point p) => new(Math.Clamp(p.X, 0, ActualWidth), Math.Clamp(p.Y, 0, ActualHeight));
    private void Mouse_Down(object s, MouseButtonEventArgs e) { start = Clamp(e.GetPosition(this)); CaptureMouse(); }
    private void Mouse_Move(object s, System.Windows.Input.MouseEventArgs e) { if (start is not System.Windows.Point a || e.LeftButton != MouseButtonState.Pressed) return; var r = new Rect(a, Clamp(e.GetPosition(this))); System.Windows.Controls.Canvas.SetLeft(Selection, r.Left); System.Windows.Controls.Canvas.SetTop(Selection, r.Top); Selection.Width = r.Width; Selection.Height = r.Height; }
    private void Mouse_Up(object s, MouseButtonEventArgs e)
    {
        if (start is not System.Windows.Point a) return;
        var r = new Rect(PointToScreen(a), PointToScreen(Clamp(e.GetPosition(this))));
        var left = Math.Max(physicalBounds.Left, (int)Math.Floor(r.Left));
        var top = Math.Max(physicalBounds.Top, (int)Math.Floor(r.Top));
        var right = Math.Min(physicalBounds.Right, (int)Math.Ceiling(r.Right));
        var bottom = Math.Min(physicalBounds.Bottom, (int)Math.Ceiling(r.Bottom));
        ReleaseMouseCapture(); start = null;
        SelectedRectangle = right - left > 4 && bottom - top > 4 ? new Rect(left, top, right - left, bottom - top) : null;
        DialogResult = SelectedRectangle.HasValue;
    }
    private void Window_KeyDown(object s, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) { SelectedRectangle = null; ReleaseMouseCapture(); DialogResult = false; } }
}
