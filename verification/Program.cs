using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clipsa;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--public-channel-only")) { PublicChannelChecks.RunAsync().GetAwaiter().GetResult(); return; }
        if (args.Contains("--updates-only")) { UpdateChecks.Run(); return; }
        if (args.Contains("--shortcuts-only")) { ShortcutChecks.Run(); return; }
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Clipsa-checks-" + Guid.NewGuid().ToString("N")));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        if (args.Contains("--scroll-only")) { CheckScroll(); ScrollChecks.Run(); CaptureService.Cleanup(); return; }
        if (Environment.GetEnvironmentVariable("CLIPSA_BROWSER_QA") == "only")
        {
            var fixture = System.IO.Path.Combine(AppPaths.DataDirectory, "Clipsa-browser-qa.png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(MakeSample()));
            using (var stream = System.IO.File.Create(fixture)) encoder.Save(stream);
            var preview = new BitmapImage(); using (var stream = System.IO.File.OpenRead(fixture)) { preview.BeginInit(); preview.CacheOption = BitmapCacheOption.OnLoad; preview.StreamSource = stream; preview.EndInit(); } preview.Freeze();
            try { BrowserDropChecks.Run(fixture, preview); } finally { System.IO.File.Delete(fixture); CaptureService.Cleanup(); }
            return;
        }
        CheckStorageAndHotkeys();
        var window = new MainWindow();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var settings = (AppSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(window)!;
        var panel = (FrameworkElement)window.FindName("PanelRoot");
        var root = (Border)window.FindName("RootBorder");
        var background = (Border)window.FindName("GlassBackground");
        var apply = typeof(MainWindow).GetMethod("ApplyGlassBrush", flags)!;
        var sample = new Image { Width = 48, Height = 48, Source = MakeSample(), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(300, 90, 0, 0) };
        ((Grid)panel).Children.Add(sample);
        byte[]? firstPixel = null;
        foreach (var transparency in new[] { 0, 68, 100 })
        {
            settings.GlassOpacity = transparency; apply.Invoke(window, null);
            panel.Measure(new Size(1100, 276)); panel.Arrange(new Rect(0, 0, 1100, 276)); panel.UpdateLayout();
            var rendered = new RenderTargetBitmap(1100, 276, 96, 96, PixelFormats.Pbgra32); rendered.Render(panel);
            var foreground = Pixel(rendered, 320, 110);
            firstPixel ??= foreground;
            Require(foreground.SequenceEqual(firstPixel) && foreground[3] == 255, "foreground remains fully opaque");
            var backPixel = Pixel(rendered, 8, 100);
            var expectedAlpha = (byte)Math.Round(255 * (1 - transparency / 100.0));
            Require(Math.Abs(backPixel[3] - expectedAlpha) <= 1, "background alpha follows transparency setting");
            Require(root.Opacity == 1 && window.Opacity == 1 && window.AllowsTransparency, "per-pixel transparency without global fading");
            Console.WriteLine($"glass={transparency}% backgroundAlpha={backPixel[3]} captureAlpha={foreground[3]} OK");
        }
        var initial = settings.Full.Display;
        var dialog = new SettingsWindow(settings);
        typeof(SettingsWindow).GetMethod("Full_Click", flags)!.Invoke(dialog, new object[] { dialog, new RoutedEventArgs() });
        Require(settings.Full.Display == initial, "unsaved shortcuts do not mutate live settings");
        Console.WriteLine("Unsaved settings isolated: OK");
        CheckEditor(flags);
        CheckScroll();
        CheckIcons();
        CheckLiveScroll();
        Console.WriteLine("All checks passed.");
        CaptureService.Cleanup();
    }

    static BitmapSource MakeSample() => BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 37, 93, 219, 255 }, 4);
    static byte[] Pixel(BitmapSource bitmap, int x, int y) { var data = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), data, 4, 0); return data; }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void CheckStorageAndHotkeys()
    {
        var settings = new AppSettings { GlassOpacity = 12 };
        SettingsStore.Save(settings); settings.GlassOpacity = 73; SettingsStore.Save(settings);
        System.IO.File.WriteAllText(AppPaths.SettingsFile, "{ corrupt");
        Require(SettingsStore.Load().GlassOpacity == 12 && SettingsStore.LastLoadNotice is not null, "corrupt settings recovered from atomic backup");
        System.IO.File.WriteAllText(AppPaths.SettingsFile, "null");
        Require(SettingsStore.Load().GlassOpacity == 12 && !SettingsStore.UsedFallbackDefaults, "null settings rejected in favor of valid backup");
        SettingsStore.Save(new AppSettings());
        var duplicate = HotkeyService.Validate(new[] { new HotkeySetting { Key = 0x78, Modifiers = 3 }, new HotkeySetting { Key = 0x78, Modifiers = 3 } });
        Require(duplicate is not null, "duplicate hotkey rejected");
        Require(HotkeyService.Validate(new[] { new HotkeySetting { Key = 0, Modifiers = 0 } }) is null, "disabled hotkey accepted");
        var native = typeof(MainWindow).Assembly.GetType("Clipsa.Native")!;
        var register = native.GetMethod("RegisterHotKey")!; var unregister = native.GetMethod("UnregisterHotKey")!;
        var own = (bool)register.Invoke(null, new object[] { IntPtr.Zero, 32000, 3u, 0x79u })!;
        try { if (own) Require(HotkeyService.Validate(new[] { new HotkeySetting { Key = 0x79, Modifiers = 3 } }) is not null, "occupied OS hotkey detected"); }
        finally { if (own) unregister.Invoke(null, new object[] { IntPtr.Zero, 32000 }); }
        AppPaths.InitializeSession();
        var active = System.IO.Path.Combine(AppPaths.SessionsDirectory, Guid.NewGuid().ToString("N"));
        var abandoned = System.IO.Path.Combine(AppPaths.SessionsDirectory, Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(active); System.IO.Directory.CreateDirectory(abandoned);
        using (var lease = new System.IO.FileStream(System.IO.Path.Combine(active, ".active"), System.IO.FileMode.CreateNew, System.IO.FileAccess.ReadWrite, System.IO.FileShare.Read))
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(active, "keep.png"), "active-test");
            System.IO.File.WriteAllText(System.IO.Path.Combine(abandoned, "recover.png"), "abandoned-test");
            Require(!SessionRecovery.AbandonedSessions().Contains(active), "active session protected");
            var recovered = SessionRecovery.Recover();
            Require(recovered.Count == 1 && System.IO.File.Exists(recovered[0]) && !System.IO.Directory.Exists(abandoned), "abandoned session recovered locally");
            CaptureService.Cleanup();
            Require(System.IO.File.Exists(System.IO.Path.Combine(active, "keep.png")), "cleanup affects own session only");
        }
        System.IO.Directory.Delete(active, true);
        Console.WriteLine("Atomic settings backup, shortcut validation/conflicts and isolated crash recovery: OK");
    }
    static void CheckEditor(BindingFlags flags)
    {
        foreach (var point in new[] { new Point(400, 300), new Point(10, 20), new Point(400, 20), new Point(10, 300) })
        {
            var rect = CropGeometry.Select(new Point(100, 100), point, new Size(640, 480), 0);
            Require(rect.Contains(new Point(100, 100)) && rect.Contains(point), "free crop begins at anchor in every direction");
        }
        foreach (var ratio in new[] { 16.0 / 9, 9.0 / 16, 1, 4.0 / 3, 1.5 })
        {
            var rect = CropGeometry.Select(new Point(100, 100), new Point(1000, 800), new Size(640, 480), ratio);
            Require(Math.Abs(rect.Width / rect.Height - ratio) < .0001 && rect.Right <= 640 && rect.Bottom <= 480, "fixed crop ratio and bounds");
        }
        var bytes = new byte[240 * 160 * 4];
        for (var y = 0; y < 160; y++) for (var x = 0; x < 240; x++) { var i = (y * 240 + x) * 4; bytes[i] = (byte)x; bytes[i + 1] = (byte)y; bytes[i + 2] = 200; bytes[i + 3] = 255; }
        var source = BitmapSource.Create(240, 160, 96, 96, PixelFormats.Bgra32, null, bytes, 240 * 4);
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Clipsa-editor-check-" + Guid.NewGuid() + ".png");
        try
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = System.IO.File.Create(file)) encoder.Save(stream);
            CheckHistory(file, flags);
            CheckLiveEditorKeys(file, flags);
            var editor = new AnnotationWindow(file, promptOnDiscard: false);
            var surface = (FrameworkElement)editor.FindName("DrawingSurface");
            surface.Measure(new Size(240, 160)); surface.Arrange(new Rect(0, 0, 240, 160)); surface.UpdateLayout();
            var render = typeof(AnnotationWindow).GetMethod("RenderImage", flags)!;
            var original = (BitmapSource)render.Invoke(editor, null)!;
            Require(original.PixelWidth == 240 && original.PixelHeight == 160, "editor preserves native pixel dimensions");
            typeof(AnnotationWindow).GetField("crop", flags)!.SetValue(editor, new Rect(37, 23, 113, 67));
            var selection = (System.Windows.Shapes.Rectangle)editor.FindName("CropSelection"); selection.Visibility = Visibility.Visible;
            var cropped = (BitmapSource)render.Invoke(editor, null)!;
            Require(cropped.PixelWidth == 113 && cropped.PixelHeight == 67 && Pixel(cropped, 0, 0).SequenceEqual(Pixel(original, 37, 23)), "crop starts at selected pixel, excludes guides");
            typeof(AnnotationWindow).GetField("crop", flags)!.SetValue(editor, Rect.Empty);
            typeof(AnnotationWindow).GetMethod("Rotate_Click", flags)!.Invoke(editor, new object[] { editor, new RoutedEventArgs() });
            surface.Measure(new Size(160, 240)); surface.Arrange(new Rect(0, 0, 160, 240)); surface.UpdateLayout();
            var rotated = (BitmapSource)render.Invoke(editor, null)!;
            Require(rotated.PixelWidth == 160 && rotated.PixelHeight == 240, "90 degree rotation swaps dimensions");
            Require(Pixel(rotated, 159 - 23, 37).SequenceEqual(Pixel(original, 37, 23)), "rotation preserves pixel positions");
            var compactWindow = new MainWindow();
            var compactCards = (ItemsControl)compactWindow.FindName("CompactCards");
            var preview = new BitmapImage(); using (var stream = System.IO.File.OpenRead(file)) { preview.BeginInit(); preview.CacheOption = BitmapCacheOption.OnLoad; preview.StreamSource = stream; preview.EndInit(); } preview.Freeze();
            compactCards.ItemsSource = new[] { new CaptureItem { FilePath = file, Preview = preview } };
            typeof(MainWindow).GetMethod("Collapse_Click", flags)!.Invoke(compactWindow, new object[] { compactWindow, new RoutedEventArgs() });
            var compactPanel = (FrameworkElement)compactWindow.FindName("PanelRoot");
            compactPanel.Measure(new Size(1100, 44)); compactPanel.Arrange(new Rect(0, 0, 1100, 44)); compactPanel.UpdateLayout();
            Require(((FrameworkElement)compactWindow.FindName("CompactScroll")).Visibility == Visibility.Visible && compactCards.ItemContainerGenerator.ContainerFromIndex(0) is not null, "compact bar generates interactive thumbnail");
            CheckLiveDrag(file, preview, flags);
            if (Environment.GetEnvironmentVariable("CLIPSA_BROWSER_QA") == "1") BrowserDropChecks.Run(file, preview);
            Console.WriteLine("Compact thumbnail generation: OK");
            Console.WriteLine("Free crop, fixed ratios, native resolution, guide exclusion and 90 degree rotation: OK");
        }
        finally { System.IO.File.Delete(file); }
    }
    static void CheckScroll()
    {
        var random = new Random(42); const int width = 120, height = 180, offset = 57;
        var page = new byte[width * (height + offset) * 4]; random.NextBytes(page);
        for (var i = 3; i < page.Length; i += 4) page[i] = 255;
        var first = page.Take(width * height * 4).ToArray();
        var second = page.Skip(width * offset * 4).Take(width * height * 4).ToArray();
        Require(ScrollStitcher.FindOffset(first, second, width, height) == offset, "scroll detects actual overlap");
        Require(ScrollStitcher.Difference(first, first, width, height) == 0, "identical frame is not appended");
        var unrelated = new byte[first.Length]; random.NextBytes(unrelated);
        Require(ScrollStitcher.FindOffset(first, unrelated, width, height) == 0, "unrelated scroll views rejected");
        const int stickyHeight = 240, top = 24, bottom = 16, body = stickyHeight - top - bottom;
        var document = new byte[width * 600 * 4]; random.NextBytes(document);
        var sticky = new byte[width * stickyHeight * 4]; random.NextBytes(sticky);
        byte[] StickyFrame(int start) { var frame = (byte[])sticky.Clone(); Array.Copy(document, start * width * 4, frame, top * width * 4, body * width * 4); return frame; }
        var stickyFirst = StickyFrame(100); var stickySecond = StickyFrame(150);
        var down = ScrollStitcher.Match(stickyFirst, stickySecond, width, stickyHeight);
        var up = ScrollStitcher.Match(stickySecond, stickyFirst, width, stickyHeight);
        Require(down.Kind == ScrollStitcher.MatchKind.Matched && down.Offset == 50 && down.FixedTop == top && down.FixedBottom == bottom, "fixed header/footer correctly excluded");
        Require(up.Kind == ScrollStitcher.MatchKind.Matched && up.Offset == -50, "reverse scroll matched");
        var periodic = new byte[width * height * 4]; var periodicNext = new byte[periodic.Length];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { var i = (y * width + x) * 4; periodic[i] = (byte)(y % 5 * 40); periodicNext[i] = (byte)((y + 2) % 5 * 40); periodic[i + 3] = periodicNext[i + 3] = 255; }
        Require(ScrollStitcher.Match(periodic, periodicNext, width, height).Kind == ScrollStitcher.MatchKind.Ambiguous, "repeated content rejected as ambiguous");
        Console.WriteLine("Manual scroll matching and rejection of unrelated frames: OK");
        Console.WriteLine("Fixed headers/footers, reverse scroll and ambiguous repeated content: OK");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetCursorPos(out MousePosition point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr WindowFromPoint(MousePosition point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct MousePosition { public int X; public int Y; }
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T found) yield return found; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    static void CheckLiveDrag(string file, BitmapImage preview, BindingFlags flags)
    {
        var window = new MainWindow();
        ((AppSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(window)!).KeepInTray = true;
        ((ItemsControl)window.FindName("CompactCards")).ItemsSource = new[] { new CaptureItem { FilePath = file, Preview = preview } };
        window.Show();
        typeof(MainWindow).GetMethod("Collapse_Click", flags)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
        window.Left = 20; window.Top = 220; window.Width = 700; window.UpdateLayout();
        var source = Descendants<Border>((DependencyObject)window.FindName("CompactCards")).First(b => b.Tag is CaptureItem);
        var target = new Border { Background = Brushes.DarkGreen, AllowDrop = true, Child = new TextBlock { Text = "Clipsa drag verification", Foreground = Brushes.White } };
        var host = new Window { Content = target, Width = 450, Height = 150, Left = 20, Top = 420, Topmost = true };
        var frame = new System.Windows.Threading.DispatcherFrame();
        var dropped = false; var captured = false;
        target.Drop += (_, e) =>
        {
            dropped = e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1 && paths[0] == file;
            e.Effects = DragDropEffects.Copy; e.Handled = true; frame.Continue = false;
        };
        host.Show(); host.UpdateLayout(); ScrollChecks.Pump(250);
        var start = source.PointToScreen(new Point(source.ActualWidth / 2, source.ActualHeight / 2));
        var finish = target.PointToScreen(new Point(90, 70));
        GetCursorPos(out var cursor);
        var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; };
        timeout.Start();
        var input = Task.Run(() =>
        {
            Thread.Sleep(350);
            window.Dispatcher.Invoke(() => { start = source.PointToScreen(new Point(source.ActualWidth / 2, source.ActualHeight / 2)); finish = target.PointToScreen(new Point(90, 70)); });
            SetCursorPos((int)start.X, (int)start.Y); Thread.Sleep(100); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(150); window.Dispatcher.Invoke(() => captured = source.IsMouseCaptured);
            SetCursorPos((int)start.X + 22, (int)start.Y + 70); Thread.Sleep(200);
            SetCursorPos((int)finish.X, (int)finish.Y); Thread.Sleep(200); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
        });
        try
        {
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            if (input.IsFaulted) throw input.Exception!;
            Require(captured && dropped && System.IO.File.Exists(file), $"compact thumbnail captures mouse and exports a file after pointer leaves its bounds (captured={captured}, dropped={dropped})");
            Console.WriteLine("Live compact drag to external file-drop target, source preserved: OK");
        }
        finally
        {
            timeout.Stop(); mouse_event(4, 0, 0, 0, UIntPtr.Zero); SetCursorPos(cursor.X, cursor.Y);
            host.Close(); window.Hide();
            typeof(MainWindow).GetMethod("UnregisterHotkeys", flags)!.Invoke(window, null);
            (typeof(MainWindow).GetField("tray", flags)!.GetValue(window) as IDisposable)?.Dispose();
        }
    }
    static void CheckLiveEditorKeys(string file, BindingFlags flags)
    {
        var editor = new AnnotationWindow(file, promptOnDiscard: false) { Width = 760, Height = 500, Left = 300, Top = 270, Topmost = true, WindowStartupLocation = WindowStartupLocation.Manual };
        editor.Show(); editor.Activate(); editor.UpdateLayout();
        var surface = (FrameworkElement)editor.FindName("DrawingSurface");
        typeof(AnnotationWindow).GetMethod("Tool_Click", flags)!.Invoke(editor, new object[] { new Button { Tag = "Crop" }, new RoutedEventArgs() });
        var first = surface.PointToScreen(new Point(20, 20)); var last = surface.PointToScreen(new Point(100, 90));
        var frame = new System.Windows.Threading.DispatcherFrame();
        var undoSelection = false; var redoSelection = false; var undoApplied = false;
        GetCursorPos(out var cursor);
        void Shortcut(byte key) { keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero); }
        var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; }; timeout.Start();
        var input = Task.Run(() =>
        {
            Thread.Sleep(350);
            editor.Dispatcher.Invoke(() => { first = surface.PointToScreen(new Point(20, 20)); last = surface.PointToScreen(new Point(100, 90)); });
            SetCursorPos((int)first.X, (int)first.Y); Thread.Sleep(100);
            editor.Dispatcher.Invoke(() => Require(WindowFromPoint(new MousePosition { X=(int)first.X, Y=(int)first.Y }) == new System.Windows.Interop.WindowInteropHelper(editor).Handle, "editor is visible at test pointer"));
            mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(120); SetCursorPos((int)last.X, (int)last.Y); Thread.Sleep(120); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(200); Shortcut(0x5A); Thread.Sleep(200);
            editor.Dispatcher.Invoke(() => undoSelection = ((Rect)typeof(AnnotationWindow).GetField("crop", flags)!.GetValue(editor)!).IsEmpty);
            Shortcut(0x59); Thread.Sleep(200);
            editor.Dispatcher.Invoke(() =>
            {
                var selected = (Rect)typeof(AnnotationWindow).GetField("crop", flags)!.GetValue(editor)!;
                redoSelection = !selected.IsEmpty && Math.Abs(selected.Width - 80) <= 2 && Math.Abs(selected.Height - 70) <= 2;
                typeof(AnnotationWindow).GetMethod("ApplyCrop_Click", flags)!.Invoke(editor, new object[] { editor, new RoutedEventArgs() });
            });
            Thread.Sleep(150); Shortcut(0x5A); Thread.Sleep(200);
            editor.Dispatcher.Invoke(() => { undoApplied = surface.Width == 240 && surface.Height == 160; frame.Continue = false; });
        });
        try
        {
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            if (input.IsFaulted) throw input.Exception!;
            Require(undoSelection && redoSelection && undoApplied, "actual Ctrl+Z/Ctrl+Y undo and redo mouse crop and restore applied crop");
            Console.WriteLine("Live mouse crop, Ctrl+Z, Ctrl+Y and undo applied crop: OK");
        }
        finally { timeout.Stop(); mouse_event(4, 0, 0, 0, UIntPtr.Zero); SetCursorPos(cursor.X, cursor.Y); editor.Close(); }
    }
    static void CheckHistory(string file, BindingFlags flags)
    {
        var editor = new AnnotationWindow(file, promptOnDiscard: false);
        var ink = (Canvas)editor.FindName("Ink");
        var surface = (FrameworkElement)editor.FindName("DrawingSurface");
        void Layout() { surface.Measure(new Size(surface.Width, surface.Height)); surface.Arrange(new Rect(0, 0, surface.Width, surface.Height)); surface.UpdateLayout(); }
        object? Call(string name, params object[] args) => typeof(AnnotationWindow).GetMethod(name, flags)!.Invoke(editor, args);
        void Undo() => ApplicationCommands.Undo.Execute(null, editor);
        void Redo() => ApplicationCommands.Redo.Execute(null, editor);
        Require(editor.InputBindings.OfType<KeyBinding>().Any(k => k.Key == System.Windows.Input.Key.Z && k.Modifiers == System.Windows.Input.ModifierKeys.Control), "Ctrl+Z is bound");
        Call("AddTextAnnotation", new Point(10, 12), "Texto de prueba", 28.0);
        Require(ink.Children.Count == 1 && ApplicationCommands.Undo.CanExecute(null, editor), "text addition enters undo history");
        Undo(); Require(ink.Children.Count == 0, "undo removes text");
        Redo(); Require(ink.Children.Count == 1 && ((TextBlock)ink.Children[0]).Text == "Texto de prueba", "redo restores text");
        var before = Call("CaptureState")!;
        var stroke = new System.Windows.Shapes.Polyline { Stroke = Brushes.Red, StrokeThickness = 10, Points = new PointCollection { new Point(20, 100), new Point(220, 100) } };
        ink.Children.Add(stroke); Call("CommitState", before); Layout();
        var render = typeof(AnnotationWindow).GetMethod("RenderImage", flags)!;
        var painted = (BitmapSource)render.Invoke(editor, null)!;
        before = Call("CaptureState")!; Call("EraseAt", new Point(120, 100)); Call("CommitState", before); Layout();
        var erased = (BitmapSource)render.Invoke(editor, null)!;
        Require(Pixel(erased, 120, 100)[2] == 200 && Pixel(erased, 30, 100)[2] == 255 && Pixel(erased, 210, 100)[2] == 255, "eraser removes only the touched section");
        Undo(); Layout(); Require(Pixel((BitmapSource)render.Invoke(editor, null)!, 120, 100).SequenceEqual(Pixel(painted, 120, 100)), "undo restores erased pixels");
        Redo(); Layout(); Require(Pixel((BitmapSource)render.Invoke(editor, null)!, 120, 100).SequenceEqual(Pixel(erased, 120, 100)), "redo reapplies partial erasure");
        before = Call("CaptureState")!;
        typeof(AnnotationWindow).GetField("crop", flags)!.SetValue(editor, new Rect(15, 10, 150, 110));
        ((System.Windows.Shapes.Rectangle)editor.FindName("CropSelection")).Visibility = Visibility.Visible;
        Call("CommitState", before);
        Call("ApplyCrop_Click", editor, new RoutedEventArgs()); Layout();
        Require(surface.Width == 150 && surface.Height == 110, "crop applied");
        Undo(); Layout(); Require(surface.Width == 240 && surface.Height == 160 && ink.Children.Count == 2, "undo applied crop restores size and annotations");
        Undo(); Require(((Rect)typeof(AnnotationWindow).GetField("crop", flags)!.GetValue(editor)!).IsEmpty, "undo crop selection");
        Redo(); Redo(); Layout(); Require(surface.Width == 150 && surface.Height == 110, "redo selection and applied crop");
        Call("Rotate_Click", editor, new RoutedEventArgs()); Require(surface.Width == 110 && surface.Height == 150, "rotation enters history");
        Undo(); Require(surface.Width == 150 && surface.Height == 110, "undo rotation");
        Redo(); Require(surface.Width == 110 && surface.Height == 150, "redo rotation");
        Undo(); Call("AddTextAnnotation", new Point(5, 5), "Nueva rama", 16.0);
        Require(!ApplicationCommands.Redo.CanExecute(null, editor), "new edit clears redo branch");
        Call("Clear_Click", editor, new RoutedEventArgs()); Require(ink.Children.Count == 0, "clear annotations");
        Undo(); Require(ink.Children.Count == 1, "undo clear annotations");
        Console.WriteLine("Undo/redo commands for text, strokes, partial erasure, crop selection, applied crop, rotation and history branching: OK");
    }
    static void CheckIcons()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Clipsa;component/Brand/clipsa-icon.ico"))!.Stream;
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Require(decoder.Frames.Count == 9, "icon has nine embedded sizes");
        foreach (var frame in decoder.Frames)
        {
            var rgba = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var p = Pixel(rgba, frame.PixelWidth / 4, frame.PixelHeight / 4);
            Require(p[3] > 200 && p[0] != p[1] && p[1] != p[2], "each icon size has opaque colored pixels");
        }
        Console.WriteLine("Embedded color icon at nine resolutions: OK");
    }
    static void CheckLiveScroll()
    {
        var bytes = new byte[240 * 800 * 4]; new Random(7).NextBytes(bytes);
        for (var i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
        var image = new Image { Source = BitmapSource.Create(240, 800, 96, 96, PixelFormats.Bgra32, null, bytes, 960), Width = 240, Height = 800, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        var scroller = new ScrollViewer { Content = image, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var host = new Window { Title = "Clipsa scroll verification", Content = scroller, Width = 260, Height = 260, Left = 20, Top = 300, Topmost = true };
        host.Show(); host.UpdateLayout(); ScrollChecks.Pump(250);
        var a = scroller.PointToScreen(new Point(10, 10)); var b = scroller.PointToScreen(new Point(210, 190));
        var bounds = new System.Drawing.Rectangle((int)a.X, (int)a.Y, (int)(b.X - a.X), (int)(b.Y - a.Y));
        var type = typeof(MainWindow).Assembly.GetType("Clipsa.ScrollCaptureWindow")!;
        var session = (Window)Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.Instance, null, new object[] { bounds, 100 }, null)!;
        var step = 0;
        var ticker = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        ticker.Tick += async (_, _) =>
        {
            step++;
            if (step <= 3) scroller.ScrollToVerticalOffset(step * 40);
            else
            {
                ticker.Stop(); type.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(session, null);
                for (var wait = 0; wait < 100 && session.IsVisible && type.GetProperty("ResultPath")!.GetValue(session) is null; wait++) await Task.Delay(20);
                if (type.GetProperty("ResultPath")!.GetValue(session) is null)
                {
                    var state = (TextBlock)type.GetField("status", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
                    Console.WriteLine("Live scroll diagnostic: " + state.Text + "; viewport offset=" + scroller.VerticalOffset);
                    var previous = (byte[]?)type.GetField("previous", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session);
                    using var live = (System.Drawing.Bitmap)type.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(session, null)!;
                    live.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clipsa-live-final.png"));
                    if (previous is not null)
                    {
                        var pixels = ScrollStitcher.Pixels(live); var best = Enumerable.Range(0, bounds.Height - 40).Select(x => (Offset: x, Error: ScrollStitcher.Difference(previous, pixels, bounds.Width, bounds.Height, x))).OrderBy(x => x.Error).First();
                        Console.WriteLine("Best live overlap: " + best);
                    }
                    var frames = (System.Collections.IEnumerable)type.GetField("frames", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
                    var n = 0; foreach (var frame in frames) { var bmp = (System.Drawing.Bitmap)frame.GetType().GetField("Item1")!.GetValue(frame)!; bmp.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clipsa-live-" + n++ + ".png")); }
                    session.DialogResult = false;
                }
            }
        };
        ticker.Start();
        string? result = null;
        try
        {
            session.ShowDialog();
            result = (string?)type.GetProperty("ResultPath")!.GetValue(session);
            Require(result is not null, "live manual scroll produces an image");
            using var stream = System.IO.File.OpenRead(result!);
            var stitched = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var scale = PresentationSource.FromVisual(host)!.CompositionTarget!.TransformToDevice.M22;
            Require(stitched.PixelWidth == bounds.Width && Math.Abs(stitched.PixelHeight - (bounds.Height + 120 * scale)) <= 2, "live scrolling produces correctly stitched height");
            Console.WriteLine("Live manual scroll session with three controlled displacements: OK (" + stitched.PixelWidth + " × " + stitched.PixelHeight + ")");
        }
        finally { ticker.Stop(); host.Close(); if (result is not null) System.IO.File.Delete(result); }
    }
}
