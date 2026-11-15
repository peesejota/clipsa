using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Orientation = System.Windows.Controls.Orientation;

namespace Clipsa;

public partial class MainWindow
{
    private readonly DispatcherTimer dockTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private bool autoHidden;
    private long edgeEntered, dockLeft;
    private Rectangle fullDockBounds, hiddenDockBounds;
    private bool SideDock => settings.PanelPlacement is "Left" or "Right" || settings.PanelVertical;
    private void InitializeDock()
    {
        collapsed = settings.PanelCollapsed;
        autoHidden = collapsed && settings.AutoHideCollapsed;
        SideCompactCards.ItemsSource = store.Items;
        SideCompactCards.ItemTemplate = CompactCards.ItemTemplate;
        SideCompactScroll.AllowDrop = true;
        SideCompactScroll.DragOver += Compact_DragOver; SideCompactScroll.Drop += Compact_Drop;
        SideCompactScroll.DragLeave += (_, _) => HideInsertion();
        dockTimer.Tick += (_, _) => DockTick(); dockTimer.Start();
        Activated += (_, _) => { if (autoHidden && !captureBusy && !settingsOpen) { autoHidden = false; edgeEntered = dockLeft = 0; PositionPanel(); } };
        EventHandler displayChanged = (_, _) => Dispatcher.BeginInvoke(() => { if (IsVisible && !captureBusy) PositionPanel(); });
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += displayChanged;
        Closed += (_, _) => { dockTimer.Stop(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= displayChanged; };
    }
    private void ApplyDockLayout()
    {
        var vertical = SideDock; var smallSide = vertical && collapsed;
        MainHeader.Visibility = smallSide ? Visibility.Collapsed : Visibility.Visible;
        SideCompactPanel.Visibility = smallSide ? Visibility.Visible : Visibility.Collapsed;
        CompactScroll.Visibility = collapsed && !vertical ? Visibility.Visible : Visibility.Collapsed;
        BrandTitle.Visibility = !collapsed ? Visibility.Visible : Visibility.Collapsed;
        CardsScroll.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        RopeRow.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ShelfLayout.RowDefinitions[0].Height = new GridLength(smallSide ? 0 : 30);
        ShelfLayout.RowDefinitions[1].Height = new GridLength(collapsed ? 0 : 22);
        MainHeader.ColumnDefinitions[0].Width = new GridLength(vertical ? 0 : 120);
        MainHeader.ColumnDefinitions[2].Width = new GridLength(vertical ? 90 : 120);
        RootBorder.Padding = smallSide ? new Thickness(4) : collapsed ? new Thickness(24, 5, 24, 5) : vertical ? new Thickness(12) : new Thickness(24, 12, 24, 20);
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, vertical ? Orientation.Vertical : Orientation.Horizontal);
        Cards.ItemsPanel = new ItemsPanelTemplate(factory);
        CardsScroll.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
        CardsScroll.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        CollapseButton.Content = collapsed ? "⌄" : "⌃";
        RootBorder.Visibility = autoHidden ? Visibility.Collapsed : Visibility.Visible;
        AutoHideHandle.Visibility = autoHidden ? Visibility.Visible : Visibility.Collapsed;
        GlassBackground.Visibility = autoHidden ? Visibility.Collapsed : Visibility.Visible;
    }
    private void PlaceDock()
    {
        CloseCompactActions();
        var device = settings.PanelMonitor.Length > 0 ? settings.PanelMonitor : panelMonitor.Length > 0 ? panelMonitor : settings.CaptureMonitor;
        var screen = MonitorService.Resolve(device); var dpi = MonitorService.DpiScale(screen); var work = screen.WorkingArea;
        ApplyDockLayout();
        var w = SideDock ? (collapsed ? 56 : 260) : 1100;
        var h = SideDock ? (collapsed ? Math.Min(560, Math.Max(150, store.Items.Count * 32 + 110)) : 760) : collapsed ? 44 : 276;
        fullDockBounds = DockGeometry.Place(work, (int)Math.Round(w * settings.PanelScale * dpi.X), (int)Math.Round(h * settings.PanelScale * dpi.Y), settings.PanelPlacement, settings.PanelX, settings.PanelY);
        var edge = DockGeometry.Edge(settings.PanelPlacement, fullDockBounds, work);
        hiddenDockBounds = DockGeometry.Tab(fullDockBounds, work, edge, (int)Math.Round(6 * dpi.X), (int)Math.Round(90 * dpi.Y));
        var bounds = autoHidden ? hiddenDockBounds : fullDockBounds;
        MinWidth = autoHidden ? 1 : SideDock && collapsed ? 40 : Math.Min(240, work.Width / dpi.X);
        Width = bounds.Width / dpi.X; Height = bounds.Height / dpi.Y;
        Left = bounds.Left / dpi.X; Top = bounds.Top / dpi.Y;
        if (new WindowInteropHelper(this).Handle != IntPtr.Zero) MonitorService.PlaceWindow(this, bounds);
    }
    private void DockTick()
    {
        if (!IsVisible || !collapsed || !settings.AutoHideCollapsed || captureBusy || settingsOpen || applyingUpdate || dragInProgress) { edgeEntered = dockLeft = 0; return; }
        var point = Forms.Cursor.Position; var now = Environment.TickCount64;
        if (autoHidden)
        {
            var hot = hiddenDockBounds; hot.Inflate(7, 7);
            var screen = Forms.Screen.FromRectangle(fullDockBounds); var physical = screen.Bounds;
            var edge = DockGeometry.Edge(settings.PanelPlacement, fullDockBounds, screen.WorkingArea);
            var edgeHit = physical.Contains(point) && (edge switch
            {
                "Top" => point.Y <= physical.Top + 3 && point.X >= hot.Left && point.X <= hot.Right,
                "Bottom" => point.Y >= physical.Bottom - 4 && point.X >= hot.Left && point.X <= hot.Right,
                "Left" => point.X <= physical.Left + 3 && point.Y >= hot.Top && point.Y <= hot.Bottom,
                _ => point.X >= physical.Right - 4 && point.Y >= hot.Top && point.Y <= hot.Bottom
            });
            if (!hot.Contains(point) && !edgeHit) { edgeEntered = 0; return; }
            if (edgeEntered == 0) edgeEntered = now;
            if (now - edgeEntered >= settings.EdgeRevealDelay) { autoHidden = false; edgeEntered = dockLeft = 0; PlaceDock(); }
        }
        else
        {
            var hot = fullDockBounds; hot.Inflate(14, 14);
            if (hot.Contains(point) || CompactActionsPopup.IsOpen) { dockLeft = 0; return; }
            if (dockLeft == 0) dockLeft = now;
            if (now - dockLeft >= 800) { autoHidden = true; dockLeft = 0; PlaceDock(); }
        }
    }
    private void RevealDock() { autoHidden = false; edgeEntered = dockLeft = 0; Show(); PositionPanel(); }
    private void SaveDock()
    {
        try { SettingsStore.Save(settings); }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Guardar posición de Clipsa"); StatusText.Text = "No se pudo guardar la posición: " + ex.Message; }
    }
    private void DockDrag_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || captureBusy || settingsOpen || applyingUpdate) return;
        CloseCompactActions();
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        var bounds = MonitorService.WindowBounds(this);
        var screen = Forms.Screen.FromRectangle(bounds); var work = screen.WorkingArea;
        settings.PanelVertical = SideDock;
        settings.PanelPlacement = "Custom"; settings.PanelMonitor = screen.DeviceName;
        settings.PanelX = Math.Clamp((double)(bounds.Left - work.Left) / Math.Max(1, work.Width - bounds.Width), 0, 1);
        settings.PanelY = Math.Clamp((double)(bounds.Top - work.Top) / Math.Max(1, work.Height - bounds.Height), 0, 1);
        autoHidden = false; SaveDock(); PositionPanel(); e.Handled = true;
    }
}
