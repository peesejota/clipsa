using System.Windows;
using System.Windows.Input;
using System.IO;
using System.Text.Json;
using Forms = System.Windows.Forms;

namespace Clipsa;

public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    private readonly AppSettings original;
    private HotkeySetting? recording;
    private PrintScreenShortcuts? printRecorder;
    private sealed record MonitorChoice(string Device, string Label);
    public SettingsWindow(AppSettings value)
    {
        InitializeComponent(); original = value; settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(value))!;
        MonitorBox.DisplayMemberPath = "Label"; MonitorBox.SelectedValuePath = "Device";
        MonitorBox.Items.Add(new MonitorChoice("", "Automático · pantalla donde está el ratón"));
        foreach (var screen in Forms.Screen.AllScreens) MonitorBox.Items.Add(new MonitorChoice(screen.DeviceName, screen.DeviceName + (screen.Primary ? " · principal" : "") + " · " + screen.Bounds.Width + " × " + screen.Bounds.Height));
        Refresh();
        Closed += (_, _) => CancelRecording();
    }

    private void Refresh()
    {
        FullButton.Content = settings.Full.Display; RegionButton.Content = settings.Region.Display; ScrollButton.Content = settings.Scroll.Display;
        DelayBox.Text = settings.ScrollDelay.ToString(); ScaleBox.Text = Math.Round(settings.PanelScale * 100).ToString(); GlassSlider.Value = settings.GlassOpacity;
        FolderBox.Text = settings.SaveFolder; KeepTrayBox.IsChecked = settings.KeepInTray; StartupBox.IsChecked = settings.StartWithWindows; RecoveryBox.IsChecked = settings.RecoverAfterCrash;
        MonitorBox.SelectedValue = settings.CaptureMonitor; if (MonitorBox.SelectedIndex < 0) MonitorBox.SelectedIndex = 0;
        UpdateFeedBox.Text = settings.UpdateFeedUrl;
        StartupUpdatesBox.IsChecked = settings.CheckUpdatesOnStartup;
        ModeText.Text = "Clipsa " + UpdateService.CurrentVersion.ToString(3) + (AppPaths.IsPortable ? " · Portable" : " · Instalada");
        StorageText.Text = AppPaths.DataDirectory;
    }

    private void Start(HotkeySetting target)
    {
        CancelRecording(); recording = target; Keyboard.Focus(this);
        try
        {
            var choices = Enumerable.Range(0, 16).SelectMany(modifiers => Enumerable.Range(0, 4).Select(n => new HotkeySetting { Key = 0x2C, ChordKey = n == 0 ? 0 : (uint)(0x30 + n), Modifiers = (uint)modifiers })).ToArray();
            printRecorder = new PrintScreenShortcuts(choices, key => Dispatcher.BeginInvoke(() =>
            {
                if (recording is null) return;
                recording.Key = key.Key; recording.Modifiers = key.Modifiers; recording.ChordKey = key.ChordKey;
                recording.Display = SettingsStore.DescribeVirtual(recording); CancelRecording();
            }));
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Elegir Impr Pant"); System.Windows.MessageBox.Show(this, "No se pudo escuchar Impr Pant. Utiliza el botón de combinaciones o elige otro atajo.", "Clipsa"); }
        if (target == settings.Full) FullButton.Content = "Pulsa una tecla · Esc cancela";
        else if (target == settings.Region) RegionButton.Content = "Pulsa una tecla · Esc cancela";
        else ScrollButton.Content = "Pulsa una tecla · Esc cancela";
    }
    private void CancelRecording()
    {
        recording = null;
        printRecorder?.Dispose(); printRecorder = null;
        FullButton.Content = settings.Full.Display; RegionButton.Content = settings.Region.Display; ScrollButton.Content = settings.Scroll.Display;
    }
    private void Full_Click(object s, RoutedEventArgs e) => Start(settings.Full);
    private void Region_Click(object s, RoutedEventArgs e) => Start(settings.Region);
    private void Scroll_Click(object s, RoutedEventArgs e) => Start(settings.Scroll);
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (recording is null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Snapshot) { e.Handled = true; return; }
        if (key == Key.Escape) { CancelRecording(); e.Handled = true; return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key is Key.Delete or Key.Back && Keyboard.Modifiers == ModifierKeys.None) { recording.Key = 0; recording.Modifiers = 0; recording.Display = "Sin asignar"; }
        else { recording.Key = (uint)KeyInterop.VirtualKeyFromKey(key); recording.Modifiers = SettingsStore.ModifierValue(Keyboard.Modifiers); recording.Display = SettingsStore.Describe(key, Keyboard.Modifiers); }
        recording.ChordKey = 0;
        CancelRecording(); e.Handled = true;
    }
    private void Folder_Click(object s, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = Directory.Exists(FolderBox.Text) ? FolderBox.Text : AppPaths.BaseDirectory, Description = "Elige dónde guardar las capturas" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) FolderBox.Text = dialog.SelectedPath;
    }
    private void PrintPreset_Click(object s, RoutedEventArgs e)
    {
        CancelRecording();
        foreach (var pair in new[] { (settings.Full, 1u), (settings.Region, 2u), (settings.Scroll, 3u) })
        {
            pair.Item1.Key = 0x2C; pair.Item1.Modifiers = 0; pair.Item1.ChordKey = 0x30 + pair.Item2;
            pair.Item1.Display = SettingsStore.DescribeVirtual(pair.Item1);
        }
        FullButton.Content = settings.Full.Display; RegionButton.Content = settings.Region.Display; ScrollButton.Content = settings.Scroll.Display;
    }

    private void Save_Click(object s, RoutedEventArgs e)
    {
        if (recording is not null) { System.Windows.MessageBox.Show(this, "Termina de elegir el atajo antes de guardar.", "Clipsa"); return; }
        var conflict = HotkeyService.Validate(new[] { settings.Full, settings.Region, settings.Scroll });
        if (conflict is not null) { System.Windows.MessageBox.Show(this, conflict, "Clipsa"); return; }
        if (!int.TryParse(DelayBox.Text, out var delay) || delay < 100 || delay > 400 || !double.TryParse(ScaleBox.Text, out var scale) || scale < 70 || scale > 140)
        { System.Windows.MessageBox.Show(this, "El intervalo debe estar entre 100 y 400 ms y el tamaño entre 70 y 140 %.", "Clipsa"); return; }
        settings.ScrollDelay = delay; settings.PanelScale = scale / 100; settings.GlassOpacity = (int)GlassSlider.Value;
        settings.UpdateFeedUrl = UpdateFeedBox.Text.Trim();
        try { if (settings.UpdateFeedUrl.Length > 0) _ = UpdateService.ResolveFeed(settings.UpdateFeedUrl); }
        catch (ArgumentException ex) { System.Windows.MessageBox.Show(this, ex.Message, "Clipsa"); return; }
        settings.CheckUpdatesOnStartup = StartupUpdatesBox.IsChecked == true;
        if (settings.CheckUpdatesOnStartup && settings.UpdateFeedUrl.Length == 0)
        { System.Windows.MessageBox.Show(this, "Introduce el repositorio o manifiesto antes de activar los avisos.", "Clipsa"); return; }
        try
        {
            settings.SaveFolder = Path.GetFullPath(FolderBox.Text);
            Directory.CreateDirectory(settings.SaveFolder);
            using (new FileStream(Path.Combine(settings.SaveFolder, Guid.NewGuid() + ".probe"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            settings.KeepInTray = KeepTrayBox.IsChecked == true; settings.StartWithWindows = StartupBox.IsChecked == true; settings.RecoverAfterCrash = RecoveryBox.IsChecked == true;
            settings.CaptureMonitor = (MonitorBox.SelectedItem as MonitorChoice)?.Device ?? "";
            var oldStartup = StartupManager.GetCurrent();
            try { StartupManager.Apply(settings.StartWithWindows); SettingsStore.Save(settings); }
            catch { StartupManager.Restore(oldStartup); throw; }
            original.Full = settings.Full; original.Region = settings.Region; original.Scroll = settings.Scroll;
            original.ScrollDelay = settings.ScrollDelay; original.PanelScale = settings.PanelScale; original.GlassOpacity = settings.GlassOpacity;
            original.SaveFolder = settings.SaveFolder; original.KeepInTray = settings.KeepInTray; original.StartWithWindows = settings.StartWithWindows;
            original.RecoverAfterCrash = settings.RecoverAfterCrash; original.CaptureMonitor = settings.CaptureMonitor; original.UpdateFeedUrl = settings.UpdateFeedUrl;
            original.CheckUpdatesOnStartup = settings.CheckUpdatesOnStartup;
            DialogResult = true;
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Guardar ajustes"); System.Windows.MessageBox.Show(this, "No se pudieron guardar los ajustes: " + ex.Message, "Clipsa"); }
    }
    private void Cancel_Click(object s, RoutedEventArgs e) { DialogResult = false; }
    private void Storage_Click(object s, RoutedEventArgs e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.DataDirectory) { UseShellExecute = true });
    private async void Updates_Click(object s, RoutedEventArgs e)
    {
        var url = UpdateFeedBox.Text.Trim();
        if (url.Length == 0) { System.Windows.MessageBox.Show(this, "El canal aún no está configurado. Introduce la URL HTTPS del manifiesto de la web o del repositorio de Clipsa.", "Clipsa"); return; }
        UpdatesButton.IsEnabled = false;
        try
        {
            var release = await UpdateService.CheckAsync(url);
            if (release is null) { System.Windows.MessageBox.Show(this, "Tienes la versión más reciente disponible en este canal.", "Clipsa"); return; }
            if (System.Windows.MessageBox.Show(this, "Disponible Clipsa " + release.Version + ". ¿Abrir la página de descarga? Guarda tus capturas pendientes antes de instalar.", "Clipsa", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(release.DownloadUrl) { UseShellExecute = true });
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Consultar actualizaciones"); System.Windows.MessageBox.Show(this, "No se pudo comprobar la actualización: " + ex.Message, "Clipsa"); }
        finally { UpdatesButton.IsEnabled = true; }
    }
}
