using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Clipsa.Capture;

internal static class ShareXCaptureChecks
{
    public static void Run()
    {
        using var document = Document(400, 2400);
        using var composer = new ShareXScrollComposer();
        using (var first = View(document, 0, 300)) Require(composer.Append(first) == ScrollCombineStatus.Appended, "first frame");
        foreach (var offset in new[] { 70, 140, 210, 280, 350 })
        {
            using var frame = View(document, offset, 300);
            Require(composer.Append(frame) == ScrollCombineStatus.Appended && composer.Height == 300 + offset, "exact ShareX row join at " + offset);
        }
        using (var repeated = View(document, 350, 300)) Require(composer.Append(repeated) == ScrollCombineStatus.Unchanged, "end does not append a duplicate");
        using (var output = composer.Snapshot())
        using (var expected = View(document, 0, 650)) Require(CapturePixels.Equal(output, expected), "complete output preserves every pixel with fixed header/footer");
        using (var foreign = View(document, 1200, 300)) Require(composer.Append(foreign) == ScrollCombineStatus.NoMatch && composer.Height == 650, "unrelated view preserves prior output");
        using (var smaller = View(document, 420, 300)) Require(composer.Append(smaller) == ScrollCombineStatus.Appended && composer.Height == 720, "recovers against accumulated tail after a rejected view");
        using var estimated = new ShareXScrollComposer { AllowEstimates = true };
        using (var first = View(document, 0, 300)) estimated.Append(first);
        using (var moved = View(document, 70, 300)) estimated.Append(moved);
        using (var animated = View(document, 1200, 300)) Require(estimated.Append(animated) == ScrollCombineStatus.Estimated && estimated.HasEstimates, "ShareX best-guess remains explicit and marked");
        using var narrow = new ShareXScrollComposer();
        using (var original = View(document, 0, 300))
        using (var first = original.Clone(new Rectangle(0, 0, 80, 300), PixelFormat.Format32bppArgb)) narrow.Append(first);
        using (var original = View(document, 60, 300))
        using (var moved = original.Clone(new Rectangle(0, 0, 80, 300), PixelFormat.Format32bppArgb)) Require(narrow.Append(moved) == ScrollCombineStatus.Appended && narrow.Height == 360, "narrow capture clamps ignored margins");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { using var frame = View(document, 420, 300); composer.Append(frame, cancelled.Token); throw new Exception("Cancelled compose accepted."); } catch (OperationCanceledException) { }
        Console.WriteLine("ShareX composer: exact accumulated rows, sticky edges, duplicate end, rejection/recovery, marked estimates, narrow regions and cancellation: OK.");
    }
    internal static Bitmap Document(int width, int height)
    {
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var pixels = new byte[width * height * 4]; new Random(872).NextBytes(pixels);
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        var data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, image.PixelFormat);
        try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); } finally { image.UnlockBits(data); }
        return image;
    }
    private static Bitmap View(Bitmap document, int offset, int height)
    {
        var image = document.Clone(new Rectangle(0, offset, document.Width, height), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.FillRectangle(Brushes.DarkSlateBlue, 0, 0, image.Width, 20);
        graphics.FillRectangle(Brushes.DarkOrange, 0, height - 30, image.Width, 30);
        return image;
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
}
