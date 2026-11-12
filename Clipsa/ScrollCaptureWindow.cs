using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WpfBrushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace Clipsa;

internal sealed class ScrollCaptureWindow : Window
{
    private readonly Rectangle region;
    private readonly IntPtr targetWindow;
    private readonly Rectangle? targetBounds;
    private readonly DispatcherTimer timer;
    // Offset is the strip's document coordinate, including negative coordinates when scrolling up.
    private readonly List<(Bitmap Image, int Offset)> frames = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly TextBlock status = new() { Foreground = WpfBrushes.White, Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private readonly System.Windows.Controls.Image preview = new() { Width = 180, Height = 90, Stretch = System.Windows.Media.Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 8, 4) };
    private byte[]? previous;
    private Bitmap? header, footer;
    private int position, minimum, maximum, fixedTop, fixedBottom;
    private bool bodyInitialized, processing, finishing, finished, sizeLimitReached;
    private CaptureCancellation? cancelInput;
    public string? ResultPath { get; private set; }

    // Kept exactly for reflection-based LiveScroll verification.
    public ScrollCaptureWindow(Rectangle bounds, int delay)
    {
        if (bounds.Width < 1 || bounds.Height < 1 || bounds.Width > 32768 || bounds.Height > 8192
            || (long)bounds.Width * bounds.Height > 60_000_000) throw new ArgumentOutOfRangeException(nameof(bounds));
        var screen = Forms.Screen.FromRectangle(bounds);
        if (!screen.Bounds.Contains(bounds)) throw new ArgumentException("The capture must fit inside one monitor.", nameof(bounds));
        region = bounds;
        targetWindow = GetAncestor(WindowFromPoint(new NativePoint { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 }), 2);
        if (targetWindow != IntPtr.Zero && GetWindowRect(targetWindow, out var original)) targetBounds = original.Rectangle;
        Title = "Clipsa · Scroll"; Width = 520; Height = 260;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var work = MonitorService.WorkArea(screen);
        Width = Math.Min(Width, work.Width); Height = Math.Min(Height, work.Height);
        Left = work.Left; Top = work.Bottom - Height;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 31, 53));
        var root = new StackPanel { Margin = new Thickness(8) };
        root.Children.Add(status);
        root.Children.Add(preview);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var finish = new Button { Content = "Terminar y guardar", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(4) };
        var partial = new Button { Content = "Guardar lo capturado", IsEnabled = false, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(4), ToolTip = "Guardar sólo los tramos aceptados, aunque el último desplazamiento no se haya podido unir." };
        var cancel = new Button { Content = "Cancelar", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(4) };
        finish.Click += (_, _) => Finish(); cancel.Click += (_, _) => Cancel();
        partial.Click += (_, _) => { if (System.Windows.MessageBox.Show(this, "¿Guardar únicamente los tramos aceptados? La parte final que no se haya unido puede faltar.", "Clipsa", MessageBoxButton.YesNo) == MessageBoxResult.Yes) FinishCore(false); };
        buttons.Children.Add(cancel); buttons.Children.Add(partial); buttons.Children.Add(finish); root.Children.Add(buttons); Content = root;
        // The old three-stable-sample gate skipped the entire gesture during continuous scroll.
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Clamp(delay / 3, 40, 100)) };
        timer.Tick += (_, _) => Tick();
        Loaded += (_, _) =>
        {
            try { cancelInput = new CaptureCancellation(() => Dispatcher.BeginInvoke(Cancel)); }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Activar cancelación de scroll"); }
            var scale = MonitorService.DpiScale(screen);
            var area = screen.WorkingArea;
            var w = Math.Min(area.Width, (int)Math.Ceiling(Width * scale.X));
            var h = Math.Min(area.Height, (int)Math.Ceiling(Height * scale.Y));
            var controls = new Rectangle(area.Left, area.Bottom - h, w, h);
            if (!Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11))
            {
                AppDiagnostics.Log(new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()), "Exclude scroll controls from capture; using placement fallback");
                var choices = new[]
                {
                    new Rectangle(area.Left, area.Top, w, h),
                    new Rectangle(area.Left, area.Bottom - h, w, h),
                    new Rectangle(area.Right - w, area.Top, w, h),
                    new Rectangle(area.Right - w, area.Bottom - h, w, h),
                    new Rectangle(area.Left, region.Bottom + 8, w, h),
                    new Rectangle(area.Left, region.Top - h - 8, w, h),
                    new Rectangle(region.Right + 8, area.Top, w, h),
                    new Rectangle(region.Left - w - 8, area.Top, w, h)
                };
                var safe = choices.FirstOrDefault(r => area.Contains(r) && !r.IntersectsWith(region));
                if (safe.Width == 0)
                {
                    status.Text = "Selecciona una zona más pequeña para dejar fuera los controles de scroll.";
                    finish.IsEnabled = false; return;
                }
                controls = safe;
            }
            MonitorService.PlaceWindow(this, controls);
            try
            {
                // Establish the baseline immediately, BEFORE inviting the user to scroll.
                using var initial = Capture();
                InitializeFirstFrame(initial, ScrollStitcher.Pixels(initial));
                partial.IsEnabled = true;
                status.Text = $"Inicio guardado · {region.Width} × {region.Height} px. Desplaza arriba o abajo, dejando solapamiento. Terminar para guardar · Esc o clic derecho para cancelar.";
                timer.Start();
            }
            catch (Exception ex)
            {
                AppDiagnostics.Log(ex, "Initialize manual scroll capture");
                status.Text = "No se pudo guardar el inicio: " + ex.Message;
                finish.IsEnabled = false;
            }
        };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Cancel(); } };
        PreviewMouseRightButtonUp += (_, e) => { e.Handled = true; Cancel(); };
        Closed += (_, _) =>
        {
            cancelInput?.Dispose(); cancelInput = null;
            timer.Stop(); cancellation.Cancel();
            finished = true;
            foreach (var frame in frames) frame.Image.Dispose();
            header?.Dispose(); footer?.Dispose();
        };
    }

    private void Cancel()
    {
        if (finished || !IsVisible) return;
        ResultPath = null; timer.Stop(); cancellation.Cancel(); DialogResult = false;
    }

    private bool TargetUnmoved()
    {
        if (targetBounds is not Rectangle original) return true;
        if (GetWindowRect(targetWindow, out var current) && current.Rectangle == original) return true;
        status.Text = "La ventana se ha movido, redimensionado o cerrado. Restaura su posición y tamaño para continuar, o cancela.";
        return false;
    }

    private Bitmap Capture()
    {
        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(region.Location, System.Drawing.Point.Empty, region.Size);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    private async void Tick()
    {
        if (finished || processing || finishing || sizeLimitReached) return;
        processing = true;
        try
        {
            if (!TargetUnmoved()) return;
            using var frame = Capture();
            var token = cancellation.Token;
            var pixels = await Task.Run(() => ScrollStitcher.Pixels(frame), token);
            if (finished) return;
            // Accept a matched displacement even while moving. Failed/animated frames do not
            // replace previous, so the first and all already accepted strips remain intact.
            await Accept(frame, pixels, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Capture manual scroll frame"); timer.Stop(); status.Text = "No se pudo capturar: " + ex.Message; }
        finally { processing = false; }
    }

    private async Task<bool> Accept(Bitmap frame, byte[] pixels, CancellationToken token)
    {
        if (previous is null)
        {
            InitializeFirstFrame(frame, pixels);
            status.Text = "Primer tramo capturado. Desplaza y haz una pausa corta.";
            return true;
        }
        if (ScrollStitcher.Difference(previous, pixels, frame.Width, frame.Height) <= .5) return true;
        var old = previous;
        var top = bodyInitialized ? fixedTop : -1;
        var bottom = bodyInitialized ? fixedBottom : -1;
        var match = await Task.Run(() => ScrollStitcher.Match(old, pixels, frame.Width, frame.Height, token, top, bottom), token);
        token.ThrowIfCancellationRequested();
        if (finished) return false;
        if (match.Kind == ScrollStitcher.MatchKind.Unchanged) return true;
        if (match.Kind != ScrollStitcher.MatchKind.Matched)
        {
            status.Text = match.Kind == ScrollStitcher.MatchKind.Ambiguous
                ? "Esperando contenido reconocible. Lo capturado se conserva; desplázate más despacio."
                : "El desplazamiento ha sido demasiado grande o el contenido cambió. Lo capturado se conserva; vuelve un poco hacia arriba y continúa despacio.";
            return false;
        }
        if (!bodyInitialized)
        {
            fixedTop = match.FixedTop; fixedBottom = match.FixedBottom;
            var first = frames[0].Image;
            Bitmap? newHeader = null, newFooter = null, body = null;
            try
            {
                if (fixedTop > 0) newHeader = first.Clone(new Rectangle(0, 0, region.Width, fixedTop), PixelFormat.Format32bppArgb);
                if (fixedBottom > 0) newFooter = first.Clone(new Rectangle(0, region.Height - fixedBottom, region.Width, fixedBottom), PixelFormat.Format32bppArgb);
                body = first.Clone(new Rectangle(0, fixedTop, region.Width, region.Height - fixedTop - fixedBottom), PixelFormat.Format32bppArgb);
            }
            catch { newHeader?.Dispose(); newFooter?.Dispose(); body?.Dispose(); throw; }
            header = newHeader; footer = newFooter;
            frames[0] = (body!, 0); first.Dispose();
            maximum = body!.Height; bodyInitialized = true;
        }
        var bodyHeight = region.Height - fixedTop - fixedBottom;
        var next = checked(position + match.Offset);
        var newMin = Math.Min(minimum, next);
        var newMax = Math.Max(maximum, checked(next + bodyHeight));
        var outputHeight = (long)newMax - newMin + fixedTop + fixedBottom;
        // Reserve a bounded output and cap strip metadata even for one-pixel movements.
        if (outputHeight > 65535 || region.Width * outputHeight > 60_000_000 || frames.Count >= 4096)
        {
            sizeLimitReached = true; timer.Stop();
            status.Text = "Límite de tamaño alcanzado. Pulsa Terminar para guardar los tramos aceptados.";
            return false;
        }
        if (next < minimum)
        {
            var count = minimum - next;
            frames.Add((frame.Clone(new Rectangle(0, fixedTop, region.Width, count), PixelFormat.Format32bppArgb), next));
        }
        if (next + bodyHeight > maximum)
        {
            var count = next + bodyHeight - maximum;
            frames.Add((frame.Clone(new Rectangle(0, fixedTop + bodyHeight - count, region.Width, count), PixelFormat.Format32bppArgb), maximum));
        }
        minimum = newMin; maximum = newMax; position = next; previous = pixels;
        UpdatePreview();
        status.Text = $"{frames.Count} tramos · {region.Width} × {outputHeight} px. Sigue desplazando o pulsa Terminar.";
        return true;
    }

    private void InitializeFirstFrame(Bitmap frame, byte[] pixels)
    {
        if (previous is not null) throw new InvalidOperationException("Scroll baseline already initialized.");
        frames.Add((new Bitmap(frame), 0)); previous = pixels; maximum = frame.Height;
        UpdatePreview();
    }
    private void UpdatePreview()
    {
        var height = maximum - minimum + fixedTop + fixedBottom;
        using var thumb = new Bitmap(180, 90, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(thumb))
        {
            graphics.Clear(System.Drawing.Color.Transparent);
            var scale = Math.Min(180f / region.Width, 90f / height); graphics.ScaleTransform(scale, scale);
            if (header is not null) graphics.DrawImageUnscaled(header, 0, 0);
            foreach (var frame in frames) graphics.DrawImageUnscaled(frame.Image, 0, fixedTop + frame.Offset - minimum);
            if (footer is not null) graphics.DrawImageUnscaled(footer, 0, fixedTop + maximum - minimum);
        }
        using var stream = new System.IO.MemoryStream(); thumb.Save(stream, ImageFormat.Png); stream.Position = 0;
        var image = new System.Windows.Media.Imaging.BitmapImage(); image.BeginInit(); image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); preview.Source = image;
    }

    private void Finish() => FinishCore(true);
    private async void FinishCore(bool includeFinal)
    {
        if (finished || finishing) return;
        finishing = true; timer.Stop();
        try
        {
            while (processing) await Task.Delay(20, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!sizeLimitReached && includeFinal)
            {
                if (!TargetUnmoved()) { timer.Start(); return; }
                using var final = Capture();
                // An unchanged final frame completes synchronously for the existing reflection verifier.
                if (!await Accept(final, ScrollStitcher.Pixels(final), cancellation.Token) && !sizeLimitReached)
                { timer.Start(); return; }
            }
            using var result = new Bitmap(region.Width, maximum - minimum + fixedTop + fixedBottom, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(result))
            {
                if (header is not null) graphics.DrawImageUnscaled(header, 0, 0);
                foreach (var frame in frames) graphics.DrawImageUnscaled(frame.Image, 0, fixedTop + frame.Offset - minimum);
                if (footer is not null) graphics.DrawImageUnscaled(footer, 0, fixedTop + maximum - minimum);
            }
            ResultPath = CaptureService.Save(result); finished = true; DialogResult = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Finish manual scroll capture"); status.Text = "No se pudo terminar: " + ex.Message; }
        finally { finishing = false; }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public Rectangle Rectangle => System.Drawing.Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
}
