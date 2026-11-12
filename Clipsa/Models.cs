using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
namespace Clipsa;
public sealed class CaptureItem { public required string FilePath { get; init; } public required BitmapImage Preview { get; init; } public string Name => Path.GetFileName(FilePath); }
public sealed class CaptureStore
{
    public ObservableCollection<CaptureItem> Items { get; } = new();
    public void Refresh(CaptureItem item)
    {
        var index = Items.IndexOf(item);
        if (index < 0) return;
        var image = new BitmapImage();
        using var stream = new MemoryStream(File.ReadAllBytes(item.FilePath));
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 260; image.StreamSource = stream; image.EndInit(); image.Freeze();
        Items[index] = new CaptureItem { FilePath = item.FilePath, Preview = image };
    }
    public void Add(string path)
    {
        if (!File.Exists(path)) return; for (var attempt = 0; attempt < 4; attempt++) { try { var bytes = File.ReadAllBytes(path); if (bytes.Length < 64) { Thread.Sleep(40); continue; } using var stream = new MemoryStream(bytes); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 260; image.StreamSource = stream; image.EndInit(); image.Freeze(); Items.Insert(0, new CaptureItem { FilePath = path, Preview = image }); return; } catch when (attempt < 3) { Thread.Sleep(60); } }
    }
}
