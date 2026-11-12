using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Clipsa;

internal static class ShortcutChecks
{
    public static void Run(bool live = true)
    {
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Clipsa-shortcuts-" + Guid.NewGuid().ToString("N")));
        var single = new HotkeySetting { Key = 0x2C, Modifiers = 0 };
        var one = new HotkeySetting { Key = 0x2C, Modifiers = 0, ChordKey = 0x31 };
        var two = new HotkeySetting { Key = 0x2C, Modifiers = 0, ChordKey = 0x32 };
        var three = new HotkeySetting { Key = 0x2C, Modifiers = 0, ChordKey = 0x33 };
        Require(SettingsStore.DescribeVirtual(one) == "Impr Pant + 1", "Spanish name");
        Require(HotkeyService.Validate(new[] { single, one, two, three }) is null, "distinct chords");
        Require(HotkeyService.Validate(new[] { one, one }) is not null, "duplicate chord rejected");
        Require(!SettingsStore.IsValid(new HotkeySetting { Key = 0x41, ChordKey = 0x31 }), "invalid prefix rejected");
        Require(!SettingsStore.IsValid(new HotkeySetting { Key = 0x2C, ChordKey = 0x34 }), "unsupported chord rejected");
        var saved = JsonSerializer.Deserialize<HotkeySetting>(JsonSerializer.Serialize(one))!;
        Require(saved.ChordKey == 0x31 && saved.Key == 0x2C, "settings roundtrip");
        var old = JsonSerializer.Deserialize<HotkeySetting>("{\"Key\":84,\"Modifiers\":3}")!;
        Require(old.ChordKey == 0 && SettingsStore.IsValid(old), "old settings compatible");
        long now = 1000;
        var tracker = new PrintScreenChordTracker(new[] { single, one, two, three }, () => now);
        Require(!tracker.Process(0x41, true, 0, out _), "ordinary typing untouched");
        Require(!tracker.Process(0x2C, true, 8, out _), "unconfigured Windows modifier untouched");
        Require(tracker.Process(0x2C, true, 0, out var match) && match is null, "prefix waits");
        Require(tracker.Process(0x2C, true, 0, out match) && match is null, "no prefix auto-repeat");
        Require(tracker.Process(0x31, true, 0, out match) && match is null, "digit consumed");
        Require(tracker.Process(0x31, true, 0, out match) && match is null, "digit repeat consumed");
        Require(tracker.Process(0x2C, false, 0, out match) && match == one, "only chord triggered on release");
        Require(tracker.Process(0x31, false, 0, out match) && match is null, "paired digit up consumed");
        Require(!tracker.Process(0x31, true, 0, out _), "digit outside gesture untouched");
        tracker.Process(0x2C, true, 0, out _); tracker.Process(0x62, true, 0, out _); tracker.Process(0x62, false, 0, out _);
        Require(tracker.Process(0x2C, false, 0, out match) && match == two, "numeric keypad");
        tracker.Process(0x2C, true, 0, out _);
        Require(tracker.Process(0x2C, false, 0, out match) && match is null, "single waits for possible sequence");
        now += PrintScreenChordTracker.SequenceTimeoutMilliseconds;
        Require(tracker.Expire(out match) && match == single && !tracker.Expire(out _), "single print screen expires exactly once");
        Require(new PrintScreenChordTracker(new[] { single }).Process(0x2C, false, 0, out match) && match == single, "release-only print screen");
        foreach (var binding in new[] { one, two, three })
        {
            tracker.Process(0x2C, true, 0, out _); tracker.Process(0x2C, false, 0, out _);
            now += 150;
            Require(tracker.Process(binding.ChordKey, true, 0, out match) && match == binding, "pulsed prefix then digit");
            Require(tracker.Process(binding.ChordKey, true, 0, out match) && match is null, "sequence suppresses digit repetition");
            Require(tracker.Process(binding.ChordKey, false, 0, out _), "sequence consumes paired release");
            now += 1000; Require(!tracker.Expire(out _), "sequence never also fires plain print screen");
            tracker.Process(0x2C, false, 0, out _);
            Require(tracker.Process(binding.ChordKey, true, 0, out match) && match == binding, "release-only laptop sequence");
            tracker.Process(binding.ChordKey, false, 0, out _);
        }
        var chordsOnly = new PrintScreenChordTracker(new[] { one, two, three }, () => now);
        chordsOnly.Process(0x2C, false, 0, out _); now += 1000;
        Require(!chordsOnly.Process(0x31, true, 0, out _) && !chordsOnly.Expire(out _), "expired prefix leaves digits untouched");
        chordsOnly.Process(0x2C, false, 0, out _);
        Require(!chordsOnly.Process(0x41, true, 0, out _) && !chordsOnly.Process(0x31, true, 0, out _), "unrelated typing cancels prefix");
        var defaults = new AppSettings();
        Require(defaults.Full.ChordKey == 0x31 && defaults.Region.ChordKey == 0x32 && defaults.Scroll.ChordKey == 0x33, "fresh defaults use print screen chords");
        Console.WriteLine("Shortcut state, repeats, duplicates, keypad, settings compatibility and untouched typing: OK");
        if (!live) return;
        LiveListener(single, one, two, three);
        LiveRecording();
    }
    private static void LiveListener(params HotkeySetting[] bindings)
    {
        using var ready = new AutoResetEvent(false);
        HotkeySetting? received = null;
        using (var listener = new PrintScreenShortcuts(bindings, key => { received = key; ready.Set(); }))
        {
            foreach (var binding in bindings)
            {
                received = null;
                KeyEvent(0x2C, false);
                if (binding.ChordKey != 0) { KeyEvent((byte)binding.ChordKey, false); KeyEvent((byte)binding.ChordKey, true); }
                KeyEvent(0x2C, true);
                Require(ready.WaitOne(2000) && received == binding, "native listener chord " + binding.ChordKey);
                Require(!ready.WaitOne(100), "native listener fires exactly once");
            }
            foreach (var binding in bindings.Where(b => b.ChordKey != 0))
            {
                foreach (var releaseOnly in new[] { false, true })
                {
                    received = null;
                    if (!releaseOnly) KeyEvent(0x2C, false);
                    KeyEvent(0x2C, true);
                    Thread.Sleep(150);
                    KeyEvent((byte)binding.ChordKey, false); KeyEvent((byte)binding.ChordKey, true);
                    Require(ready.WaitOne(2000) && received == binding, "native pulsed/release-only prefix " + binding.ChordKey);
                    Require(!ready.WaitOne(900), "sequence suppresses delayed plain capture");
                }
            }
        }
        Console.WriteLine("Real Windows listener with injected Print Screen alone, + 1/2/3 and clean disposal: OK");
    }
    private static void LiveRecording()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        var window = new SettingsWindow(new AppSettings());
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var settings = (AppSettings)typeof(SettingsWindow).GetField("settings", flags)!.GetValue(window)!;
        typeof(SettingsWindow).GetMethod("Full_Click", flags)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
        KeyEvent(0x2C, false); KeyEvent(0x33, false); KeyEvent(0x33, true); KeyEvent(0x2C, true);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
        Require(settings.Full.Key == 0x2C && settings.Full.ChordKey == 0x33, "settings selector records chord");
        typeof(SettingsWindow).GetMethod("PrintPreset_Click", flags)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
        Require(settings.Full.ChordKey == 0x31 && settings.Region.ChordKey == 0x32 && settings.Scroll.ChordKey == 0x33, "one-click presets");
        window.Close();
        LiveMainWindow(flags);
        app.Shutdown();
        Console.WriteLine("Settings selector and Print Screen preset buttons: OK");
    }
    private static void LiveMainWindow(System.Reflection.BindingFlags flags)
    {
        var main = new MainWindow();
        var settings = (AppSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(main)!;
        settings.Full = HotkeySetting.PrintScreen(1); settings.Region = HotkeySetting.PrintScreen(2); settings.Scroll = HotkeySetting.PrintScreen(3);
        settings.KeepInTray = false; settings.CheckUpdatesOnStartup = false;
        var store = (CaptureStore)typeof(MainWindow).GetField("store", flags)!.GetValue(main)!;
        main.Show(); ScrollChecks.Pump(200);
        try
        {
            foreach (var mode in new[] { "visible", "compact", "hidden" })
            {
                if (mode == "compact") typeof(MainWindow).GetMethod("Collapse_Click", flags)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
                if (mode == "hidden") main.Hide();
                var count = store.Items.Count;
                Pulse(0x31); ScrollChecks.Pump(700);
                Require(store.Items.Count == count + 1, "actual full capture, pulsed shortcut, " + mode);
                foreach (var digit in new byte[] { 0x32, 0x33 })
                {
                    if (mode == "hidden") main.Hide();
                    var selected = false;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
                    timer.Tick += (_, _) =>
                    {
                        var overlay = Application.Current.Windows.OfType<RegionWindow>().FirstOrDefault(w => w.IsVisible);
                        if (overlay is not null) { selected = true; overlay.Close(); }
                    };
                    timer.Start();
                    try { Pulse(digit); ScrollChecks.Pump(700); }
                    finally { timer.Stop(); }
                    Require(selected, "actual region/scroll selector, pulsed shortcut, " + mode + " / " + digit);
                    ScrollChecks.Pump(100);
                }
            }
        }
        finally { main.Close(); CaptureService.Cleanup(); }
        Console.WriteLine("Real MainWindow capture and region/scroll shortcuts: visible, compact and hidden OK.");
    }
    private static void Pulse(byte digit)
    { KeyEvent(0x2C, false); KeyEvent(0x2C, true); KeyEvent(digit, false); KeyEvent(digit, true); }
    private static void KeyEvent(byte key, bool up) { keybd_event(key, 0, up ? 2u : 0u, UIntPtr.Zero); Thread.Sleep(40); }
    private static void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
