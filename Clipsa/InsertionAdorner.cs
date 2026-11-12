using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
namespace Clipsa;
public sealed class InsertionAdorner : Adorner
{
    public double X { get; set; }
    public InsertionAdorner(UIElement adorned) : base(adorned) { IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext drawingContext) { var pen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 140, 255)), 3); drawingContext.DrawLine(pen, new System.Windows.Point(X, 8), new System.Windows.Point(X, AdornedElement.RenderSize.Height - 8)); drawingContext.DrawEllipse(new SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 140, 255)), null, new System.Windows.Point(X, 8), 5, 5); drawingContext.DrawEllipse(new SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 140, 255)), null, new System.Windows.Point(X, AdornedElement.RenderSize.Height - 8), 5, 5); }
}
