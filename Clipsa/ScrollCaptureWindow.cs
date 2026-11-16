using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Clipsa.Capture;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;

namespace Clipsa;

// UI adapter only. Pixel capture and stitching live in Capture/ and are shared
// with the screen/region capture entry points.
internal sealed class ScrollCaptureWindow : Window
{
    private readonly Rectangle region;
    private readonly IntPtr targetWindow;
    private readonly Rectangle targetBounds;
    private readonly ScrollDriver driver;
    private readonly ShareXScrollComposer composer = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Queue<Bitmap> pending = new();
    private readonly DispatcherTimer samples = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly DispatcherTimer release = new() { Interval = TimeSpan.FromMilliseconds(20) };
    private readonly TextBlock status = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8), MaxHeight = 42 };
    private readonly CheckBox estimates = new() { Content = "Permitir avance estimado si una animación impide unir", Foreground = Brushes.White, Margin = new Thickness(8, 0, 8, 4) };
    private readonly ComboBox method = new() { Width = 122, Margin = new Thickness(4), SelectedIndex = 0 };
    private readonly Button automatic = new() { Content = "Automático", Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
    private readonly Button stop = new() { Content = "Parar", Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
    private readonly Button save = new() { Content = "Terminar y guardar", Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
    private readonly int delay, queueLimit;
    private Task? processingTask, automaticTask, firstGesture;
    private CancellationTokenSource? automaticCancellation;
    private CaptureCancellation? cancelInput;
    private ScrollRegionFrame? selectionFrame;
    private bool ready, finishing, finished, held, completeResult, loop, hasGap, sizeLimit;
    private int firstWheel, firstKey;
    public bool StartAutomatically { get; set; }
    public byte[]? InitialPixels { get; private set; }
    public string? ResultPath { get; private set; }
    public int CapturedHeight => composer.Height;

    public ScrollCaptureWindow(Rectangle bounds, int delay)
    {
        if (bounds.Width < 1 || bounds.Height < 3 || (long)bounds.Width * bounds.Height > 60_000_000) throw new ArgumentOutOfRangeException(nameof(bounds));
        if (!System.Windows.Forms.Screen.FromRectangle(bounds).Bounds.Contains(bounds)) throw new ArgumentException("The capture must fit inside one monitor.");
        region = bounds; this.delay = Math.Clamp(delay, 100, 1000);
        queueLimit = (int)Math.Clamp(64_000_000L / ((long)bounds.Width * bounds.Height * 4), 1, 8);
        var content = WindowFromPoint(new NativePoint { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 });
        targetWindow = GetAncestor(content, 2);
        GetWindowRect(targetWindow, out var target); targetBounds = target.Rectangle;
        driver = new ScrollDriver(targetWindow, content, bounds, lifetime.Token);
        Title = "Clipsa · Captura con scroll"; Width = 570; Height = 142;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = System.Windows.Forms.Screen.FromRectangle(bounds); var work = MonitorService.WorkArea(screen);
        Width = Math.Min(Width, work.Width); Height = Math.Min(Height, work.Height); Left = work.Left; Top = work.Top;
        foreach (var label in new[] { "Rueda", "Flecha abajo", "Av. página", "Mensaje de scroll" }) method.Items.Add(label);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
        automatic.Click += (_, _) => StartAutomatic(); stop.Click += (_, _) => StopAutomatic(); save.Click += (_, _) => Finish(); cancel.Click += (_, _) => Cancel();
        estimates.Checked += (_, _) => { if (!IsAutomatic) composer.AllowEstimates = true; };
        estimates.Unchecked += (_, _) => { if (!IsAutomatic) composer.AllowEstimates = false; };
        buttons.Children.Add(method); buttons.Children.Add(automatic); buttons.Children.Add(stop); buttons.Children.Add(cancel); buttons.Children.Add(save);
        var stack = new StackPanel(); stack.Children.Add(status); stack.Children.Add(estimates); stack.Children.Add(buttons);
        Content = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 169, 155, 255)), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(242, 29, 25, 48)), Child = stack };
        samples.Tick += (_, _) => Sample();
        release.Tick += (_, _) => { if (held && !finishing && (GetAsyncKeyState(1) & 0x8000) == 0) { release.Stop(); Finish(); } };
        Loaded += (_, _) =>
        {
            cancelInput = new CaptureCancellation(() => Dispatcher.BeginInvoke(Cancel));
            PlaceControls(screen);
        };
        ContentRendered += (_, _) => Initialize();
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Cancel(); } };
        Closed += (_, _) =>
        {
            finished = true; samples.Stop(); release.Stop(); lifetime.Cancel(); automaticCancellation?.Cancel();
            cancelInput?.Dispose(); selectionFrame?.Close(); selectionFrame = null; ClearPending();
            _ = DisposeAfterWork();
        };
    }
    private bool IsAutomatic => automaticTask is { IsCompleted: false };
    public void UseHoldGesture(int wheel, int key) { held = true; firstWheel = wheel; firstKey = key; }
    public void PrepareBaseline(Bitmap frame)
    {
        if (frame.Size != region.Size) throw new ArgumentException("Baseline size differs from selection.");
        InitialPixels = CapturePixels.Read(frame); composer.Append(frame);
    }
    public bool? ShowCapture()
    {
        loop = true;
        var frame = new DispatcherFrame(); Closed += (_, _) => frame.Continue = false;
        Show(); if (IsVisible) Dispatcher.PushFrame(frame);
        return completeResult;
    }
    private void Complete(bool success)
    {
        completeResult = success; if (loop) Close(); else DialogResult = success;
    }
    private void PlaceControls(System.Windows.Forms.Screen screen)
    {
        var scale = MonitorService.DpiScale(screen); var work = screen.WorkingArea;
        var w = Math.Min(work.Width, (int)Math.Ceiling(Width * scale.X)); var h = Math.Min(work.Height, (int)Math.Ceiling(Height * scale.Y));
        var excluded = Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11);
        var choices = new[] { new Rectangle(work.Left, work.Top, w, h), new Rectangle(work.Left, work.Bottom - h, w, h), new Rectangle(work.Right - w, work.Top, w, h), new Rectangle(work.Right - w, work.Bottom - h, w, h) };
        var safe = choices.FirstOrDefault(r => !r.IntersectsWith(region));
        if (!excluded && safe.IsEmpty) throw new InvalidOperationException("Selecciona un área menor para dejar fuera los controles.");
        MonitorService.PlaceWindow(this, safe.IsEmpty ? choices[0] : safe);
    }
    private void Initialize()
    {
        if (ready || finished) return;
        try
        {
            selectionFrame = new ScrollRegionFrame(region) { Owner = this };
            if (!selectionFrame.ShowGuide()) selectionFrame = null;
            driver.Focus(); Native.DwmFlush();
            if (InitialPixels is null) { using var first = Capture(); PrepareBaseline(first); }
            ready = true; UpdateStatus(); samples.Start();
            if (held)
            {
                firstGesture = SendInitial(); release.Start();
            }
            else if (StartAutomatically) Dispatcher.BeginInvoke(StartAutomatic);
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Preparar captura ShareX"); status.Text = "No se pudo iniciar: " + ex.Message; save.IsEnabled = false; automatic.IsEnabled = false; }
    }
    private async Task SendInitial()
    {
        try { await Task.Run(() => driver.ForwardInitial(firstWheel, firstKey), lifetime.Token); }
        catch (Exception ex) { held = false; release.Stop(); status.Text = ex.Message; AppDiagnostics.Log(ex, "Primer movimiento"); }
    }
    private bool TargetValid()
    {
        if (GetWindowRect(targetWindow, out var bounds) && bounds.Rectangle == targetBounds && !targetBounds.IsEmpty) return true;
        status.Text = "La ventana cambió de posición o tamaño. Restáurala o cancela."; return false;
    }
    private Bitmap Capture() => DesktopCapture.Capture(region);
    private void Sample()
    {
        if (!ready || finished || finishing || IsAutomatic || sizeLimit || !TargetValid() || pending.Count >= queueLimit) return;
        try { pending.Enqueue(Capture()); if (processingTask is null || processingTask.IsCompleted) processingTask = Drain(); }
        catch (Exception ex) { samples.Stop(); status.Text = ex.Message; AppDiagnostics.Log(ex, "Capturar frame"); }
    }
    private async Task Drain()
    {
        try
        {
            while (!finished && pending.TryDequeue(out var frame))
            {
                using (frame) await Append(frame, lifetime.Token);
                if (sizeLimit) { ClearPending(); break; }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { samples.Stop(); ClearPending(); if (!finished) status.Text = ex.Message; AppDiagnostics.Log(ex, "Unir captura ShareX"); }
    }
    private async Task<ScrollCombineStatus> Append(Bitmap frame, CancellationToken token)
    {
        var result = await Task.Run(() => composer.Append(frame, token), token);
        if (finished) return result;
        hasGap = result == ScrollCombineStatus.NoMatch;
        sizeLimit = result == ScrollCombineStatus.SizeLimit;
        if (result is ScrollCombineStatus.Appended or ScrollCombineStatus.Estimated) { selectionFrame?.SetGap(false); UpdateStatus(); }
        if (sizeLimit) { samples.Stop(); status.Text = "Límite de imagen alcanzado. Termina para conservar lo capturado."; }
        return result;
    }
    private void UpdateStatus() => status.Text = $"{composer.Width} × {composer.Height} px · {(held ? "Desplaza sin soltar; suelta para terminar." : IsAutomatic ? "Avanzando y capturando. Parar detiene el avance." : "Desplaza tú, o usa Automático. Terminar guarda.")}{(composer.HasEstimates ? " · Hay uniones estimadas para revisar." : "")}";
    private void StartAutomatic()
    {
        if (!ready || finished || finishing || IsAutomatic || sizeLimit) return;
        held = false; release.Stop(); samples.Stop();
        automaticCancellation?.Dispose(); automaticCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var selected = (ScrollInputMethod)Math.Max(0, method.SelectedIndex);
        composer.AllowEstimates = estimates.IsChecked == true;
        method.IsEnabled = estimates.IsEnabled = false; automatic.IsEnabled = false;
        automaticTask = RunAutomatic(selected, automaticCancellation.Token);
    }
    private void StopAutomatic() { automaticCancellation?.Cancel(); driver.Focus(); }
    private async Task RunAutomatic(ScrollInputMethod input, CancellationToken token)
    {
        Bitmap? last = null;
        try
        {
            if (processingTask is not null) await processingTask;
            token.ThrowIfCancellationRequested();
            driver.FocusForAutomatic();
            await Task.Delay(300, token);
            last = Capture();
            if (await Append(last, token) == ScrollCombineStatus.NoMatch)
            {
                selectionFrame?.SetGap(true);
                status.Text = "Vuelve al último tramo capturado antes de iniciar el avance automático."; return;
            }
            var unchanged = 0;
            for (var step = 0; step < 600 && !sizeLimit; step++)
            {
                token.ThrowIfCancellationRequested(); if (!TargetValid()) return;
                driver.Step(input);
                using var current = await SettledFrame(token);
                if (CapturePixels.Equal(last, current))
                {
                    if (++unchanged >= 2) { status.Text = "Fin del desplazamiento detectado. Pulsa Terminar y guardar."; return; }
                }
                else
                {
                    unchanged = 0;
                    var combined = await Append(current, token);
                    if (combined == ScrollCombineStatus.NoMatch)
                    {
                        selectionFrame?.SetGap(true);
                        status.Text = "No hay unión fiable con este avance. Prueba Flecha abajo, reduce el área o vuelve un poco arriba. Lo capturado se conserva."; return;
                    }
                }
                last.Dispose(); last = new Bitmap(current);
            }
            if (!sizeLimit) status.Text = "Límite de pasos alcanzado. Revisa y termina la captura.";
        }
        catch (OperationCanceledException) { if (!finished && !finishing) UpdateStatus(); }
        catch (Exception ex) { if (!finished) status.Text = "Captura automática detenida: " + ex.Message; AppDiagnostics.Log(ex, "Scroll automático ShareX"); }
        finally
        {
            last?.Dispose(); method.IsEnabled = estimates.IsEnabled = true; automatic.IsEnabled = true;
            if (!finished && !finishing && !sizeLimit) samples.Start();
        }
    }
    private async Task<Bitmap> SettledFrame(CancellationToken token)
    {
        await Task.Delay(delay, token);
        Bitmap? last = Capture();
        try
        {
            var stable = 0;
            for (var i = 0; i < 40 && stable < 2; i++)
            {
                await Task.Delay(40, token); if (!TargetValid()) throw new InvalidOperationException("La ventana cambió durante la captura.");
                using var next = Capture();
                stable = CapturePixels.Equal(last, next) ? stable + 1 : 0;
                last.Dispose(); last = new Bitmap(next);
            }
            var result = last; last = null; return result;
        }
        finally { last?.Dispose(); }
    }
    private void Finish() => FinishCore();
    private async void FinishCore()
    {
        if (finished || finishing || !ready) return;
        finishing = true; held = false; samples.Stop(); release.Stop(); automaticCancellation?.Cancel();
        try
        {
            if (firstGesture is not null) await firstGesture;
            if (automaticTask is not null) await automaticTask;
            if (processingTask is not null) await processingTask;
            if (!sizeLimit)
            {
                using var final = await SettledFrame(lifetime.Token);
                await Append(final, lifetime.Token);
            }
            if ((hasGap || composer.HasEstimates) && System.Windows.MessageBox.Show(this, "La unión final no es segura o contiene avances estimados. ¿Guardar lo capturado para revisarlo? No se añaden zonas que no se hayan capturado.", "Clipsa · Revisar captura", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
            { selectionFrame?.SetGap(true); status.Text = "Vuelve al último tramo y continúa; o cancela."; samples.Start(); return; }
            using var image = composer.Snapshot();
            ResultPath = CaptureService.Save(image); Complete(true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { status.Text = "No se pudo guardar: " + ex.Message; AppDiagnostics.Log(ex, "Guardar captura ShareX"); samples.Start(); }
        finally { finishing = false; }
    }
    private void Cancel() { if (!finished && IsVisible) { ResultPath = null; lifetime.Cancel(); automaticCancellation?.Cancel(); Complete(false); } }
    private void ClearPending() { while (pending.TryDequeue(out var frame)) frame.Dispose(); }
    private async Task DisposeAfterWork()
    {
        try { if (firstGesture is not null) await firstGesture; if (automaticTask is not null) await automaticTask; if (processingTask is not null) await processingTask; }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Finalizar motor de captura"); }
        finally { composer.Dispose(); automaticCancellation?.Dispose(); lifetime.Dispose(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
}
