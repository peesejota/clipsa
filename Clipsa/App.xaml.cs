using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Size = System.Windows.Size;

namespace Clipsa;

public partial class App : System.Windows.Application
{
    private SingleInstance? instance;
    public static bool IsFatal { get; private set; }
    public static bool BackgroundLaunch { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--self-test"))
        {
            var index = Array.IndexOf(e.Args, "--report");
            var report = index >= 0 && index + 1 < e.Args.Length ? e.Args[index + 1] : Path.Combine(AppPaths.DataDirectory, "self-test.json");
            RunSelfTest(report); return;
        }
        instance = new SingleInstance();
        if (!instance.Acquire())
        {
            SingleInstance.NotifyExisting().GetAwaiter().GetResult();
            instance.Dispose(); instance = null; Shutdown(); return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            IsFatal = true; AppDiagnostics.Log(args.Exception, "Error de interfaz");
            args.Handled = true;
            System.Windows.MessageBox.Show("Clipsa ha encontrado un error y necesita cerrarse. Las capturas de esta sesión se conservarán para recuperarlas si activaste esa opción. El informe está en la carpeta de ajustes, dentro de logs.", "Clipsa", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            IsFatal = true;
            if (args.ExceptionObject is Exception ex) AppDiagnostics.Log(ex, "Error fatal");
        };
        TaskScheduler.UnobservedTaskException += (_, args) => { AppDiagnostics.Log(args.Exception, "Tarea en segundo plano"); args.SetObserved(); };
        BackgroundLaunch = e.Args.Contains("--background");
        base.OnStartup(e);
        _ = instance.Listen(() => Dispatcher.BeginInvoke(() => { if (MainWindow is { } window) { window.Show(); window.Activate(); } }));
    }

    private void RunSelfTest(string path)
    {
        try
        {
            var window = new MainWindow();
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(1100, 276)); root.Arrange(new Rect(0, 0, 1100, 276)); root.UpdateLayout();
            using var logo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Clipsa;component/Brand/clipsa-icon.ico"))!.Stream;
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(logo, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count != 9) throw new InvalidDataException("El icono no contiene sus nueve resoluciones.");
            var crop = CropGeometry.Select(new System.Windows.Point(10, 10), new System.Windows.Point(170, 100), new Size(240, 160), 16.0 / 9);
            if (Math.Abs(crop.Width / crop.Height - 16.0 / 9) > .001) throw new InvalidDataException("Error de geometría del recorte.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { passed = true, version = UpdateService.CurrentVersion.ToString(), runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), portable = AppPaths.IsPortable, dataDirectory = AppPaths.DataDirectory, iconFrames = decoder.Frames.Count, ui = "loaded" }, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Autocomprobación");
            File.WriteAllText(path, JsonSerializer.Serialize(new { passed = false, error = ex.ToString() })); Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!IsFatal) CaptureService.Cleanup();
        AppPaths.ReleaseSession(); instance?.Dispose();
        base.OnExit(e);
    }
}
