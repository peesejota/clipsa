using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using WpfCanvas = System.Windows.Controls.Canvas;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using Cursors = System.Windows.Input.Cursors;
using PathShape = System.Windows.Shapes.Path;
using FontFamily = System.Windows.Media.FontFamily;

namespace Clipsa;

public partial class AnnotationWindow : Window
{
    private readonly string filePath;
    private Point last;
    private bool drawing;
    private string tool = "Pen";
    private System.Windows.Media.Brush brush = System.Windows.Media.Brushes.Red;
    private Shape? current;
    private Rect crop = Rect.Empty;
    private sealed record EditorState(BitmapSource Image, string[] Annotations, Rect Crop, bool CropVisible, int Ratio);
    private readonly List<EditorState> undoHistory = new();
    private readonly Stack<EditorState> redoHistory = new();
    private EditorState? pendingState;
    private bool restoring;
    private readonly bool promptOnDiscard;
    private bool allowDiscard;
    private EditorState? initialState;

    public AnnotationWindow(string path, bool promptOnDiscard = true)
    {
        InitializeComponent(); filePath = path; this.promptOnDiscard = promptOnDiscard;
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
        SetImage(image);
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (_, _) => UndoEdit(), (_, e) => e.CanExecute = undoHistory.Count > 0));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (_, _) => RedoEdit(), (_, e) => e.CanExecute = redoHistory.Count > 0));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Undo, Key.Z, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Redo, Key.Y, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));
        UpdateHistoryButtons();
        initialState = CaptureState();
        Closing += (_, e) =>
        {
            if (!this.promptOnDiscard || allowDiscard || DialogResult == true || initialState is null) return;
            FinishGesture(); var now = CaptureState();
            if (ReferenceEquals(now.Image, initialState.Image) && now.Crop == initialState.Crop && now.Annotations.SequenceEqual(initialState.Annotations)) return;
            var answer = System.Windows.MessageBox.Show(this, "Hay cambios sin guardar. ¿Guardarlos antes de cerrar?", "Clipsa", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) e.Cancel = true;
            else if (answer == MessageBoxResult.Yes) { e.Cancel = true; Dispatcher.BeginInvoke(() => Save_Click(this, new RoutedEventArgs())); }
        };
    }

    private void SetImage(BitmapSource image)
    {
        Screenshot.Source = image; DrawingSurface.Width = image.PixelWidth; DrawingSurface.Height = image.PixelHeight;
        Ink.Children.Clear(); crop = Rect.Empty; CropSelection.Visibility = Visibility.Collapsed;
        UpdateDimensions();
    }

    private EditorState CaptureState() => new((BitmapSource)Screenshot.Source,
        Ink.Children.Cast<UIElement>().Select(XamlWriter.Save).ToArray(), crop,
        CropSelection.Visibility == Visibility.Visible, CropRatio.SelectedIndex);

    private void RestoreState(EditorState state)
    {
        restoring = true;
        try
        {
            SetImage(state.Image);
            foreach (var element in state.Annotations) Ink.Children.Add((UIElement)XamlReader.Parse(element));
            crop = state.Crop; CropRatio.SelectedIndex = state.Ratio;
            if (!crop.IsEmpty) SetRect(CropSelection, crop);
            CropSelection.Visibility = state.CropVisible && !crop.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            UpdateDimensions();
        }
        finally { restoring = false; }
    }

    private void CommitState(EditorState before)
    {
        var after = CaptureState();
        if (ReferenceEquals(before.Image, after.Image) && before.Crop == after.Crop &&
            before.Ratio == after.Ratio && before.Annotations.SequenceEqual(after.Annotations)) return;
        undoHistory.Add(before);
        if (undoHistory.Count > 60) undoHistory.RemoveAt(0);
        while (undoHistory.Count > 1 && HistoryImageBytes() > 256L * 1024 * 1024) undoHistory.RemoveAt(0);
        redoHistory.Clear(); UpdateHistoryButtons();
    }

    private long HistoryImageBytes()
    {
        var images = new HashSet<BitmapSource>(ReferenceEqualityComparer.Instance);
        long total = 0;
        foreach (var state in undoHistory) if (images.Add(state.Image)) total += (long)state.Image.PixelWidth * state.Image.PixelHeight * 4;
        return total;
    }

    private void FinishGesture()
    {
        if (pendingState is null) return;
        var before = pendingState; pendingState = null; drawing = false; current = null;
        DrawingSurface.ReleaseMouseCapture(); CommitState(before);
    }

    private void UndoEdit()
    {
        FinishGesture();
        if (undoHistory.Count == 0) return;
        redoHistory.Push(CaptureState());
        var state = undoHistory[^1]; undoHistory.RemoveAt(undoHistory.Count - 1);
        RestoreState(state); UpdateHistoryButtons();
    }

    private void RedoEdit()
    {
        FinishGesture();
        if (redoHistory.Count == 0) return;
        undoHistory.Add(CaptureState()); RestoreState(redoHistory.Pop()); UpdateHistoryButtons();
    }

    private void UpdateHistoryButtons()
    {
        UndoButton.IsEnabled = undoHistory.Count > 0; RedoButton.IsEnabled = redoHistory.Count > 0;
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateDimensions() => CropSizeText.Text = !crop.IsEmpty && crop.Width >= 1 && crop.Height >= 1
        ? $"{(int)crop.Width} × {(int)crop.Height} px · selección"
        : $"{(int)DrawingSurface.Width} × {(int)DrawingSurface.Height} px";

    private void Tool_Click(object s, RoutedEventArgs e)
    {
        FinishGesture();
        if (s is FrameworkElement element && element.Tag is string selected)
        {
            tool = selected;
            DrawingSurface.Cursor = tool == "Crop" ? Cursors.Cross : tool == "Text" ? Cursors.IBeam : Cursors.Pen;
            CropSelection.Visibility = tool == "Crop" && !crop.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            ToolStatus.Text = tool switch { "Pen" => "Lápiz", "Highlight" => "Resaltador", "Crop" => "Recorte", "Eraser" => "Goma", "Text" => "Texto", "Rectangle" => "Rectángulo", "Ellipse" => "Círculo", _ => "Flecha" };
        }
    }

    private void Ratio_Changed(object s, SelectionChangedEventArgs e)
    {
        if (DrawingSurface is null || Screenshot.Source is null || restoring) return;
        FinishGesture();
        var before = CaptureState();
        // The selection event has already changed the combo; keep the previous ratio for undo.
        if (e.RemovedItems.Count > 0) before = before with { Ratio = CropRatio.Items.IndexOf(e.RemovedItems[0]) };
        tool = "Crop"; crop = Rect.Empty; CropSelection.Visibility = Visibility.Collapsed;
        DrawingSurface.Cursor = Cursors.Cross; UpdateDimensions(); CommitState(before);
    }

    private void Color_Click(object s, RoutedEventArgs e)
    {
        using var picker = new Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.Red };
        if (picker.ShowDialog() == Forms.DialogResult.OK)
        {
            var c = picker.Color; brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B));
            ColorButton.Background = brush;
        }
    }

    private Point SurfacePoint(System.Windows.Input.MouseEventArgs e)
    {
        var p = e.GetPosition(DrawingSurface);
        return new Point(Math.Clamp(p.X, 0, DrawingSurface.Width), Math.Clamp(p.Y, 0, DrawingSurface.Height));
    }

    private void DrawStart(object s, MouseButtonEventArgs e)
    {
        FinishGesture(); DrawingSurface.Focus(); last = SurfacePoint(e); e.Handled = true;
        if (tool == "Text") { InsertText(last); return; }
        pendingState = CaptureState(); drawing = true; DrawingSurface.CaptureMouse(); current = null;
        if (tool == "Crop") { crop = new Rect(last, last); CropSelection.Visibility = Visibility.Visible; SetRect(CropSelection, crop); return; }
        if (tool == "Eraser") { EraseAt(last); return; }
        if (tool is "Pen" or "Highlight") { current = NewStroke(last); Ink.Children.Add(current); }
    }

    private void DrawMove(object s, System.Windows.Input.MouseEventArgs e)
    {
        if (!drawing) return;
        var now = SurfacePoint(e);
        if (tool == "Eraser")
        {
            var count = Math.Max(1, (int)Math.Ceiling((now - last).Length / Math.Max(2, Thickness.Value / 2)));
            for (var i = 1; i <= count; i++) EraseAt(last + (now - last) * ((double)i / count));
            last = now; return;
        }
        if (current is Polyline stroke && tool is "Pen" or "Highlight") { stroke.Points.Add(now); return; }
        if (tool == "Crop")
        {
            var ratio = double.Parse((string)((ComboBoxItem)CropRatio.SelectedItem).Tag, System.Globalization.CultureInfo.InvariantCulture);
            crop = CropGeometry.Select(last, now, new Size(DrawingSurface.Width, DrawingSurface.Height), ratio);
            SetRect(CropSelection, crop); UpdateDimensions(); return;
        }
        if (current is not null) Ink.Children.Remove(current);
        var bounds = new Rect(last, now);
        current = tool switch { "Rectangle" => NewRectangle(bounds), "Ellipse" => NewEllipse(bounds), "Arrow" => NewArrow(last, now), _ => null };
        if (current is null) return;
        if (current is not Polyline) { WpfCanvas.SetLeft(current, bounds.Left); WpfCanvas.SetTop(current, bounds.Top); }
        Ink.Children.Add(current);
    }

    private void DrawEnd(object s, MouseButtonEventArgs e) { if (drawing) DrawMove(s, e); FinishGesture(); }
    private void DrawLostCapture(object s, System.Windows.Input.MouseEventArgs e) => FinishGesture();

    private Polyline NewStroke(Point first) => new() { Points = new PointCollection { first }, Stroke = brush,
        Opacity = tool == "Highlight" ? .35 : 1, StrokeThickness = tool == "Highlight" ? Math.Max(12, Thickness.Value * 2) : Thickness.Value,
        StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };

    private Polyline NewArrow(Point a, Point b)
    {
        var angle = Math.Atan2(b.Y - a.Y, b.X - a.X); var size = 12 + Thickness.Value;
        var left = new Point(b.X - size * Math.Cos(angle - .55), b.Y - size * Math.Sin(angle - .55));
        var right = new Point(b.X - size * Math.Cos(angle + .55), b.Y - size * Math.Sin(angle + .55));
        return new Polyline { Points = new PointCollection { a, b, left, b, right }, Stroke = brush, StrokeThickness = Thickness.Value, StrokeLineJoin = PenLineJoin.Round };
    }

    private System.Windows.Shapes.Rectangle NewRectangle(Rect r) => new() { Width = r.Width, Height = r.Height, Stroke = brush, StrokeThickness = Thickness.Value, Fill = System.Windows.Media.Brushes.Transparent };
    private Ellipse NewEllipse(Rect r) => new() { Width = r.Width, Height = r.Height, Stroke = brush, StrokeThickness = Thickness.Value, Fill = System.Windows.Media.Brushes.Transparent };
    private static void SetRect(FrameworkElement item, Rect r) { WpfCanvas.SetLeft(item, r.Left); WpfCanvas.SetTop(item, r.Top); item.Width = r.Width; item.Height = r.Height; }

    private void EraseAt(Point point)
    {
        Ink.UpdateLayout();
        var radius = Math.Max(4, Thickness.Value);
        for (var i = Ink.Children.Count - 1; i >= 0; i--)
        {
            if (Ink.Children[i] is TextBlock label)
            {
                var text = new FormattedText(label.Text, System.Globalization.CultureInfo.CurrentCulture, label.FlowDirection,
                    new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch), label.FontSize, label.Foreground, VisualTreeHelper.GetDpi(label).PixelsPerDip)
                    { MaxTextWidth = Math.Max(1, label.MaxWidth) };
                var geometry = text.BuildGeometry(new Point());
                var lx = WpfCanvas.GetLeft(label); var ly = WpfCanvas.GetTop(label);
                var cut = new EllipseGeometry(new Point(point.X - lx, point.Y - ly), radius, radius);
                if (geometry.FillContainsWithDetail(cut) == IntersectionDetail.Empty) continue;
                var remainingText = Geometry.Combine(geometry, cut, GeometryCombineMode.Exclude, null);
                var replacementText = new PathShape { Data = remainingText, Fill = label.Foreground, Stretch = Stretch.None };
                WpfCanvas.SetLeft(replacementText, lx); WpfCanvas.SetTop(replacementText, ly);
                Ink.Children.RemoveAt(i); if (!remainingText.Bounds.IsEmpty) Ink.Children.Insert(i, replacementText); continue;
            }
            if (Ink.Children[i] is not Shape shape) continue;
            var x = WpfCanvas.GetLeft(shape); var y = WpfCanvas.GetTop(shape);
            if (double.IsNaN(x)) x = 0; if (double.IsNaN(y)) y = 0;
            Geometry painted;
            System.Windows.Media.Brush? paint;
            if (shape.Stroke is not null && shape.StrokeThickness > 0)
            {
                var pen = new System.Windows.Media.Pen(shape.Stroke, shape.StrokeThickness) { StartLineCap = shape.StrokeStartLineCap, EndLineCap = shape.StrokeEndLineCap, LineJoin = shape.StrokeLineJoin };
                painted = shape.RenderedGeometry.GetWidenedPathGeometry(pen); paint = shape.Stroke;
            }
            else { painted = shape.RenderedGeometry; paint = shape.Fill; }
            var eraser = new EllipseGeometry(new Point(point.X - x, point.Y - y), radius, radius);
            if (painted.FillContainsWithDetail(eraser) == IntersectionDetail.Empty) continue;
            var remaining = Geometry.Combine(painted, eraser, GeometryCombineMode.Exclude, null);
            var replacement = new PathShape { Data = remaining, Fill = paint, Opacity = shape.Opacity, Stretch = Stretch.None };
            WpfCanvas.SetLeft(replacement, x); WpfCanvas.SetTop(replacement, y);
            Ink.Children.RemoveAt(i);
            if (!remaining.Bounds.IsEmpty) Ink.Children.Insert(i, replacement);
        }
    }

    private void InsertText(Point position)
    {
        var dialog = new TextEntryWindow { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
        var size = int.TryParse(TextSizeBox.Text, out var requested) ? Math.Clamp(requested, 8, 200) : 28;
        AddTextAnnotation(position, dialog.Value, size);
    }

    private void AddTextAnnotation(Point position, string value, double size)
    {
        var before = CaptureState();
        var label = new TextBlock { Text = value, Foreground = brush, FontSize = size, FontFamily = new FontFamily("Segoe UI"),
            TextWrapping = TextWrapping.Wrap, MaxWidth = Math.Max(1, DrawingSurface.Width - position.X) };
        WpfCanvas.SetLeft(label, position.X); WpfCanvas.SetTop(label, position.Y);
        Ink.Children.Add(label); CommitState(before);
    }

    private void Undo_Click(object s, RoutedEventArgs e) => UndoEdit();
    private void Redo_Click(object s, RoutedEventArgs e) => RedoEdit();
    private void Clear_Click(object s, RoutedEventArgs e)
    {
        FinishGesture(); var before = CaptureState();
        Ink.Children.Clear(); crop = Rect.Empty; CropSelection.Visibility = Visibility.Collapsed; UpdateDimensions(); CommitState(before);
    }
    private void Cancel_Click(object s, RoutedEventArgs e) { allowDiscard = true; DialogResult = false; }
    private void Save_Click(object s, RoutedEventArgs e)
    {
        FinishGesture();
        try
        {
            var output = RenderImage(); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(output));
            var temp = filePath + ".annotated.tmp";
            try { using (var stream = File.Create(temp)) encoder.Save(stream); File.Move(temp, filePath, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            DialogResult = true;
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, "No se pudieron guardar los cambios: " + ex.Message, "Clipsa"); }
    }

    private BitmapSource RenderImage()
    {
        var visible = CropSelection.Visibility; CropSelection.Visibility = Visibility.Collapsed;
        try
        {
            DrawingSurface.UpdateLayout();
            var width = (int)DrawingSurface.Width; var height = (int)DrawingSurface.Height;
            var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); render.Render(DrawingSurface);
            BitmapSource output = render;
            if (!crop.IsEmpty && crop.Width >= 1 && crop.Height >= 1)
            {
                var left = Math.Clamp((int)crop.Left, 0, width - 1); var top = Math.Clamp((int)crop.Top, 0, height - 1);
                output = new CroppedBitmap(render, new Int32Rect(left, top, Math.Clamp((int)crop.Width, 1, width - left), Math.Clamp((int)crop.Height, 1, height - top)));
            }
            output.Freeze(); return output;
        }
        finally { CropSelection.Visibility = visible; }
    }

    private void ApplyCrop_Click(object s, RoutedEventArgs e)
    {
        FinishGesture();
        if (crop.IsEmpty || crop.Width < 1 || crop.Height < 1) return;
        var before = CaptureState(); SetImage(RenderImage()); CommitState(before);
    }

    private void Rotate_Click(object s, RoutedEventArgs e)
    {
        FinishGesture(); var before = CaptureState();
        var image = new TransformedBitmap(RenderImage(), new RotateTransform(90)); image.Freeze(); SetImage(image); CommitState(before);
    }
}
