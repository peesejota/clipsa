#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

// Adapted for Clipsa by Francisco Serrano Jiménez, 2026-11-16.
// Source: ShareX v22.4.0, commit 08bfff15e748a945944e609aca7faffbe888c7af,
// ShareX.ScreenCaptureLib/ScrollingCaptureManager.cs (CombineImages).
// Changes: System.Drawing buffers, cancellation, bounded output, explicit estimated joins.
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Clipsa.Capture;

public enum ScrollCombineStatus { Appended, Unchanged, NoMatch, Estimated, SizeLimit }

public sealed class ShareXScrollComposer : IDisposable
{
    private Bitmap? result;
    private Bitmap? lastFrame;
    private ScrollCombineStatus status;
    private int bestMatchCount, bestMatchIndex, bestIgnoreBottomOffset;
    public bool AllowEstimates { get; set; }
    public bool AutoIgnoreBottomEdge { get; set; } = true;
    public bool HasEstimates { get; private set; }
    public int Height => result?.Height ?? 0;
    public int Width => result?.Width ?? 0;
    public Bitmap Snapshot() => result is null ? throw new InvalidOperationException("No captured frames.") : new Bitmap(result);
    public ScrollCombineStatus Append(Bitmap frame, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (frame.PixelFormat != PixelFormat.Format32bppArgb) throw new ArgumentException("Expected BGRA32 capture.");
        if (frame.Width < 1 || frame.Height < 3 || (long)frame.Width * frame.Height > 60_000_000) throw new ArgumentOutOfRangeException(nameof(frame));
        if (lastFrame is not null && CapturePixels.Equal(lastFrame, frame)) return ScrollCombineStatus.Unchanged;
        var combined = CombineImages(result, frame, token);
        if (combined is not null) { result?.Dispose(); result = combined; lastFrame?.Dispose(); lastFrame = new Bitmap(frame); }
        if (status == ScrollCombineStatus.Estimated) HasEstimates = true;
        return status;
    }
    public void Dispose() { result?.Dispose(); result = null; lastFrame?.Dispose(); lastFrame = null; }
    private static unsafe int CompareRows(IntPtr first, IntPtr second, int count)
        => new ReadOnlySpan<byte>(first.ToPointer(), count).SequenceEqual(new ReadOnlySpan<byte>(second.ToPointer(), count)) ? 0 : 1;
    private sealed class BitmapPixels : IDisposable
    {
        private readonly Bitmap image;
        private readonly BitmapData data;
        private bool disposed;
        public IntPtr Pointer => data.Scan0;
        public BitmapPixels(Bitmap image)
        {
            this.image = image;
            data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            if (data.Stride != image.Width * 4) { image.UnlockBits(data); throw new ArgumentException("Expected positive tightly packed BGRA stride."); }
        }
        public void Dispose() { if (!disposed) { disposed = true; image.UnlockBits(data); } }
    }
        private unsafe Bitmap? CombineImages(Bitmap? result, Bitmap currentImage, CancellationToken token)
        {
            if (result == null)
            {
                status = ScrollCombineStatus.Appended;

                return new Bitmap(currentImage);
            }

            // Rows of both images are addressed with the result's stride below. Each capture is clipped to the current screen, so
            // a display that changes size mid-capture gives a frame that cannot be stitched on, and comparing it would read past
            // its pixel buffer. It is treated like a frame with no match: the capture ends with what it has.
            if (currentImage.Width != result.Width || currentImage.Height > result.Height)
            {
                status = ScrollCombineStatus.NoMatch;

                return null;
            }

            int matchCount = 0;
            int matchIndex = 0;
            int matchLimit = currentImage.Height / 2;

            int ignoreSideOffset = Math.Max(50, currentImage.Width / 20);
            ignoreSideOffset = Math.Min(ignoreSideOffset, currentImage.Width / 3);

            Rectangle rect = new Rectangle(ignoreSideOffset, result.Height - currentImage.Height, currentImage.Width - ignoreSideOffset * 2, currentImage.Height);

            using BitmapPixels bdResult = new(result);
            using BitmapPixels bdCurrentImage = new(currentImage);
            int stride = result.Width * 4;
            int pixelSize = stride / result.Width;
            IntPtr resultScan0 = bdResult.Pointer + pixelSize * ignoreSideOffset;
            IntPtr currentImageScan0 = bdCurrentImage.Pointer + pixelSize * ignoreSideOffset;
            int compareLength = pixelSize * rect.Width;

            int ignoreBottomOffsetMax = currentImage.Height / 3;
            int ignoreBottomOffset = Math.Max(50, currentImage.Height / 10);

            if (AutoIgnoreBottomEdge)
            {
                IntPtr resultScan0Last = resultScan0 + (result.Height - 1) * stride;
                IntPtr currentImageScan0Last = currentImageScan0 + (currentImage.Height - 1) * stride;

                for (int i = 0; i <= ignoreBottomOffsetMax; i++)
                {
                    if (CompareRows(resultScan0Last - i * stride, currentImageScan0Last - i * stride, compareLength) != 0)
                    {
                        ignoreBottomOffset += i;
                        break;
                    }
                }

                ignoreBottomOffset = Math.Max(ignoreBottomOffset, bestIgnoreBottomOffset);
            }

            ignoreBottomOffset = Math.Min(ignoreBottomOffset, ignoreBottomOffsetMax);

            int rectBottom = rect.Bottom - ignoreBottomOffset - 1;

            for (int currentImageY = currentImage.Height - 1; currentImageY >= 0 && matchCount < matchLimit; currentImageY--)
            {
                token.ThrowIfCancellationRequested();
                int currentMatchCount = 0;

                for (int y = 0; currentImageY - y >= 0 && currentMatchCount < matchLimit; y++)
                {
                    if (CompareRows(resultScan0 + ((rectBottom - y) * stride), currentImageScan0 + ((currentImageY - y) * stride), compareLength) == 0)
                    {
                        currentMatchCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (currentMatchCount > matchCount)
                {
                    matchCount = currentMatchCount;
                    matchIndex = currentImageY;
                }
            }

            bool bestGuess = false;

            if (AllowEstimates && matchCount == 0 && bestMatchCount > 0)
            {
                matchCount = bestMatchCount;
                matchIndex = bestMatchIndex;
                ignoreBottomOffset = bestIgnoreBottomOffset;
                bestGuess = true;
            }

            if (matchCount > 0)
            {
                int matchHeight = currentImage.Height - matchIndex - 1;

                if (matchHeight > 0)
                {
                    if (matchCount > bestMatchCount)
                    {
                        bestMatchCount = matchCount;
                        bestMatchIndex = matchIndex;
                        bestIgnoreBottomOffset = ignoreBottomOffset;
                    }

                    long outputHeight = (long)result.Height - ignoreBottomOffset + matchHeight;
                    if (outputHeight > 65535 || outputHeight * result.Width > 60_000_000) { status = ScrollCombineStatus.SizeLimit; return null; }
                    bdResult.Dispose(); bdCurrentImage.Dispose();
                    Bitmap newResult = new(result.Width, (int)outputHeight, PixelFormat.Format32bppArgb);

                    try
                    {
                        using Graphics g = Graphics.FromImage(newResult);
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.DrawImage(result, new Rectangle(0, 0, result.Width, result.Height - ignoreBottomOffset),
                            new Rectangle(0, 0, result.Width, result.Height - ignoreBottomOffset), GraphicsUnit.Pixel);
                        g.DrawImage(currentImage, new Rectangle(0, result.Height - ignoreBottomOffset, currentImage.Width, matchHeight),
                            new Rectangle(0, matchIndex + 1, currentImage.Width, matchHeight), GraphicsUnit.Pixel);
                    }
                    catch { newResult.Dispose(); throw; }

                    if (bestGuess)
                    {
                        status = ScrollCombineStatus.Estimated;
                    }
                    else if (status != ScrollCombineStatus.Estimated)
                    {
                        status = ScrollCombineStatus.Appended;
                    }

                    return newResult;
                }
            }

            status = ScrollCombineStatus.NoMatch;

            return null;
        }
}

