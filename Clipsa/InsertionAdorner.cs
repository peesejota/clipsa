using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
namespace Clipsa;
public sealed class InsertionAdorner : Adorner
{
    public double X { get; set; }
    public bool Horizontal { get; set; }
    public InsertionAdorner(UIElement adorned) : base(adorned) { IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext drawingContext)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 140, 255)); var pen = new System.Windows.Media.Pen(brush, 3);
        var a = Horizontal ? new System.Windows.Point(8, X) : new System.Windows.Point(X, 8);
        var b = Horizontal ? new System.Windows.Point(AdornedElement.RenderSize.Width - 8, X) : new System.Windows.Point(X, AdornedElement.RenderSize.Height - 8);
        drawingContext.DrawLine(pen, a, b); drawingContext.DrawEllipse(brush, null, a, 5, 5); drawingContext.DrawEllipse(brush, null, b, 5, 5);
    }
}
