using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using Forms = System.Windows.Forms;
namespace Clipsa;
public partial class MainWindow : Window
{
    private readonly CancellationTokenSource updateCancellation = new();
    private ReleaseInfo? pendingUpdate;
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private bool checkingUpdate;
    private bool settingsOpen, applyingUpdate;
    private bool windowInitialized;
    private readonly DispatcherTimer compactHoverTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private PrintScreenShortcuts? printShortcuts;
    private const int HotkeyFull = 9001, HotkeyRegion = 9002, HotkeyScroll = 9003;
    private readonly CaptureStore store = new(); private readonly AppSettings settings = SettingsStore.Load(); private Forms.NotifyIcon? tray; private System.Drawing.Icon? trayIcon; private HwndSource? source; private System.Windows.Point dragStart; private bool collapsed; private bool exitRequested; private FrameworkElement? hoveredCard; private InsertionAdorner? insertion; private FrameworkElement? dragCard; private bool suppressCompactClick; private bool dragInProgress; private bool captureBusy; private string panelMonitor = ""; private readonly List<int> registeredHotkeys = new();
    public MainWindow() { InitializeComponent(); Title = "Clipsa"; foreach (var text in FindVisualChildren<TextBlock>(this)) if (text.Text == "CLIPSA") text.Text = "CLIPSA"; Cards.ItemsSource = store.Items; CompactCards.ItemsSource = store.Items; CompactScroll.AllowDrop = true; CompactScroll.DragOver += Compact_DragOver; CompactScroll.Drop += Compact_Drop; CompactScroll.DragLeave += (_, _) => HideInsertion(); Cards.AllowDrop = true; CardsScroll.AllowDrop = true; Cards.AddHandler(UIElement.DropEvent, new System.Windows.DragEventHandler(Cards_Drop), true); Cards.AddHandler(UIElement.DragOverEvent, new System.Windows.DragEventHandler(Cards_DragOver), true); Cards.AddHandler(UIElement.MouseMoveEvent, new System.Windows.Input.MouseEventHandler(Cards_MouseMove), true); Cards.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(CardImage_MouseDown), true); CardsScroll.Drop += Cards_Drop; CardsScroll.DragOver += Cards_DragOver; CardsScroll.DragLeave += (_, _) => HideInsertion(); store.Items.CollectionChanged += ItemsChanged; }
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject { if (root is null) yield break; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed; foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant; } }
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (windowInitialized) return; windowInitialized = true;
        compactHoverTimer.Tick += (_, _) =>
        {
            compactHoverTimer.Stop();
            if (!CompactActionsRoot.IsMouseOver && !CompactActionsRoot.IsKeyboardFocusWithin && CompactActionsPopup.PlacementTarget is not FrameworkElement { IsMouseOver: true }) CloseCompactActions();
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) CloseCompactActions(); };
        ApplyScale(); PositionPanel(); source = (HwndSource)PresentationSource.FromVisual(this)!; source.AddHook(WndProc); ApplyGlassBrush();
        RegisterHotkeys();
        using var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Clipsa;component/Brand/clipsa-icon.ico"))!.Stream; trayIcon = new System.Drawing.Icon(iconStream, 32, 32); tray = new Forms.NotifyIcon { Visible = true, Text = "Clipsa", Icon = trayIcon }; var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Abrir Clipsa", null, (_, _) => { Show(); Activate(); PositionPanel(); }); menu.Items.Add("Ajustes", null, (_, _) => ShowSettings()); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Captura completa", null, (_, _) => CaptureFull()); menu.Items.Add("Captura de región", null, (_, _) => CaptureRegion()); menu.Items.Add("Captura con scroll", null, (_, _) => CaptureScroll()); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Salir", null, (_, _) => ExitApplication()); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => TogglePanel();
        if (SettingsStore.LastLoadNotice is { } notice) StatusText.Text = notice;
        if (AppPaths.StorageNotice is { } storageNotice) StatusText.Text = storageNotice;
        if (SessionRecovery.AbandonedSessions().Any())
        {
            if ((settings.RecoverAfterCrash || SettingsStore.UsedFallbackDefaults) && System.Windows.MessageBox.Show(this, "Hay capturas de un cierre inesperado. ¿Recuperarlas en Clipsa? Si eliges No, se eliminarán.", "Clipsa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                foreach (var path in SessionRecovery.Recover()) { try { store.Add(path); } catch (Exception ex) { AppDiagnostics.Log(ex, "Cargar captura recuperada"); } }
            else SessionRecovery.DiscardAbandoned();
        }
        if (App.BackgroundLaunch) Dispatcher.BeginInvoke(() => Hide(), DispatcherPriority.ContextIdle);
        tray.BalloonTipClicked += (_, _) =>
        {
            if (pendingUpdate is not null) ShowSettings();
        };
        updateTimer.Tick += (_, _) => _ = CheckStartupUpdateAsync(); updateTimer.Start();
        _ = CheckStartupUpdateAsync();
    }
    private async Task CheckStartupUpdateAsync()
    {
        if (checkingUpdate || !settings.CheckUpdatesOnStartup || string.IsNullOrWhiteSpace(settings.UpdateFeedUrl)) return;
        checkingUpdate = true;
        try
        {
            await Task.Delay(3000, updateCancellation.Token);
            var release = await UpdateService.CheckAsync(settings.UpdateFeedUrl, updateCancellation.Token);
            if (release is null || updateCancellation.IsCancellationRequested) return;
            var alreadyNotified = pendingUpdate?.Version == release.Version;
            pendingUpdate = release;
            UpdateBadge.Visibility = Visibility.Visible;
            if (!alreadyNotified) tray?.ShowBalloonTip(10000, "Disponible Clipsa " + release.Version, "Haz clic para descargar o actualizar desde Ajustes.", Forms.ToolTipIcon.Info);
            StatusText.Text = "Disponible Clipsa " + release.Version + " · descarga desde Ajustes";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Aviso de actualización al iniciar"); }
        finally { checkingUpdate = false; }
    }
    private void RegisterHotkeys()
    {
        if (source is null) return;
        var conflicts = new List<string>();
        foreach (var entry in new[] { (HotkeyFull, settings.Full), (HotkeyRegion, settings.Region), (HotkeyScroll, settings.Scroll) })
        {
            if (entry.Item2.Key == 0) continue;
            if (entry.Item2.Key == 0x2C) continue;
            if (Native.RegisterHotKey(source.Handle, entry.Item1, entry.Item2.Modifiers | 0x4000, entry.Item2.Key)) registeredHotkeys.Add(entry.Item1);
            else conflicts.Add(entry.Item2.Display);
        }
        var printBindings = new[] { settings.Full, settings.Region, settings.Scroll }.Where(k => k.Key == 0x2C).ToArray();
        if (printBindings.Length > 0)
        {
            try { printShortcuts = new PrintScreenShortcuts(printBindings, key => Dispatcher.BeginInvoke(() => { if (printShortcuts is null || !new[] { settings.Full, settings.Region, settings.Scroll }.Contains(key)) return; if (ReferenceEquals(key, settings.Full)) CaptureFull(); else if (ReferenceEquals(key, settings.Region)) CaptureRegion(); else CaptureScroll(); })); }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Activar Impr Pant"); conflicts.Add("Impr Pant: no se pudo activar"); }
        }
        if (conflicts.Count > 0) StatusText.Text = "Atajo ocupado: " + string.Join(", ", conflicts);
    }
    private void UnregisterHotkeys() { printShortcuts?.Dispose(); printShortcuts = null; if (source is null) return; foreach (var id in registeredHotkeys) Native.UnregisterHotKey(source.Handle, id); registeredHotkeys.Clear(); }
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CloseCompactActions();
        if (!App.IsFatal && settings.KeepInTray && !exitRequested) { e.Cancel = true; Hide(); return; }
        updateCancellation.Cancel();
        updateTimer.Stop();
        UnregisterHotkeys(); tray?.Dispose(); trayIcon?.Dispose(); source?.RemoveHook(WndProc);
        if (!App.IsFatal) CaptureService.Cleanup();
    }
    private void ExitApplication() { exitRequested = true; Close(); }
    private void ExitForUpdate()
    {
        // Update confirmation and temporary-capture handling have already completed.
        // Explicit shutdown also closes hidden windows and reaches App.OnExit,
        // which releases the instance mutex. Hiding in the tray is not sufficient.
        exitRequested = true;
        System.Windows.Application.Current.Shutdown();
    }
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) { if (msg == Native.WM_HOTKEY) { switch (wParam.ToInt32()) { case HotkeyFull: CaptureFull(); break; case HotkeyRegion: CaptureRegion(); break; case HotkeyScroll: CaptureScroll(); break; } handled = true; } return IntPtr.Zero; }
    private void ApplyScale() { PanelRoot.LayoutTransform = new ScaleTransform(settings.PanelScale, settings.PanelScale); }
    private void ApplyGlassBrush()
    {
        // Alpha belongs exclusively to the background brush. All foreground elements stay opaque.
        var alpha = (byte)Math.Round(255 * (1 - Math.Clamp(settings.GlassOpacity, 0, 100) / 100.0));
        var brush = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(alpha, 54, 46, 86), 0));
        brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(alpha, 78, 48, 70), 1));
        brush.Freeze();
        GlassBackground.Background = brush;
    }
    private void PositionPanel()
    {
        CloseCompactActions();
        var screen = MonitorService.Resolve(panelMonitor.Length > 0 ? panelMonitor : settings.CaptureMonitor);
        var work = MonitorService.WorkArea(screen); var dpi = MonitorService.DpiScale(screen); var scale = settings.PanelScale;
        Width = Math.Min(1100 * scale, Math.Max(240, work.Width - 32)); Height = (collapsed ? 44 : 276) * scale;
        Left = work.Left + (work.Width - Width) / 2; Top = work.Top;
        if (new WindowInteropHelper(this).Handle != IntPtr.Zero)
        {
            var width = Math.Min(screen.WorkingArea.Width, (int)Math.Round(Width * dpi.X));
            MonitorService.PlaceWindow(this, new System.Drawing.Rectangle(screen.WorkingArea.Left + (screen.WorkingArea.Width - width) / 2, screen.WorkingArea.Top, width, (int)Math.Round(Height * dpi.Y)));
        }
    }
    private void ItemsChanged(object? s, NotifyCollectionChangedEventArgs e) { if (store.Items.Count > 0) { Show(); Activate(); PositionPanel(); StatusText.Text = $"  {store.Items.Count} captura{(store.Items.Count == 1 ? "" : "s")}"; } }
    private void CaptureFull() => RunCapture(() => CaptureService.CaptureScreen(panelMonitor));
    private void CaptureRegion() => RunCapture(() => CaptureService.CaptureRegion(panelMonitor));
    private void CaptureScroll() => RunCapture(() => CaptureService.CaptureScroll(settings.ScrollSteps, settings.ScrollDelay, settings.ScrollOverlap, panelMonitor));
    private void RunCapture(Func<string?> capture)
    {
        if (captureBusy || settingsOpen || applyingUpdate || dragInProgress) return;
        panelMonitor = MonitorService.Resolve(settings.CaptureMonitor).DeviceName;
        captureBusy = true;
        var cancellation = new CancellationTokenSource();
        CaptureCancellation preparation;
        try { preparation = new CaptureCancellation(cancellation.Cancel); }
        catch (Exception ex)
        {
            cancellation.Dispose(); captureBusy = false;
            AppDiagnostics.Log(ex, "Preparar captura cancelable"); StatusText.Text = "No se pudo preparar la captura: " + ex.Message; return;
        }
        var wasVisible = IsVisible;
        Hide();
        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                using (preparation)
                    await Task.Delay(150, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                var path = capture();
                if (path is not null) Add(path);
                else { if (wasVisible) Show(); StatusText.Text = "Captura cancelada"; }
            }
            catch (OperationCanceledException) { if (wasVisible) Show(); StatusText.Text = "Captura cancelada"; }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Capturar"); Show(); StatusText.Text = "Error: " + ex.Message; }
            finally { preparation.Dispose(); cancellation.Dispose(); captureBusy = false; PositionPanel(); }
        }, DispatcherPriority.ApplicationIdle);
    }
    private void Add(string path) => store.Add(path); private void TogglePanel() { if (IsVisible) Hide(); else { Show(); PositionPanel(); } } private void ClosePanel_Click(object sender, RoutedEventArgs e) => Close(); private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void Collapse_Click(object sender, RoutedEventArgs e) { collapsed = !collapsed; CompactScroll.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed; BrandTitle.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible; CardsScroll.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible; RopeRow.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible; RootBorder.Padding = collapsed ? new Thickness(24, 5, 24, 5) : new Thickness(24, 12, 24, 20); CollapseButton.Content = collapsed ? "⌄" : "⌃"; PositionPanel(); }
    private void ShowSettings()
    {
        if (captureBusy || settingsOpen || applyingUpdate || dragInProgress) return;
        settingsOpen = true;
        (ReleaseInfo Release, string Package)? update = null;
        UnregisterHotkeys();
        try
        {
            var dialog = new SettingsWindow(settings); if (IsVisible) dialog.Owner = this; else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowDialog(); update = dialog.ReadyUpdate;
            panelMonitor = MonitorService.Resolve(settings.CaptureMonitor).DeviceName; ApplyScale(); ApplyGlassBrush(); PositionPanel();
        }
        finally { settingsOpen = false; RegisterHotkeys(); }
        if (update is { } ready) ApplyUpdate(ready.Release, ready.Package);
    }
    private void ApplyUpdate(ReleaseInfo release, string package)
    {
        applyingUpdate = true;
        try
        {
            if (store.Items.Count > 0)
            {
                var decision = System.Windows.MessageBox.Show(this, "Hay capturas temporales. ¿Guardar todas antes de actualizar?\nSí: elegir carpeta y guardarlas. No: descartarlas. Cancelar: seguir en Clipsa.", "Clipsa · Actualizar", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (decision == MessageBoxResult.Cancel) return;
                if (decision == MessageBoxResult.Yes)
                {
                    using var folder = new Forms.FolderBrowserDialog { Description = "Guardar las capturas antes de actualizar", SelectedPath = AppPaths.ResolveSaveFolder(settings.SaveFolder) };
                    if (folder.ShowDialog() != Forms.DialogResult.OK) return;
                    foreach (var capture in store.Items)
                    {
                        var name = Path.Combine(folder.SelectedPath, capture.Name);
                        if (File.Exists(name)) name = Path.Combine(folder.SelectedPath, Path.GetFileNameWithoutExtension(capture.Name) + "_" + Guid.NewGuid().ToString("N") + ".png");
                        File.Copy(capture.FilePath, name, false);
                    }
                }
            }
            else if (System.Windows.MessageBox.Show(this, "¿Actualizar a Clipsa " + release.Version + "? La aplicación se cerrará y volverá a abrirse. Se conservarán tus ajustes.", "Clipsa", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            UpdateInstaller.Start(release, package); ExitForUpdate();
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Preparar actualización"); System.Windows.MessageBox.Show(this, "No se pudo iniciar: " + ex.Message, "Clipsa"); }
        finally { applyingUpdate = false; }
    }
    private void Card_MouseDown(object sender, MouseButtonEventArgs e)
    {
        suppressCompactClick = false; dragCard = null;
        if (sender is not FrameworkElement card || card.Tag is not CaptureItem) return;
        var node = e.OriginalSource as DependencyObject;
        while (node is not null && node != card)
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase) return;
            node = VisualTreeHelper.GetParent(node);
        }
        CloseCompactActions();
        dragStart = e.GetPosition(this); dragCard = card; card.CaptureMouse();
    }
    private void Card_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement card && card.IsMouseCaptured) card.ReleaseMouseCapture();
        dragCard = null;
    }
    private void HorizontalWheel(object sender, MouseWheelEventArgs e) { if (sender is ScrollViewer viewer) { viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta); e.Handled = true; } }
    private void Compact_Click(object sender, MouseButtonEventArgs e)
    {
        var clicked = !suppressCompactClick && !dragInProgress && ReferenceEquals(dragCard, sender) && (e.GetPosition(this) - dragStart).Length < 8;
        Card_MouseUp(sender, e);
        if (clicked && sender is FrameworkElement element && element.Tag is CaptureItem item) { OpenAnnotation(item); e.Handled = true; }
    }
    private int CompactIndex(double x) { for (var i = 0; i < store.Items.Count; i++) if (CompactCards.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement c && x < c.TranslatePoint(new System.Windows.Point(c.ActualWidth / 2, 0), CompactScroll).X) return i; return store.Items.Count; }
    private void Compact_DragOver(object s, System.Windows.DragEventArgs e) { if (e.Data.GetDataPresent(typeof(CaptureItem))) { e.Effects = System.Windows.DragDropEffects.Move; var x = e.GetPosition(CompactScroll).X; ScrollNearEdge(CompactScroll, x); ShowInsertionAt(CompactScroll, CompactCards, CompactIndex(x)); e.Handled = true; } }
    private void Compact_Drop(object s, System.Windows.DragEventArgs e) { if (e.Data.GetData(typeof(CaptureItem)) is CaptureItem item) { var index = CompactIndex(e.GetPosition(CompactScroll).X); var from = store.Items.IndexOf(item); if (from < 0) return; if (index > from) index--; if (index != from) store.Items.Move(from, Math.Clamp(index, 0, store.Items.Count - 1)); HideInsertion(); e.Handled = true; } }
    private void CardImage_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount != 2) return; var node = e.OriginalSource as DependencyObject; while (node is not null) { if (node is System.Windows.Controls.Image image && image.DataContext is CaptureItem item) { OpenAnnotation(item); e.Handled = true; return; } node = System.Windows.Media.VisualTreeHelper.GetParent(node); } }
    private void OpenAnnotation(CaptureItem item)
    {
        if (captureBusy) return; captureBusy = true;
        try { var editor = new AnnotationWindow(item.FilePath) { Owner = this }; if (editor.ShowDialog() == true) store.Refresh(item); }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Abrir editor"); System.Windows.MessageBox.Show(this, "No se pudo abrir la captura: " + ex.Message, "Clipsa"); }
        finally { captureBusy = false; }
    }
    private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) { if (sender is FrameworkElement card && card.Tag is CaptureItem) { card.RenderTransformOrigin = new System.Windows.Point(.5, .5); card.RenderTransform = new ScaleTransform(1.045, 1.045); card.Opacity = .98; System.Windows.Controls.Panel.SetZIndex(card, 5); } }
    private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { if (sender is FrameworkElement card && card.Tag is CaptureItem) { card.RenderTransform = null; card.Opacity = 1; System.Windows.Controls.Panel.SetZIndex(card, 0); } }
    private void Cards_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) { var node = e.OriginalSource as DependencyObject; FrameworkElement? card = null; while (node is not null) { if (node is FrameworkElement candidate && candidate.Tag is CaptureItem) { card = candidate; break; } node = System.Windows.Media.VisualTreeHelper.GetParent(node); } if (card == hoveredCard) return; if (hoveredCard is not null) { hoveredCard.RenderTransform = null; hoveredCard.Opacity = 1; System.Windows.Controls.Panel.SetZIndex(hoveredCard, 0); } hoveredCard = card; if (hoveredCard is not null) { hoveredCard.RenderTransformOrigin = new System.Windows.Point(.5, .5); hoveredCard.RenderTransform = new ScaleTransform(1.045, 1.045); hoveredCard.Opacity = .98; System.Windows.Controls.Panel.SetZIndex(hoveredCard, 5); } }
    private void Card_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (dragInProgress || !ReferenceEquals(dragCard, sender) || e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement card || card.Tag is not CaptureItem item) return;
        if ((e.GetPosition(this) - dragStart).Length < 8) return;
        StartCardDrag(card, item);
    }
    private void StartCardDrag(FrameworkElement card, CaptureItem item)
    {
        CloseCompactActions();
        suppressCompactClick = true; dragInProgress = true; dragCard = null; card.ReleaseMouseCapture();
        var data = CreateDragData(item);
        var oldTransform = card.RenderTransform;
        try
        {
            card.Opacity = .65; card.RenderTransformOrigin = new System.Windows.Point(.5, .5); card.RenderTransform = new ScaleTransform(1.08, 1.08);
            StatusText.Text = "Arrastrando…";
            System.Windows.DragDrop.DoDragDrop(card, data, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);
        }
        catch (Exception ex) { StatusText.Text = "No se pudo arrastrar: " + ex.Message; }
        finally { card.Opacity = 1; card.RenderTransform = oldTransform; dragInProgress = false; HideInsertion(); }
    }
    private static System.Windows.DataObject CreateDragData(CaptureItem item)
    {
        var data = new System.Windows.DataObject();
        data.SetData(System.Windows.DataFormats.FileDrop, new[] { item.FilePath });
        data.SetData(typeof(CaptureItem), item);
        // Explorer should export a copy while internal targets explicitly request Move.
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)));
        return data;
    }
    private void Cards_DragOver(object sender, System.Windows.DragEventArgs e) { if (!e.Data.GetDataPresent(typeof(CaptureItem))) return; e.Effects = System.Windows.DragDropEffects.Move; var point = e.GetPosition(CardsScroll); ScrollNearEdge(CardsScroll, point.X); ShowInsertionAt(CardsScroll, Cards, FindDropIndex(point.X)); e.Handled = true; }
    private int FindDropIndex(double x) { for (var i = 0; i < store.Items.Count; i++) if (Cards.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container) { var origin = container.TranslatePoint(new System.Windows.Point(0, 0), CardsScroll); if (x < origin.X + container.ActualWidth / 2) return i; } return store.Items.Count; }
    private static void ScrollNearEdge(ScrollViewer viewport, double x) { if (x < 22) viewport.ScrollToHorizontalOffset(viewport.HorizontalOffset - 12); else if (x > viewport.ActualWidth - 22) viewport.ScrollToHorizontalOffset(viewport.HorizontalOffset + 12); }
    private void ShowInsertionAt(ScrollViewer viewport, ItemsControl cards, int target)
    {
        var layer = AdornerLayer.GetAdornerLayer(viewport); if (layer is null || cards.Items.Count == 0) return;
        var index = Math.Min(target, cards.Items.Count - 1);
        if (cards.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container) return;
        var origin = container.TranslatePoint(new System.Windows.Point(0, 0), viewport);
        var x = target >= cards.Items.Count ? origin.X + container.ActualWidth : origin.X;
        if (insertion is not null && insertion.AdornedElement != viewport) { HideInsertion(); insertion = null; }
        insertion ??= new InsertionAdorner(viewport);
        var existing = layer.GetAdorners(viewport);
        if (existing is null || !existing.Contains(insertion)) layer.Add(insertion);
        insertion.X = Math.Clamp(x, 2, Math.Max(2, viewport.ActualWidth - 2)); insertion.InvalidateVisual();
    }
    private void HideInsertion() { if (insertion is not null) { var layer = AdornerLayer.GetAdornerLayer(insertion.AdornedElement); layer?.Remove(insertion); } }
    private void Cards_Drop(object sender, System.Windows.DragEventArgs e) { if (!e.Data.GetDataPresent(typeof(CaptureItem))) return; var item = (CaptureItem)e.Data.GetData(typeof(CaptureItem))!; var point = e.GetPosition(CardsScroll); var target = FindDropIndex(point.X); var from = store.Items.IndexOf(item); if (from >= 0) { if (target > from) target--; if (target != from) store.Items.Move(from, Math.Max(0, target)); } HideInsertion(); e.Handled = true; }
    private static CaptureItem Item(object sender) => (CaptureItem)((FrameworkElement)sender).Tag;
    private void Open_Click(object sender, RoutedEventArgs e) => OpenCapture(Item(sender));
    private void Annotate_Click(object sender, RoutedEventArgs e) => EditCapture(Item(sender));
    private void Copy_Click(object sender, RoutedEventArgs e) => CopyCapture(Item(sender));
    private void Save_Click(object sender, RoutedEventArgs e) => SaveCapture(Item(sender));
    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteCapture(Item(sender));
    private void OpenCapture(CaptureItem item) => ExecuteAction(() => Process.Start(new ProcessStartInfo { FileName = item.FilePath, UseShellExecute = true }), "Abrir captura");
    private void EditCapture(CaptureItem item) => ExecuteAction(() => OpenAnnotation(item), "Editar captura");
    private void CopyCapture(CaptureItem item) => ExecuteAction(() => { using var bitmap = new Bitmap(item.FilePath); Forms.Clipboard.SetImage(bitmap); StatusText.Text = "Copiada"; }, "Copiar captura");
    private void SaveCapture(CaptureItem item) => ExecuteAction(() =>
    {
        var requestedFolder = AppPaths.ResolveSaveFolder(settings.SaveFolder);
        var folder = Directory.Exists(requestedFolder) ? requestedFolder : AppPaths.DefaultSaveFolder;
        Directory.CreateDirectory(folder);
        using var dialog = new Forms.SaveFileDialog { InitialDirectory = folder, FileName = item.Name, Filter = "Imagen PNG (*.png)|*.png", AddExtension = true };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
        if (!string.Equals(Path.GetFullPath(item.FilePath), Path.GetFullPath(dialog.FileName), StringComparison.OrdinalIgnoreCase))
        {
            var temp = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(item.FilePath, temp); File.Move(temp, dialog.FileName, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        StatusText.Text = "Guardada";
    }, "Guardar captura");
    private void DeleteCapture(CaptureItem item) => ExecuteAction(() => { if (File.Exists(item.FilePath)) File.Delete(item.FilePath); store.Items.Remove(item); }, "Eliminar captura");
    private void Compact_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => ShowCompactActions(sender);
    private void Compact_GotFocus(object sender, KeyboardFocusChangedEventArgs e) { if (Keyboard.IsKeyDown(Key.Tab)) ShowCompactActions(sender); }
    private void ShowCompactActions(object sender)
    {
        if (!collapsed || captureBusy || settingsOpen || dragInProgress || Mouse.LeftButton == MouseButtonState.Pressed || sender is not FrameworkElement { Tag: CaptureItem item } card) return;
        compactHoverTimer.Stop();
        CompactActionsPopup.PlacementTarget = card; CompactActionsPopup.DataContext = item; CompactActionsPopup.IsOpen = true;
    }
    private void Compact_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { compactHoverTimer.Stop(); compactHoverTimer.Start(); }
    private void CompactActions_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => compactHoverTimer.Stop();
    private void CloseCompactActions() { compactHoverTimer.Stop(); CompactActionsPopup.IsOpen = false; CompactActionsPopup.DataContext = null; CompactActionsPopup.PlacementTarget = null; }
    private void CompactActions_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        CloseCompactActions(); Focus(); e.Handled = true;
    }
    private void CompactAction_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender; var item = Item(sender); var action = button.CommandParameter as string;
        CloseCompactActions(); e.Handled = true;
        switch (action)
        {
            case "Edit": EditCapture(item); break; case "Save": SaveCapture(item); break;
            case "Copy": CopyCapture(item); break; case "Open": OpenCapture(item); break; case "Delete": DeleteCapture(item); break;
        }
    }
    private void ExecuteAction(Action action, string context)
    {
        try { action(); }
        catch (Exception ex) { AppDiagnostics.Log(ex, context); StatusText.Text = context + ": " + ex.Message; System.Windows.MessageBox.Show(this, context + ": " + ex.Message, "Clipsa", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
