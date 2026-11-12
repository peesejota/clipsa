using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipsa;

internal static class ScrollChecks
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Run()
    {
        CheckTextPage();
        CheckContinuous();
        CheckAcceptHistory();
        if (Environment.GetEnvironmentVariable("CLIPSA_WEB_SCROLL_QA") == "1") BrowserScrollChecks.Run();
    }
    private static void CheckTextPage()
    {
        using var page = new System.Drawing.Bitmap(800, 1800, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(page))
        using (var font = new System.Drawing.Font("Segoe UI", 12))
        {
            graphics.Clear(System.Drawing.Color.White);
            var random = new Random(81);
            for (var y = 10; y < 1750; y += 29)
            {
                var text = string.Join(" ", Enumerable.Range(0, random.Next(3, 9)).Select(_ => GuidFromRandom(random)));
                graphics.DrawString(text, font, System.Drawing.Brushes.Black, 60, y);
            }
        }
        using var first = page.Clone(new System.Drawing.Rectangle(0, 80, 800, 600), page.PixelFormat);
        using var next = page.Clone(new System.Drawing.Rectangle(0, 153, 800, 600), page.PixelFormat);
        // A moving scrollbar must not invalidate the actual page content.
        using (var graphics = System.Drawing.Graphics.FromImage(next)) graphics.FillRectangle(System.Drawing.Brushes.Gray, 786, 230, 10, 100);
        var match = ScrollStitcher.Match(ScrollStitcher.Pixels(first), ScrollStitcher.Pixels(next), 800, 600);
        Require(match.Kind == ScrollStitcher.MatchKind.Matched && match.Offset == 73, "sparse text, white margins and moving scrollbar: " + match);
        Console.WriteLine("Page-like text, white margins and moving scrollbar: OK");
    }
    private static string GuidFromRandom(Random random) => new string(Enumerable.Range(0, random.Next(3, 9)).Select(_ => (char)('a' + random.Next(26))).ToArray());
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static void CheckContinuous()
    {
        var pixels = new byte[240 * 1400 * 4]; new Random(31).NextBytes(pixels);
        for (var n = 3; n < pixels.Length; n += 4) pixels[n] = 255;
        var image = new Image { Source = BitmapSource.Create(240, 1400, 96, 96, PixelFormats.Bgra32, null, pixels, 960), Width = 240, Height = 1400, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        var scroller = new ScrollViewer { Content = image, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var host = new Window { Title = "Clipsa continuous scroll verification", Content = scroller, Width = 260, Height = 260, Left = 20, Top = 300, Topmost = true };
        host.Show(); host.UpdateLayout(); Pump(180);
        var a = scroller.PointToScreen(new Point(10, 10)); var b = scroller.PointToScreen(new Point(210, 190));
        var bounds = new System.Drawing.Rectangle((int)a.X, (int)a.Y, (int)(b.X - a.X), (int)(b.Y - a.Y));
        var type = typeof(MainWindow).Assembly.GetType("Clipsa.ScrollCaptureWindow")!;
        var session = (Window)Activator.CreateInstance(type, new object[] { bounds, 250 })!;
        byte[]? first = null;
        session.Loaded += (_, _) =>
        {
            first = (byte[]?)type.GetField("previous", Private)!.GetValue(session);
            Require(first is not null, "initial frame captured before scrolling");
        };
        var step = 0; var finishing = false;
        var ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        timeout.Tick += (_, _) => { timeout.Stop(); session.DialogResult = false; };
        ticker.Tick += (_, _) =>
        {
            if (++step <= 15) scroller.ScrollToVerticalOffset(step * 16);
            else if (step >= 18 && !finishing) { finishing = true; type.GetMethod("Finish", Private)!.Invoke(session, null); }
        };
        string? result = null;
        try
        {
            ticker.Start(); timeout.Start(); session.ShowDialog();
            result = (string?)type.GetProperty("ResultPath")!.GetValue(session);
            Require(result is not null, "continuous scroll creates result");
            using var bmp = new System.Drawing.Bitmap(result!);
            var scale = PresentationSource.FromVisual(host)!.CompositionTarget!.TransformToDevice.M22;
            Require(Math.Abs(bmp.Height - (bounds.Height + 240 * scale)) <= 2, "includes initial and all continuous displacements");
            var joined = ScrollStitcher.Pixels(bmp);
            Require(first!.AsSpan(0, bounds.Width * 30 * 4).SequenceEqual(joined.AsSpan(0, bounds.Width * 30 * 4)), "first rows preserved pixel-for-pixel");
            Console.WriteLine($"Immediate baseline and 15 continuous displacements (90 ms, no per-frame pause): OK ({bmp.Width} x {bmp.Height})");
        }
        finally { ticker.Stop(); timeout.Stop(); if (session.IsVisible) session.Close(); host.Close(); if (result is not null) System.IO.File.Delete(result); }
    }
    private static void CheckAcceptHistory()
    {
        var type = typeof(MainWindow).Assembly.GetType("Clipsa.ScrollCaptureWindow")!;
        var session = (Window)Activator.CreateInstance(type, new object[] { new System.Drawing.Rectangle(20, 20, 120, 200), 250 })!;
        var bytes = new byte[120 * 800 * 4]; new Random(13).NextBytes(bytes);
        for (var n = 3; n < bytes.Length; n += 4) bytes[n] = 255;
        using var document = new System.Drawing.Bitmap(120, 800, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = document.LockBits(new System.Drawing.Rectangle(0, 0, 120, 800), System.Drawing.Imaging.ImageLockMode.WriteOnly, document.PixelFormat);
        try { System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length); } finally { document.UnlockBits(data); }
        async Task<bool> Accept(int offset)
        {
            using var frame = document.Clone(new System.Drawing.Rectangle(0, offset, 120, 200), document.PixelFormat);
            return await (Task<bool>)type.GetMethod("Accept", Private)!.Invoke(session, new object[] { frame, ScrollStitcher.Pixels(frame), CancellationToken.None })!;
        }
        var task = session.Dispatcher.InvokeAsync(async () =>
        {
            Require(await Accept(0), "synthetic first");
            Require(await Accept(80), "synthetic down");
            Require(!await Accept(600), "gap rejected");
            Require(await Accept(160), "recovery uses last accepted, not rejected frame");
            Require(await Accept(80), "reverse does not duplicate");
            Require(await Accept(240), "resumed down");
        }).Task.Unwrap();
        while (!task.IsCompleted) Pump(20);
        task.GetAwaiter().GetResult();
        Require((int)type.GetField("maximum", Private)!.GetValue(session)! == 440, "accumulated height retained");
        session.Close();
        Console.WriteLine("Accumulation, rejected-gap recovery and reversing without losing earlier strips: OK");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
