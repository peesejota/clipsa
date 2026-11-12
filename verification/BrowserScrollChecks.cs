using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Clipsa;

internal static class BrowserScrollChecks
{
    public static void Run()
    {
        var browser = Environment.GetEnvironmentVariable("CLIPSA_WEB_SCROLL_BROWSER") ?? @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        if (!System.IO.File.Exists(browser)) throw new Exception("Browser unavailable for web regression.");
        var id = Guid.NewGuid().ToString("N"); var title = "Clipsa-Web-Scroll-" + id;
        var start = false;
        var completed = new TaskCompletionSource<(double Offset, double Dpr)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var paragraphs = string.Join("", Enumerable.Range(0, 100).Select(i => "<p>" + i + ": " + new string((char)('a' + i % 26), 10 + i % 9) + " Un documento real con texto, espacios en blanco y longitud distinta para verificar cada tramo. " + (i * 7919) + "</p>"));
        var html = "<!doctype html><meta charset=utf-8><title>" + title + "</title><style>body{margin:0;background:white;color:#222;font:17px Arial}main{width:600px;margin:40px 80px}p{line-height:1.5;margin:0 0 24px}</style><main>" + paragraphs + "</main><script>let running=false;setInterval(async()=>{if(!running&&await(await fetch('/start')).text()==='yes'){running=true;let n=0;const timer=setInterval(()=>{scrollBy(0,30);if(++n===15){clearInterval(timer);fetch('/done',{method:'POST',body:JSON.stringify({offset:scrollY,dpr:devicePixelRatio})})}},150)}},100)</script>";
        _ = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(stop.Token);
                    using var stream = client.GetStream();
                    using var reader = new System.IO.StreamReader(stream, Encoding.UTF8, false, 4096, true);
                    var request = await reader.ReadLineAsync(stop.Token) ?? ""; var length = 0;
                    for (var line = await reader.ReadLineAsync(stop.Token); !string.IsNullOrEmpty(line); line = await reader.ReadLineAsync(stop.Token))
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(line[15..].Trim(), out length);
                    if (request.StartsWith("POST /done ") && length is > 0 and < 4096)
                    {
                        var body = new char[length]; var read = 0;
                        while (read < length) { var n = await reader.ReadAsync(body.AsMemory(read), stop.Token); if (n == 0) break; read += n; }
                        using var json = JsonDocument.Parse(new string(body, 0, read));
                        completed.TrySetResult((json.RootElement.GetProperty("offset").GetDouble(), json.RootElement.GetProperty("dpr").GetDouble()));
                    }
                    var content = Encoding.UTF8.GetBytes(request.StartsWith("GET / ") ? html : request.StartsWith("GET /start ") && Volatile.Read(ref start) ? "yes" : "no");
                    var headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, stop.Token); await stream.WriteAsync(content, stop.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { completed.TrySetException(ex); }
        });
        var info = new ProcessStartInfo(browser) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in new[] { "--user-data-dir=" + System.IO.Path.Combine(AppPaths.DataDirectory, "web-" + id), "--no-first-run", "--disable-sync", "--disable-background-networking", "--disable-background-timer-throttling", "--app=http://127.0.0.1:" + port + "/" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var diagnostics = process.StandardError.ReadToEndAsync();
        IntPtr window = IntPtr.Zero; Window? session = null; string? result = null;
        try
        {
            for (var attempt = 0; attempt < 100 && window == IntPtr.Zero; attempt++)
            {
                EnumWindows((hwnd, _) => { var text = new StringBuilder(256); GetWindowText(hwnd, text, text.Capacity); if (text.ToString().Contains(title)) window = hwnd; return true; }, IntPtr.Zero);
                Pump(100);
            }
            if (window == IntPtr.Zero)
            {
                Console.WriteLine("Browser start diagnostic: exited=" + process.HasExited + (process.HasExited ? ", code=" + process.ExitCode : ""));
                if (diagnostics.IsCompletedSuccessfully) Console.WriteLine(diagnostics.Result);
            }
            Require(window != IntPtr.Zero, "isolated browser window opened");
            SetWindowPos(window, new IntPtr(-1), 50, 80, 1000, 620, 0x10); Pump(700);
            GetClientRect(window, out var client); var origin = new NativePoint(); ClientToScreen(window, ref origin);
            var bounds = new System.Drawing.Rectangle(origin.X + 40, origin.Y + 70, client.Right - 80, client.Bottom - 100);
            var type = typeof(MainWindow).Assembly.GetType("Clipsa.ScrollCaptureWindow")!;
            session = (Window)Activator.CreateInstance(type, new object[] { bounds, 250 })!;
            session.Loaded += (_, _) => Volatile.Write(ref start, true);
            var ticksAfterDone = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                if (completed.Task.IsCompleted && ++ticksAfterDone == 5) type.GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);
                if (ticksAfterDone > 40) session.DialogResult = false;
            };
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            timeout.Tick += (_, _) => { timeout.Stop(); session.DialogResult = false; };
            timer.Start(); timeout.Start();
            try { session.ShowDialog(); } finally { timer.Stop(); timeout.Stop(); }
            result = (string?)type.GetProperty("ResultPath")!.GetValue(session);
            Require(completed.Task.IsCompletedSuccessfully && result is not null, "browser scroll captured completely");
            var movement = completed.Task.Result;
            using var image = new System.Drawing.Bitmap(result!);
            Require(Math.Abs(image.Height - (bounds.Height + movement.Offset * movement.Dpr)) <= 3, $"all real browser displacement preserved: actual={image.Height}, viewport={bounds.Height}, offset={movement.Offset}, dpr={movement.Dpr}, expected={bounds.Height + movement.Offset * movement.Dpr}");
            Console.WriteLine($"Real {System.IO.Path.GetFileName(browser)} page, white background and 15 continuous scrolls: OK ({image.Width} x {image.Height}, delta={movement.Offset} CSS px)");
        }
        finally
        {
            if (session?.IsVisible == true) session.Close();
            if (window != IntPtr.Zero) PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero);
            else if (!process.HasExited) process.Kill(entireProcessTree: true); // Only the isolated browser this test started.
            stop.Cancel(); listener.Stop(); if (result is not null) System.IO.File.Delete(result);
        }
    }
    private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate bool Callback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
}
