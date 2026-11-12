using System.IO;

namespace Clipsa;

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;
    public static bool IsPortable => File.Exists(Path.Combine(BaseDirectory, "portable.mode"));
    public static string? StorageNotice { get; private set; }
    private static readonly Lazy<string> data = new(SelectDataDirectory);
    public static string DataDirectory => data.Value;
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
    public static string SessionsDirectory => Path.Combine(DataDirectory, "sessions");
    public static string SessionFolder { get; } = Path.Combine(SessionsDirectory, Guid.NewGuid().ToString("N"));
    private static FileStream? lease;

    private static string SelectDataDirectory()
    {
        var custom = Environment.GetEnvironmentVariable("CLIPSA_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(custom) && Path.IsPathFullyQualified(custom)) { Directory.CreateDirectory(custom); return custom; }
        var normal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipsa");
        if (!IsPortable) { Directory.CreateDirectory(normal); return normal; }
        var portable = Path.Combine(BaseDirectory, "data");
        try
        {
            Directory.CreateDirectory(portable);
            using (new FileStream(Path.Combine(portable, Guid.NewGuid() + ".probe"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            return portable;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StorageNotice = "La carpeta portable no permite guardar ajustes. Se utilizará AppData; mueve Clipsa a una carpeta con permiso de escritura para llevarte sus ajustes.";
            Directory.CreateDirectory(normal); return normal;
        }
    }

    public static string DefaultSaveFolder => IsPortable ? BaseDirectory :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Clipsa");

    public static void InitializeSession()
    {
        Directory.CreateDirectory(SessionFolder);
        lease ??= new FileStream(Path.Combine(SessionFolder, ".active"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }

    public static void ReleaseSession() { lease?.Dispose(); lease = null; }
}
