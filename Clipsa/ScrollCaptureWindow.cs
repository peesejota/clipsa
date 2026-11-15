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
    private readonly ScrollDriver driver;
    private readonly Button guided = new() { Content = "Capturar más", IsEnabled = false, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4), ToolTip = "Avanza en pasos cortos, comprueba cada unión y se detiene al llegar al final. Pulsa de nuevo para parar." };
    private CancellationTokenSource? guidedCancellation;
    private Task? guidedTask;
    private bool gapDetected;
    private bool captureLoop;
    private bool captureResult;
    private bool finishOnRelease;
    private bool captureStarted;
    private int firstWheel, firstKey;
    private Task? firstGesture;
    private ScrollRegionFrame? selectionFrame;
    private long gapSince;
    public void UseHoldGesture(int wheel, int key) { finishOnRelease = true; firstWheel = wheel; firstKey = key; }
    public void PrepareBaseline(Bitmap frame)
    {
        if (frame.Width != region.Width || frame.Height != region.Height) throw new ArgumentException("Initial frame must match the selected region.");
        InitializeFirstFrame(frame, ScrollStitcher.Pixels(frame));
    }
    public bool? ShowCapture()
    {
        captureLoop = true;
        var frame = new DispatcherFrame();
        Closed += (_, _) => frame.Continue = false;
        ShowActivated = false; Show();
        if (IsVisible) Dispatcher.PushFrame(frame);
        return captureResult;
    }
    private void Complete(bool success)
    {
        if (captureLoop) { captureResult = success; Close(); }
        else DialogResult = success;
    }
    private readonly DispatcherTimer timer;
    private readonly DispatcherTimer releaseTimer = new() { Interval = TimeSpan.FromMilliseconds(20) };
    private readonly Queue<Bitmap> pending = new();
    private readonly int queueLimit;
    // Offset is the strip's document coordinate, including negative coordinates when scrolling up.
    private readonly List<(Bitmap Image, int Offset)> frames = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly TextBlock status = new() { Foreground = WpfBrushes.White, Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private byte[]? previous;
    private byte[]? anchor;
    private int anchorPosition;
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
        queueLimit = (int)Math.Clamp(64_000_000L / ((long)bounds.Width * bounds.Height * 4), 1, 8);
        var contentWindow = WindowFromPoint(new NativePoint { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 });
        targetWindow = GetAncestor(contentWindow, 2);
        driver = new ScrollDriver(targetWindow, contentWindow, bounds, cancellation.Token);
        if (targetWindow != IntPtr.Zero && GetWindowRect(targetWindow, out var original)) targetBounds = original.Rectangle;
        Title = "Clipsa · Scroll"; Width = 540; Height = 116;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var work = MonitorService.WorkArea(screen);
        Width = Math.Min(Width, work.Width); Height = Math.Min(Height, work.Height);
        Left = work.Left; Top = work.Bottom - Height;
        Background = WpfBrushes.Transparent;
        var root = new StackPanel { Margin = new Thickness(8) };
        root.Children.Add(status);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var finish = new Button { Content = "Terminar y guardar", IsEnabled = false, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(4) };
        var partial = new Button { Content = "Guardar parcial", IsEnabled = false, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(4), ToolTip = "Guardar sólo los tramos aceptados, aunque el último desplazamiento no se haya podido unir." };
        var cancel = new Button { Content = "Cancelar", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(4) };
        finish.Click += (_, _) => Finish(); cancel.Click += (_, _) => Cancel();
        partial.Click += (_, _) => { if (System.Windows.MessageBox.Show(this, "¿Guardar únicamente los tramos aceptados? La parte final que no se haya unido puede faltar.", "Clipsa", MessageBoxButton.YesNo) == MessageBoxResult.Yes) FinishCore(false); };
        guided.Click += (_, _) => ToggleGuided();
        buttons.Children.Add(cancel); buttons.Children.Add(guided); buttons.Children.Add(partial); buttons.Children.Add(finish); root.Children.Add(buttons);
        Content = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 169, 155, 255)), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 29, 25, 48)), Child = root };
        // The old three-stable-sample gate skipped the entire gesture during continuous scroll.
        // Keep screen sampling independent of matching so short intermediate
        // views are retained while the previous pair is being processed.
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Clamp(delay / 20, 16, 20)) };
        timer.Tick += (_, _) => Tick();
        releaseTimer.Tick += (_, _) =>
        {
            if (!finishOnRelease || finished) { releaseTimer.Stop(); return; }
            if ((GetAsyncKeyState(1) & 0x8000) == 0) { releaseTimer.Stop(); Finish(); }
        };
        var controlsReady = false;
        status.Text = "Preparando captura…";
        Loaded += (_, _) =>
        {
            try { cancelInput = new CaptureCancellation(() => Dispatcher.BeginInvoke(Cancel)); }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Activar cancelación de scroll"); }
            var scale = MonitorService.DpiScale(screen);
            var area = screen.WorkingArea;
            var w = Math.Min(area.Width, (int)Math.Ceiling(Width * scale.X));
            var h = Math.Min(area.Height, (int)Math.Ceiling(Height * scale.Y));
            var controls = new Rectangle(area.Left, area.Bottom - h, w, h);
            var excluded = Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11);
            if (!excluded)
            {
                AppDiagnostics.Log(new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()), "Exclude scroll controls from capture; using placement fallback");
            }
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
            if (safe.Width == 0 && !excluded)
            {
                status.Text = "Selecciona una zona más pequeña para dejar fuera los controles de scroll.";
                finish.IsEnabled = false; return;
            }
            if (safe.Width > 0) controls = safe;
            MonitorService.PlaceWindow(this, controls);
            controlsReady = true;
        };
        ContentRendered += (_, _) =>
        {
            if (!controlsReady || captureStarted || finished) return;
            captureStarted = true;
            driver.Focus();
            Native.DwmFlush(); // Placement, WPF rendering and exclusion must all be committed.
            try
            {
                selectionFrame = new ScrollRegionFrame(region) { Owner = this };
                if (!selectionFrame.ShowGuide()) selectionFrame = null;
                driver.Focus(); Native.DwmFlush();
                // Establish the baseline immediately, BEFORE inviting the user to scroll.
                if (previous is null)
                {
                    using var initial = Capture();
                    InitializeFirstFrame(initial, ScrollStitcher.Pixels(initial));
                }
                partial.IsEnabled = true; finish.IsEnabled = true; guided.IsEnabled = true;
                status.Text = finishOnRelease
                    ? $"{region.Width} × {region.Height} px · Sigue pulsando y desplaza. Suelta para terminar. Esc cancela."
                    : $"{region.Width} × {region.Height} px · Desplaza con rueda, trackpad o flechas. Terminar para guardar. Esc cancela.";
                timer.Start();
                if (finishOnRelease)
                {
                    firstGesture = Task.Run(() => driver.ForwardInitial(firstWheel, firstKey));
                    _ = ObserveFirstGesture();
                    releaseTimer.Start();
                }
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
            selectionFrame?.Close(); selectionFrame = null;
            timer.Stop(); releaseTimer.Stop(); cancellation.Cancel();
            finished = true;
            ClearPending();
            foreach (var frame in frames) frame.Image.Dispose();
            header?.Dispose(); footer?.Dispose();
        };
    }

    private async Task ObserveFirstGesture()
    {
        try { if (firstGesture is not null) await firstGesture; }
        catch (Exception ex) { firstGesture = null; AppDiagnostics.Log(ex, "Iniciar scroll mantenido"); if (!finished) { finishOnRelease = false; status.Text = "No se pudo enviar el primer movimiento. Puedes continuar manualmente y pulsar Terminar."; } }
    }

    private void Cancel()
    {
        if (finished || !IsVisible) return;
        ResultPath = null; timer.Stop(); cancellation.Cancel(); Complete(false);
    }

    private void ToggleGuided()
    {
        if (guidedTask is { IsCompleted: false }) { guidedCancellation?.Cancel(); driver.Focus(); return; }
        if (finished || finishing || previous is null || sizeLimitReached) return;
        guidedCancellation?.Dispose();
        guidedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        timer.Stop(); guided.Content = "Parar scroll"; driver.Focus();
        guidedTask = RunGuided(guidedCancellation.Token);
    }
    private async Task RunGuided(CancellationToken token)
    {
        try
        {
            while (processing) await Task.Delay(20, token);
            using (var initial = Capture())
                if (!await Accept(initial, ScrollStitcher.Pixels(initial), token))
                { status.Text = "Vuelve a la última zona capturada antes de iniciar el modo guiado; falta una unión."; return; }
            var unchanged = 0;
            while (!finished && !sizeLimitReached)
            {
                token.ThrowIfCancellationRequested();
                if (!TargetUnmoved()) break;
                await Task.Run(driver.Step, token).WaitAsync(TimeSpan.FromSeconds(2));
                var moved = false;
                byte[]? lastSample = null;
                var stable = 0;
                // Sample the whole animation rather than sleeping through its middle.
                // Three stable samples release the next step; slower applications get
                // up to one second. A transient animation does not abort a valid join.
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    await Task.Delay(24, token);
                    if (!TargetUnmoved()) return;
                    using var frame = Capture();
                    var pixels = await Task.Run(() => ScrollStitcher.Pixels(frame), token);
                    stable = lastSample is not null && ScrollStitcher.Difference(lastSample, pixels, frame.Width, frame.Height) <= .5 ? stable + 1 : 0;
                    lastSample = pixels;
                    if (previous is not null && ScrollStitcher.Difference(previous, pixels, frame.Width, frame.Height) > .5)
                    {
                        var before = position;
                        if (await Accept(frame, pixels, token)) moved |= position != before;
                    }
                    if (sizeLimitReached) return;
                    if (stable >= 3 && (moved || attempt >= 11)) break;
                }
                if (gapDetected) { status.Text = "Scroll guiado pausado para conservar la continuidad. Vuelve un poco arriba hasta recuperar la unión y reanuda."; return; }
                unchanged = moved ? 0 : unchanged + 1;
                if (unchanged >= (driver.AtEnd ? 2 : 4)) { status.Text = driver.AtEnd ? "Final de página alcanzado. Pulsa Terminar y guardar." : "La página no responde al guiado o no avanza más. Comprueba el final o continúa manualmente antes de guardar."; break; }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Scroll guiado"); status.Text = "Scroll guiado pausado: " + ex.Message; }
        finally { guided.Content = "Capturar más"; if (!finished && !finishing && !sizeLimitReached) timer.Start(); }
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

    private void Tick()
    {
        if (finished || finishing || sizeLimitReached) return;
        try
        {
            if (!TargetUnmoved()) return;
            if (pending.Count >= queueLimit)
            {
                status.Text = "Procesando los tramos capturados. Reduce un momento la velocidad para mantener todo el recorrido.";
                return;
            }
            pending.Enqueue(Capture());
            DrainPending();
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Take manual scroll frame"); timer.Stop(); status.Text = "No se pudo capturar: " + ex.Message; }
    }

    private async void DrainPending()
    {
        if (processing || finished) return;
        processing = true;
        try
        {
            while (!finished && pending.TryDequeue(out var frame))
            {
                using (frame)
                {
                    var token = cancellation.Token;
                    var pixels = await Task.Run(() => ScrollStitcher.Pixels(frame), token);
                    if (finished) return;
                    await Accept(frame, pixels, token);
                }
                if (sizeLimitReached) { ClearPending(); break; }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Process manual scroll frame"); timer.Stop(); ClearPending(); status.Text = "No se pudo procesar: " + ex.Message; }
        finally { processing = false; }
    }
    private void ClearPending() { while (pending.TryDequeue(out var frame)) frame.Dispose(); }

    private async Task<bool> Accept(Bitmap frame, byte[] pixels, CancellationToken token)
    {
        if (previous is null)
        {
            InitializeFirstFrame(frame, pixels);
            status.Text = "Primer tramo capturado. Desplaza y haz una pausa corta.";
            return true;
        }
        if (ScrollStitcher.Difference(previous, pixels, frame.Width, frame.Height) <= .5)
        { if (gapDetected) { gapDetected = false; gapSince = 0; selectionFrame?.SetGap(false); status.Text = "Unión recuperada. Puedes continuar."; } return true; }
        var old = anchor ?? previous;
        var referencePosition = anchor is null ? position : anchorPosition;
        var top = bodyInitialized ? fixedTop : -1;
        var bottom = bodyInitialized ? fixedBottom : -1;
        var match = await Task.Run(() => ScrollStitcher.Match(old, pixels, frame.Width, frame.Height, token, top, bottom), token);
        if (match.Kind is ScrollStitcher.MatchKind.NoOverlap or ScrollStitcher.MatchKind.Ambiguous && !ReferenceEquals(old, previous))
        {
            old = previous; referencePosition = position;
            match = await Task.Run(() => ScrollStitcher.Match(old, pixels, frame.Width, frame.Height, token, top, bottom), token);
        }
        token.ThrowIfCancellationRequested();
        if (finished) return false;
        if (match.Kind == ScrollStitcher.MatchKind.Unchanged)
        {
            if (referencePosition == position)
            {
                gapDetected = false; gapSince = 0; selectionFrame?.SetGap(false); previous = pixels;
                return true;
            }
            match = new(ScrollStitcher.MatchKind.Matched, 0, fixedTop, fixedBottom, 0);
        }
        if (match.Kind != ScrollStitcher.MatchKind.Matched)
        {
            if (!gapDetected) gapSince = Environment.TickCount64;
            gapDetected = true;
            if (Environment.TickCount64 - gapSince < 350) return false;
            selectionFrame?.SetGap(true);
            status.Text = match.Kind == ScrollStitcher.MatchKind.Ambiguous
                ? "Esperando contenido reconocible. Lo capturado se conserva; desplázate más despacio."
                : "Falta un tramo por un salto grande o un cambio de contenido. La captura sigue activa: vuelve un poco arriba hasta recuperar la unión y continúa. Lo anterior se conserva.";
            return false;
        }
        gapDetected = false;
        gapSince = 0; selectionFrame?.SetGap(false);
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
        var next = checked(referencePosition + match.Offset);
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
        // A stable reference prevents ±1 pixel rounding errors accumulating across
        // hundreds of tiny smooth-scroll frames at fractional display scales.
        if (anchor is null || Math.Abs((long)next - anchorPosition) >= Math.Max(1, bodyHeight / 3))
        { anchor = pixels; anchorPosition = next; }
        status.Text = $"{region.Width} × {outputHeight} px · {(finishOnRelease ? "Sigue pulsando y desplaza; suelta para terminar." : "Sigue desplazando o pulsa Terminar.")}";
        return true;
    }

    private void InitializeFirstFrame(Bitmap frame, byte[] pixels)
    {
        if (previous is not null) throw new InvalidOperationException("Scroll baseline already initialized.");
        frames.Add((new Bitmap(frame), 0)); previous = anchor = pixels; anchorPosition = 0; maximum = frame.Height;
    }

    private void Finish() => FinishCore(true);
    private async void FinishCore(bool includeFinal)
    {
        if (finished || finishing) return;
        finishing = true; timer.Stop();
        // Once released, a recoverable gap stays in manual mode instead of
        // retrying Finish on every timer tick or silently exporting a partial image.
        var wasHeld = finishOnRelease; finishOnRelease = false;
        try
        {
            if (firstGesture is not null) await firstGesture;
            guidedCancellation?.Cancel();
            if (guidedTask is not null || wasHeld)
            {
                if (guidedTask is not null) await guidedTask;
                while (processing) await Task.Delay(20, cancellation.Token);
                byte[]? last = null; var stable = 0;
                for (var attempt = 0; attempt < 40 && stable < 3; attempt++)
                {
                    await Task.Delay(24, cancellation.Token); Native.DwmFlush();
                    if (!TargetUnmoved()) { timer.Start(); return; }
                    using var settling = Capture(); var pixels = ScrollStitcher.Pixels(settling);
                    stable = last is not null && ScrollStitcher.Difference(last, pixels, region.Width, region.Height) <= .5 ? stable + 1 : 0;
                    last = pixels;
                    await Accept(settling, pixels, cancellation.Token);
                    if (sizeLimitReached) break;
                }
            }
            while (processing) await Task.Delay(20, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!sizeLimitReached && includeFinal)
            {
                if (!TargetUnmoved()) { timer.Start(); return; }
                using var final = Capture();
                // An unchanged final frame completes synchronously for the existing reflection verifier.
                if (!await Accept(final, ScrollStitcher.Pixels(final), cancellation.Token) && !sizeLimitReached)
                {
                    selectionFrame?.SetGap(true);
                    status.Text = "El último tramo aún no se ha unido. Vuelve un poco arriba para recuperarlo, o usa Guardar parcial para conservar sólo lo aceptado.";
                    timer.Start(); return;
                }
            }
            using var result = new Bitmap(region.Width, maximum - minimum + fixedTop + fixedBottom, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(result))
            {
                if (header is not null) graphics.DrawImageUnscaled(header, 0, 0);
                foreach (var frame in frames) graphics.DrawImageUnscaled(frame.Image, 0, fixedTop + frame.Offset - minimum);
                if (footer is not null) graphics.DrawImageUnscaled(footer, 0, fixedTop + maximum - minimum);
            }
            ResultPath = CaptureService.Save(result); finished = true; Complete(true);
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
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
}
