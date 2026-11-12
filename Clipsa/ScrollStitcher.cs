using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace Clipsa;

public static class ScrollStitcher
{
    public enum MatchKind { Unchanged, Matched, NoOverlap, Ambiguous }
    /// <summary>Positive offset means scrolling down; fixed bands are pixel heights.</summary>
    public readonly record struct MatchResult(MatchKind Kind, int Offset, int FixedTop, int FixedBottom, double Error);

    public static byte[] Pixels(Bitmap image)
    {
        var rect = new Rectangle(0, 0, image.Width, image.Height);
        var data = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { var pixels = new byte[checked(image.Width * image.Height * 4)]; for (var y = 0; y < image.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * image.Width * 4, image.Width * 4); return pixels; }
        finally { image.UnlockBits(data); }
    }

    public static double Difference(byte[] a, byte[] b, int width, int height, int offset = 0)
    {
        Validate(a, b, width, height);
        if (Math.Abs((long)offset) >= height) throw new ArgumentOutOfRangeException(nameof(offset));
        return Score(a, b, width, height, offset, 0, 0, 64);
    }

    public static int FindOffset(byte[] previous, byte[] current, int width, int height)
    {
        var result = Match(previous, current, width, height);
        return result.Kind == MatchKind.Matched ? result.Offset : 0;
    }

    /// <summary>
    /// Pure BGRA matcher: no bitmap, screen or UI dependency. Exact integer vertical shifts only.
    /// Searches both directions, excluding contiguous stationary bands at the top/bottom.
    /// Ambiguous or unrelated views are rejected, never appended speculatively.
    /// Bounded to 8192 rows, 60M pixels and a fixed sample budget per displacement.
    /// </summary>
    public static MatchResult Match(byte[] previous, byte[] current, int width, int height, CancellationToken cancellationToken = default, int fixedTop = -1, int fixedBottom = -1)
    {
        Validate(previous, current, width, height);
        cancellationToken.ThrowIfCancellationRequested();
        if (fixedTop < -1 || fixedBottom < -1 || fixedTop >= height || fixedBottom >= height
            || Math.Max(0, fixedTop) + Math.Max(0, fixedBottom) >= height)
            throw new ArgumentOutOfRangeException(nameof(fixedTop));
        if (Score(previous, current, width, height, 0, 0, 0, 64) <= .5)
            return new(MatchKind.Unchanged, 0, 0, 0, 0);

        var top = 0; var bottom = 0;
        var bandLimit = height / 3;
        if (fixedTop >= 0) top = fixedTop;
        else while (top < bandLimit && RowDifference(previous, current, width, top) <= .5) top++;
        if (fixedBottom >= 0) bottom = fixedBottom;
        else while (bottom < bandLimit && RowDifference(previous, current, width, height - bottom - 1) <= .5) bottom++;
        var body = height - top - bottom;
        var maximum = body - Math.Max(40, body / 5);
        if (maximum < 1) return new(MatchKind.NoOverlap, 0, top, bottom, double.PositiveInfinity);

        var errors = new double[maximum * 2 + 1];
        var best = 0; var bestError = double.PositiveInfinity;
        for (var offset = -maximum; offset <= maximum; offset++)
        {
            if ((offset & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
            var error = Score(previous, current, width, height, offset, top, bottom, 12);
            errors[offset + maximum] = error;
            if (error < bestError) { bestError = error; best = offset; }
        }
        if (best == 0 || bestError > 5) return MatchText(previous, current, width, height, top, bottom, cancellationToken);
        var verifiedError = Score(previous, current, width, height, best, top, bottom, 32);
        if (verifiedError > 5 || ContentError(previous, current, width, height, best, top, bottom, width / 25, width - width / 25) > 35)
            return MatchText(previous, current, width, height, top, bottom, cancellationToken);
        // Repeated rows or blank views provide no defensible displacement.
        for (var offset = -maximum; offset <= maximum; offset++)
            if (Math.Abs(offset - best) > 2 && errors[offset + maximum] <= Math.Max(1, bestError + 1))
                return MatchText(previous, current, width, height, top, bottom, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(MatchKind.Matched, best, top, bottom, verifiedError);
    }

    // Sparse text often looks identical at the old 12 x 12 grid points (white space).
    // Row signatures sample across the content and include horizontal edges; empty rows
    // cannot establish a displacement. Dense pixel verification still rejects false joins.
    private static MatchResult MatchText(byte[] a, byte[] b, int width, int height, int top, int bottom, CancellationToken token)
    {
        var left = width / 25; var right = width - left;
        var first = Signatures(a, width, height, left, right);
        var second = Signatures(b, width, height, left, right);
        // Do not freeze blank document margins as a fixed footer/header.
        if (!Enumerable.Range(0, top).Any(y => first.Active[y])) top = 0;
        if (!Enumerable.Range(height - bottom, bottom).Any(y => first.Active[y])) bottom = 0;
        var body = height - top - bottom;
        var limit = body - Math.Max(48, body / 6);
        if (limit < 1) return new(MatchKind.NoOverlap, 0, top, bottom, double.PositiveInfinity);
        var candidates = new List<(int Offset, double Error)>();
        for (var offset = -limit; offset <= limit; offset++)
        {
            if ((offset & 31) == 0) token.ThrowIfCancellationRequested();
            if (offset == 0) continue;
            var start = top + Math.Max(0, -offset);
            var end = height - bottom - Math.Max(0, offset);
            var stride = Math.Max(1, (end - start) / 128);
            double error = 0; var rows = 0;
            for (var y = start; y < end; y += stride)
            {
                if (!first.Active[y + offset] && !second.Active[y]) continue;
                rows++;
                for (var bin = 0; bin < 16; bin++) error += Math.Abs(first.Values[(y + offset) * 16 + bin] - second.Values[y * 16 + bin]);
            }
            if (rows >= 8) candidates.Add((offset, error / (rows * 16)));
        }
        candidates.Sort((x, y) => x.Error.CompareTo(y.Error));
        foreach (var candidate in candidates.Take(12))
        {
            if (candidate.Error > 5) break;
            var error = ContentError(a, b, width, height, candidate.Offset, top, bottom, left, right);
            if (error > 35) continue;
            if (candidates.Any(other => Math.Abs(other.Offset - candidate.Offset) > 2 && other.Error <= Math.Max(.02, candidate.Error * 1.2)))
                return new(MatchKind.Ambiguous, 0, top, bottom, error);
            return new(MatchKind.Matched, candidate.Offset, top, bottom, error);
        }
        return new(MatchKind.NoOverlap, 0, top, bottom, double.PositiveInfinity);
    }
    private static (double[] Values, bool[] Active) Signatures(byte[] pixels, int width, int height, int left, int right)
    {
        var values = new double[height * 16]; var active = new bool[height];
        for (var y = 0; y < height; y++)
        {
            var low = 255.0; var high = 0.0;
            for (var bin = 0; bin < 8; bin++)
            {
                double sum = 0, edges = 0, prior = 0;
                for (var n = 0; n < 16; n++)
                {
                    var x = left + ((bin * 16 + n) * (right - left - 1) / 127);
                    var i = (y * width + x) * 4;
                    var gray = (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3.0;
                    sum += gray; if (n > 0) edges += Math.Abs(gray - prior); prior = gray;
                    low = Math.Min(low, gray); high = Math.Max(high, gray);
                }
                values[y * 16 + bin * 2] = sum / 16; values[y * 16 + bin * 2 + 1] = edges / 15;
                if (y > 0 && Math.Abs(values[y * 16 + bin * 2] - values[(y - 1) * 16 + bin * 2]) > 3) active[y] = true;
            }
            active[y] |= high - low > 3;
        }
        return (values, active);
    }
    private static double ContentError(byte[] a, byte[] b, int width, int height, int offset, int top, int bottom, int left, int right)
    {
        var start = top + Math.Max(0, -offset); var end = height - bottom - Math.Max(0, offset);
        double sum = 0; var count = 0;
        for (var y = start; y < end; y += Math.Max(1, (end - start) / 128))
            for (var x = left; x < right; x += Math.Max(1, (right - left) / 256))
            {
                var i = ((y + offset) * width + x) * 4; var j = (y * width + x) * 4;
                // Matching white margins must not dilute the error of misaligned text.
                if (a[i] > 245 && a[i + 1] > 245 && a[i + 2] > 245 && b[j] > 245 && b[j + 1] > 245 && b[j + 2] > 245) continue;
                for (var c = 0; c < 3; c++) { sum += Math.Abs(a[i + c] - b[j + c]); count++; }
            }
        return count > 0 ? sum / count : double.PositiveInfinity;
    }

    private static void Validate(byte[] a, byte[] b, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b);
        if (width < 1 || height < 1 || width > 32768 || height > 8192 || (long)width * height > 60_000_000)
            throw new ArgumentOutOfRangeException(nameof(height), "Frame exceeds the bounded matching dimensions.");
        var bytes = checked(width * height * 4);
        if (a.Length != bytes || b.Length != bytes) throw new ArgumentException("Expected tightly packed BGRA frames of identical dimensions.");
    }

    private static double RowDifference(byte[] a, byte[] b, int width, int row)
    {
        double sum = 0;
        for (var col = 1; col <= 128; col++)
        {
            var i = (row * width + col * width / 129) * 4;
            for (var channel = 0; channel < 3; channel++) sum += Math.Abs(a[i + channel] - b[i + channel]);
        }
        return sum / 384;
    }

    private static double Score(byte[] a, byte[] b, int width, int height, int offset, int top, int bottom, int samples)
    {
        var first = top + Math.Max(0, -offset);
        var overlap = height - bottom - Math.Max(0, offset) - first;
        if (overlap <= 0) return double.PositiveInfinity;
        double error = 0;
        for (var row = 1; row <= samples; row++)
        {
            var y = first + row * overlap / (samples + 1);
            for (var col = 1; col <= samples; col++)
            {
                var x = col * width / (samples + 1);
                var i = ((y + offset) * width + x) * 4;
                var j = (y * width + x) * 4;
                for (var channel = 0; channel < 3; channel++) error += Math.Abs(a[i + channel] - b[j + channel]);
            }
        }
        return error / (samples * samples * 3);
    }
}
