using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipsa;

internal static class CompactActionsChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "Clipsa-compact-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", root);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        var path = Path.Combine(root, "qa-capture.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[] { 220, 80, 120, 255, 50, 200, 180, 255, 190, 110, 230, 255, 80, 220, 130, 255 }, 8)));
        using (var stream = File.Create(path)) encoder.Save(stream);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var main = new MainWindow();
        var settings = (AppSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(main)!;
        settings.KeepInTray = false; settings.CheckUpdatesOnStartup = false; settings.SaveFolder = root;
        var store = (CaptureStore)typeof(MainWindow).GetField("store", flags)!.GetValue(main)!;
        store.Add(path); main.Show();
        typeof(MainWindow).GetMethod("Collapse_Click", flags)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
        ScrollChecks.Pump(180);
        var cards = (ItemsControl)main.FindName("CompactCards");
        var card = Descendants<Border>(cards).First(b => b.Tag is CaptureItem);
        var popup = (Popup)main.FindName("CompactActionsPopup");
        GetCursorPos(out var original);
        try
        {
            void Hover()
            {
                var outside = main.PointToScreen(new Point(10, 10)); SetCursorPos((int)outside.X, (int)outside.Y); ScrollChecks.Pump(50);
                var point = card.PointToScreen(new Point(card.ActualWidth / 2, card.ActualHeight / 2)); SetCursorPos((int)point.X, (int)point.Y);
                ScrollChecks.Pump(220);
                Require(popup.IsOpen && popup.DataContext == store.Items[0], "hover opens actions for the correct screenshot");
            }
            Hover();
            var buttons = Descendants<Button>(popup.Child).ToArray();
            Require(buttons.Length == 5 && buttons.All(b => b.Tag == store.Items[0] && b.ToolTip is not null), "edit/save/copy/open/delete are bound and described");
            var edit = buttons.Single(b => (string)b.CommandParameter == "Edit");
            var editPoint = edit.PointToScreen(new Point(edit.ActualWidth / 2, edit.ActualHeight / 2));
            SetCursorPos((int)editPoint.X, (int)editPoint.Y); ScrollChecks.Pump(250);
            Require(popup.IsOpen, "actions remain open while moving from thumbnail to toolbar");
            var sawEditor = false;
            var closeEditor = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            closeEditor.Tick += (_, _) =>
            {
                var editor = app.Windows.OfType<AnnotationWindow>().FirstOrDefault(w => w.IsVisible);
                if (editor is not null) { sawEditor = true; editor.Close(); }
            };
            closeEditor.Start();
            try { Click(); ScrollChecks.Pump(250); }
            finally { closeEditor.Stop(); }
            Require(sawEditor && !popup.IsOpen && File.Exists(path), $"toolbar pencil opens editor and preserves capture (editor={sawEditor}, popup={popup.IsOpen}, file={File.Exists(path)})");
            Hover();
            var save = Descendants<Button>(popup.Child).Single(b => (string)b.CommandParameter == "Save");
            var saveDialog = false;
            // Close only the common file dialog belonging to this QA process.
            var dismiss = Task.Run(() =>
            {
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    var foreground = GetForegroundWindow(); GetWindowThreadProcessId(foreground, out var processId);
                    var name = new StringBuilder(64); GetClassName(foreground, name, name.Capacity);
                    if (processId == Environment.ProcessId && name.ToString() == "#32770")
                    { saveDialog = true; PostMessage(foreground, 0x10, IntPtr.Zero, IntPtr.Zero); return; }
                    Thread.Sleep(50);
                }
            });
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            dismiss.GetAwaiter().GetResult();
            Require(saveDialog && File.Exists(path), "save action opens the real file picker; cancelling preserves source");
            Hover();
            var delete = Descendants<Button>(popup.Child).Single(b => (string)b.CommandParameter == "Delete");
            var deletePoint = delete.PointToScreen(new Point(delete.ActualWidth / 2, delete.ActualHeight / 2));
            SetCursorPos((int)deletePoint.X, (int)deletePoint.Y); Click(); ScrollChecks.Pump(150);
            Require(store.Items.Count == 0 && !File.Exists(path) && !popup.IsOpen, "toolbar cross removes the selected QA screenshot");
            Console.WriteLine("Compact hover panel, pointer handoff, five action bindings, real editor/save dialog and delete: OK.");
        }
        finally { mouse_event(4, 0, 0, 0, UIntPtr.Zero); SetCursorPos(original.X, original.Y); main.Close(); CaptureService.Cleanup(); app.Shutdown(); }
    }
    private static void Click() { mouse_event(2, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(25); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T found) yield return found; foreach (var nested in Descendants<T>(child)) yield return nested; } }
    private static void Require(bool value, string name) { if (!value) throw new Exception(name); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
}
