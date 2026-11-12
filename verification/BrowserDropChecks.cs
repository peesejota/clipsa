using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipsa;

internal static class BrowserDropChecks
{
    public static void Run(string file, BitmapImage preview)
    {
        foreach (var browser in new[] { (Name: "Edge", Path: @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"), (Name: "Chrome", Path: @"C:\Program Files\Google\Chrome\Application\chrome.exe") })
        {
            if (!System.IO.File.Exists(browser.Path)) { Console.WriteLine(browser.Name + " not installed; browser drop requires manual QA."); continue; }
            RunOne(browser.Name, browser.Path, file, preview);
        }
    }

    private static void RunOne(string name, string executable, string file, BitmapImage preview)
    {
        var id = Guid.NewGuid().ToString("N");
        var title = "Clipsa-QA-" + id;
        using var cancellation = new CancellationTokenSource();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var html = "<!doctype html><title>" + title + "</title><style>body{margin:0;background:#34274a;color:white;font:24px Arial}#drop{position:fixed;inset:0;display:grid;place-items:center;border:8px dashed #a99bff}</style><div id=drop>Clipsa: drop a screenshot here</div><script>document.addEventListener('dragover',e=>{e.preventDefault();e.dataTransfer.dropEffect='copy'});document.addEventListener('drop',async e=>{e.preventDefault();const names=[...e.dataTransfer.files].map(f=>f.name);document.getElementById('drop').textContent=names.join(', ');await fetch('/drop',{method:'POST',body:JSON.stringify(names)})});</script>";
        var server = Task.Run(async () =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    using var connection = await listener.AcceptTcpClientAsync(cancellation.Token);
                    using var stream = connection.GetStream();
                    using var reader = new System.IO.StreamReader(stream, Encoding.UTF8, false, 4096, true);
                    var request = await reader.ReadLineAsync(cancellation.Token); if (request is null) continue;
                    var length = 0;
                    for (var line = await reader.ReadLineAsync(cancellation.Token); !string.IsNullOrEmpty(line); line = await reader.ReadLineAsync(cancellation.Token))
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(line.Substring(15).Trim(), out length);
                    if (request.StartsWith("POST /drop ") && length is > 0 and < 4096)
                    {
                        var body = new char[length]; var read = 0;
                        while (read < length) { var n = await reader.ReadAsync(body.AsMemory(read), cancellation.Token); if (n == 0) break; read += n; }
                        received.TrySetResult(new string(body, 0, read));
                    }
                    var content = Encoding.UTF8.GetBytes(request.StartsWith("GET / ") ? html : "OK");
                    var headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, cancellation.Token); await stream.WriteAsync(content, cancellation.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { received.TrySetException(ex); }
        });
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var sourceWindow = new MainWindow();
        ((AppSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(sourceWindow)!).KeepInTray = true;
        ((ItemsControl)sourceWindow.FindName("CompactCards")).ItemsSource = new[] { new CaptureItem { FilePath = file, Preview = preview } };
        sourceWindow.Show(); typeof(MainWindow).GetMethod("Collapse_Click", flags)!.Invoke(sourceWindow, new object[] { sourceWindow, new RoutedEventArgs() });
        sourceWindow.Left = 20; sourceWindow.Top = 220; sourceWindow.Width = 700; sourceWindow.UpdateLayout();
        var thumbnail = Descendants<Border>((DependencyObject)sourceWindow.FindName("CompactCards")).First(b => b.Tag is CaptureItem);
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var argument in new[] { "--user-data-dir=" + System.IO.Path.Combine(AppPaths.DataDirectory, "browser-" + name + "-" + id), "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--disable-sync", "--disable-component-update", "--app=http://127.0.0.1:" + port + "/" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        IntPtr handle = IntPtr.Zero;
        var frame = new DispatcherFrame();
        GetCursorPos(out var originalCursor);
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; }; timeout.Start();
        var worker = Task.Run(() =>
        {
            for (var attempts = 0; attempts < 80 && handle == IntPtr.Zero; attempts++)
            {
                EnumWindows((window, _) => { var text = new StringBuilder(256); GetWindowText(window, text, text.Capacity); if (text.ToString().Contains(title)) handle = window; return true; }, IntPtr.Zero);
                Thread.Sleep(100);
            }
            if (handle == IntPtr.Zero) { sourceWindow.Dispatcher.Invoke(() => frame.Continue = false); return; }
            SetWindowPos(handle, new IntPtr(-1), 80, 500, 900, 350, 0x10);
            Thread.Sleep(500);
            Point start = default;
            sourceWindow.Dispatcher.Invoke(() => start = thumbnail.PointToScreen(new Point(thumbnail.ActualWidth / 2, thumbnail.ActualHeight / 2)));
            SetCursorPos((int)start.X, (int)start.Y); Thread.Sleep(100); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(150); SetCursorPos((int)start.X + 30, (int)start.Y + 90); Thread.Sleep(200);
            SetCursorPos(300, 700); Thread.Sleep(300); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
            for (var attempts = 0; attempts < 30 && !received.Task.IsCompleted; attempts++) Thread.Sleep(100);
            sourceWindow.Dispatcher.Invoke(() => frame.Continue = false);
        });
        try
        {
            Dispatcher.PushFrame(frame);
            if (worker.IsFaulted) throw worker.Exception!;
            if (!received.Task.IsCompletedSuccessfully || !JsonSerializer.Deserialize<string[]>(received.Task.Result)!.Contains(System.IO.Path.GetFileName(file)) || !System.IO.File.Exists(file))
                throw new InvalidOperationException(name + " did not accept the compact PNG file drop.");
            Console.WriteLine("Live compact PNG drag into isolated " + name + " HTML drop target: OK");
        }
        finally
        {
            timeout.Stop(); mouse_event(4, 0, 0, 0, UIntPtr.Zero); SetCursorPos(originalCursor.X, originalCursor.Y);
            if (handle != IntPtr.Zero) PostMessage(handle, 0x10, IntPtr.Zero, IntPtr.Zero);
            sourceWindow.Hide(); typeof(MainWindow).GetMethod("UnregisterHotkeys", flags)!.Invoke(sourceWindow, null);
            (typeof(MainWindow).GetField("tray", flags)!.GetValue(sourceWindow) as IDisposable)?.Dispose();
            cancellation.Cancel(); listener.Stop();
        }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T found) yield return found; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct MousePoint { public int X, Y; }
    private delegate bool WindowCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out MousePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wparam, IntPtr lparam);
}
