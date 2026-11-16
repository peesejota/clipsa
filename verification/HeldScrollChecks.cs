using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Clipsa;

internal static class HeldScrollChecks
{
    private sealed record HostState(int X, int Y, int Width, int Height, double Offset, double Scale, long Handle, bool Arrow, double Target);
    public static void Run(bool keyFirst = false, bool cancel = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "Clipsa-held-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", root);
        var report = Path.Combine(root, "target.json");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--guided-scroll-host"); start.ArgumentList.Add(report);
        using var host = Process.Start(start)!;
        for (var n = 0; !File.Exists(report) && n < 100 && !host.HasExited; n++) Thread.Sleep(50);
        if (!File.Exists(report)) throw new Exception("Held scroll QA target did not start.");
        HostState Read() { using var stream = new FileStream(report, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return JsonSerializer.Deserialize<HostState>(stream)!; }
        var initial = Read(); var hostHandle = new IntPtr(initial.Handle);
        GetWindowThreadProcessId(hostHandle, out var owner); if (owner != host.Id) throw new Exception("QA window ownership mismatch.");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        var type = typeof(MainWindow).Assembly.GetType("Clipsa.ScrollCaptureWindow")!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var bounds = new System.Drawing.Rectangle(initial.X, initial.Y, initial.Width, initial.Height);
        var sampleBounds = bounds; sampleBounds.Inflate(3, 3);
        GetCursorPos(out var original);
        SetForegroundWindow(hostHandle); SetWindowPos(hostHandle, new IntPtr(-1), 0, 0, 0, 0, 0x13); Thread.Sleep(150); DwmFlush();
        if (GetAncestor(WindowFromPoint(new NativePoint { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 }), 2) != hostHandle) throw new Exception("QA target is occluded before selection.");
        using var before = new System.Drawing.Bitmap(sampleBounds.Width, sampleBounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(before)) graphics.CopyFromScreen(sampleBounds.Location, System.Drawing.Point.Empty, sampleBounds.Size);
        byte[]? expected = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        var stage = 0; var timedOut = false; Exception? failure = null; var passedThrough = false; var visibleFrame = false;
        byte[]? baseline = null; string? result = null;
        double beforeSecond = 0;
        void KeyDown()
        {
            keybd_event(0x28, 0x50, 1, UIntPtr.Zero); Thread.Sleep(30); keybd_event(0x28, 0x50, 3, UIntPtr.Zero);
        }
        timeout.Tick += (_, _) => { timedOut = true; timeout.Stop(); keybd_event(0x1B, 1, 0, UIntPtr.Zero); keybd_event(0x1B, 1, 2, UIntPtr.Zero); };
        timer.Tick += (_, _) =>
        {
            try
            {
                var selection = app.Windows.OfType<RegionWindow>().FirstOrDefault(w => w.IsVisible);
                var session = app.Windows.Cast<Window>().FirstOrDefault(w => w.GetType() == type && w.IsVisible);
                if (stage == 0 && selection is not null)
                {
                    Batch(MouseMove(bounds.Left, bounds.Top), new NativeInput { Data = new InputData { Mouse = new MouseInput { Flags = 2 } } }); stage = 1;
                }
                else if (stage == 1)
                {
                    if (keyFirst) Batch(MouseMove(bounds.Right, bounds.Bottom), new NativeInput { Type = 1, Data = new InputData { Key = new KeyInput { VirtualKey = 0x28, Scan = 0x50, Flags = 1 } } }, new NativeInput { Type = 1, Data = new InputData { Key = new KeyInput { VirtualKey = 0x28, Scan = 0x50, Flags = 3 } } });
                    else Batch(MouseMove(bounds.Right, bounds.Bottom), new NativeInput { Data = new InputData { Mouse = new MouseInput { Flags = 0x800, MouseData = unchecked((uint)-120) } } });
                    stage = 3;
                }
                else if (stage == 3 && session is not null && type.GetProperty("InitialPixels")!.GetValue(session) is byte[] pixels)
                {
                    var selected = (System.Drawing.Rectangle)type.GetField("region", flags)!.GetValue(session)!;
                    if (Math.Abs(selected.Left - bounds.Left) > 2 || Math.Abs(selected.Top - bounds.Top) > 2 || Math.Abs(selected.Right - bounds.Right) > 2 || Math.Abs(selected.Bottom - bounds.Bottom) > 2) throw new Exception($"Selection coordinates changed during input: wanted={bounds}, actual={selected}, cursor={System.Windows.Forms.Cursor.Position}.");
                    bounds = selected;
                    using (var initialImage = before.Clone(new System.Drawing.Rectangle(bounds.X - sampleBounds.X, bounds.Y - sampleBounds.Y, bounds.Width, bounds.Height), before.PixelFormat)) expected = Clipsa.Capture.CapturePixels.Read(initialImage);
                    baseline = pixels;
                    if (!(bool)type.GetField("held", flags)!.GetValue(session)!) throw new Exception("Selection did not enter held capture mode.");
                    visibleFrame = type.GetField("selectionFrame", flags)!.GetValue(session) is Window { IsVisible: true };
                    passedThrough = GetAncestor(WindowFromPoint(new NativePoint { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 }), 2) == hostHandle;
                    SetCursorPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                    stage = 4;
                }
                else if (stage == 4 && Read().Offset > 0)
                {
                    beforeSecond = Read().Offset;
                    if (keyFirst) mouse_event(0x800, 0, 0, unchecked((uint)-120), UIntPtr.Zero); else KeyDown();
                    stage = 5;
                }
                else if (stage == 5 && Read().Arrow && Read().Offset > beforeSecond)
                {
                    if (cancel) { keybd_event(0x1B, 1, 0, UIntPtr.Zero); Thread.Sleep(30); keybd_event(0x1B, 1, 2, UIntPtr.Zero); }
                    else mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                    timer.Stop(); stage = 6;
                }
            }
            catch (Exception ex)
            {
                failure = ex; timer.Stop(); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                foreach (var window in app.Windows.Cast<Window>().ToArray()) window.Close();
            }
        };
        try
        {
            timer.Start(); timeout.Start();
            result = CaptureService.CaptureScroll(5, 250, .72, System.Windows.Forms.Screen.FromPoint(bounds.Location).DeviceName);
            if (failure is not null) throw failure;
            if (cancel)
            {
                if (timedOut || stage != 6 || result is not null || Directory.Exists(AppPaths.SessionFolder) && Directory.GetFiles(AppPaths.SessionFolder, "*.png").Length != 0 || app.Windows.Cast<Window>().Any(w => w.GetType().Name == "ScrollRegionFrame")) throw new Exception("Held cancellation left a screenshot or guide behind.");
                Console.WriteLine("Escape during held scroll cancels without a PNG or orphan guide: OK."); return;
            }
            if (timedOut || stage != 6 || result is null) throw new Exception($"Held capture did not finish on release: stage={stage}, timeout={timedOut}, result={result is not null}.");
            if (!passedThrough || !visibleFrame) throw new Exception("Guide did not remain visible and pass input to the page.");
            if (!expected.AsSpan().SequenceEqual(baseline))
            {
                var channels = new int[4]; var minX = bounds.Width; var minY = bounds.Height; var maxX = 0; var maxY = 0;
                if (expected?.Length == baseline?.Length) for (var i = 0; i < expected!.Length; i++) if (expected[i] != baseline![i]) { channels[i % 4]++; var pixel = i / 4; minX = Math.Min(minX, pixel % bounds.Width); maxX = Math.Max(maxX, pixel % bounds.Width); minY = Math.Min(minY, pixel / bounds.Width); maxY = Math.Max(maxY, pixel / bounds.Width); }
                throw new Exception($"Guide or controls contaminated initial screenshot: channels={string.Join(',', channels)}, box={minX},{minY}..{maxX},{maxY}.");
            }
            var final = Read(); using var image = new System.Drawing.Bitmap(result);
            if (Math.Abs(image.Height - bounds.Height - final.Offset * final.Scale) > 2) throw new Exception("Held capture lost part of the scroll.");
            var joined = Clipsa.Capture.CapturePixels.Read(image);
            if (!expected!.AsSpan(0, bounds.Width * 20 * 4).SequenceEqual(joined.AsSpan(0, bounds.Width * 20 * 4))) throw new Exception("Held capture lost initial rows.");
            if (app.Windows.Cast<Window>().Any(w => w.GetType().Name == "ScrollRegionFrame")) throw new Exception("Guide remained open after finishing.");
            Console.WriteLine($"Held selection started with {(keyFirst ? "arrow key" : "wheel")}, page received wheel+keyboard, guide passed through and was excluded, release saved complete {image.Width} x {image.Height}: OK.");
        }
        finally
        {
            timer.Stop(); timeout.Stop(); mouse_event(4, 0, 0, 0, UIntPtr.Zero); SetCursorPos(original.X, original.Y);
            foreach (var window in app.Windows.Cast<Window>().ToArray()) window.Close();
            PostMessage(hostHandle, 0x10, IntPtr.Zero, IntPtr.Zero); host.WaitForExit(3000);
            if (result is not null) File.Delete(result); CaptureService.Cleanup(); app.Shutdown();
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyInput Key; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }
    private static NativeInput MouseMove(int x, int y)
    {
        var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
        return new NativeInput { Data = new InputData { Mouse = new MouseInput { X = (int)Math.Round((x - screen.Left) * 65535d / Math.Max(1, screen.Width - 1)), Y = (int)Math.Round((y - screen.Top) * 65535d / Math.Max(1, screen.Height - 1)), Flags = 0xC001 } } };
    }
    private static void Batch(params NativeInput[] inputs) { if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>()) != inputs.Length) throw new Exception("QA SendInput failed."); }
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
