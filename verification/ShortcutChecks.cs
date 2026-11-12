using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Clipsa;

internal static class ShortcutChecks
{
    public static void Run()
    {
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
        var tracker = new PrintScreenChordTracker(new[] { single, one, two, three });
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
        Require(tracker.Process(0x2C, false, 0, out match) && match == single, "single print screen");
        Require(new PrintScreenChordTracker(new[] { single }).Process(0x2C, false, 0, out match) && match == single, "release-only print screen");
        Console.WriteLine("Shortcut state, repeats, duplicates, keypad, settings compatibility and untouched typing: OK");
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
        }
        Console.WriteLine("Real Windows listener with injected Print Screen alone, + 1/2/3 and clean disposal: OK");
    }
    private static void LiveRecording()
    {
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Clipsa-shortcuts-" + Guid.NewGuid().ToString("N")));
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
        window.Close(); app.Shutdown();
        Console.WriteLine("Settings selector and Print Screen preset buttons: OK");
    }
    private static void KeyEvent(byte key, bool up) { keybd_event(key, 0, up ? 2u : 0u, UIntPtr.Zero); Thread.Sleep(40); }
    private static void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
