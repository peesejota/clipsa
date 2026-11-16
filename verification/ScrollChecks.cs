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
    public static void RunMatcherOnly() => CheckTextPage();
    public static void Run()
    {
        CheckTextPage();
        CheckContinuous();
        CheckContinuous(60, 80, 10);
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
        using var composer = new Clipsa.Capture.ShareXScrollComposer();
        composer.Append(first);
        Require(composer.Append(next) == Clipsa.Capture.ScrollCombineStatus.Appended && composer.Height == 673, "ShareX joins text despite the moving scrollbar");
        using var fast = page.Clone(new System.Drawing.Rectangle(0, 600, 800, 600), page.PixelFormat);
        Require(composer.Append(fast) == Clipsa.Capture.ScrollCombineStatus.Appended && composer.Height == 1120, "ShareX keeps a short valid overlap");
        Console.WriteLine("Page-like text, white margins and moving scrollbar: OK");
    }
    private static string GuidFromRandom(Random random) => new string(Enumerable.Range(0, random.Next(3, 9)).Select(_ => (char)('a' + random.Next(26))).ToArray());
    internal static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static void CheckContinuous(int interval = 90, int increment = 16, int count = 15)
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
        var step = 0; var finishing = false;
        var ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        timeout.Tick += (_, _) => { timeout.Stop(); session.Close(); };
        ticker.Tick += (_, _) =>
        {
            if (++step <= count) scroller.ScrollToVerticalOffset(step * increment);
            else if (step >= count + 3 && !finishing) { finishing = true; type.GetMethod("Finish", Private)!.Invoke(session, null); }
        };
        session.ContentRendered += (_, _) =>
        {
            first = (byte[]?)type.GetProperty("InitialPixels")!.GetValue(session);
            Require(first is not null, "initial frame captured before inviting the user to scroll");
            ticker.Start();
        };
        string? result = null;
        try
        {
            timeout.Start(); type.GetMethod("ShowCapture")!.Invoke(session, null);
            result = (string?)type.GetProperty("ResultPath")!.GetValue(session);
            var state = ((TextBlock)type.GetField("status", Private)!.GetValue(session)!).Text;
            Require(result is not null, "continuous scroll creates result: " + state);
            using var bmp = new System.Drawing.Bitmap(result!);
            var scale = PresentationSource.FromVisual(host)!.CompositionTarget!.TransformToDevice.M22;
            Require(Math.Abs(bmp.Height - (bounds.Height + count * increment * scale)) <= 2, "includes initial and all continuous displacements");
            var joined = Clipsa.Capture.CapturePixels.Read(bmp);
            Require(first!.AsSpan(0, bounds.Width * 30 * 4).SequenceEqual(joined.AsSpan(0, bounds.Width * 30 * 4)), "first rows preserved pixel-for-pixel");
            Console.WriteLine($"Immediate baseline and {count} continuous displacements ({interval} ms, {increment} DIP/step): OK ({bmp.Width} x {bmp.Height})");
        }
        finally { ticker.Stop(); timeout.Stop(); if (session.IsVisible) session.Close(); host.Close(); if (result is not null) System.IO.File.Delete(result); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
