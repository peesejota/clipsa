using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Clipsa;

internal static class ShutdownUpdateChecks
{
    public static void Run()
    {
        Environment.SetEnvironmentVariable("CLIPSA_DATA_DIR", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Clipsa-shutdown-" + Guid.NewGuid().ToString("N")));
        var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Clipsa;component/Theme.xaml", UriKind.Relative)));
        var main = new MainWindow();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var settings = (AppSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(main)!;
        settings.KeepInTray = true; settings.CheckUpdatesOnStartup = false;
        var other = new Window { Width = 10, Height = 10, ShowInTaskbar = false };
        bool mainClosed = false, otherClosed = false, requested = false;
        main.Closed += (_, _) => mainClosed = true; other.Closed += (_, _) => otherClosed = true;
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timeout.Tick += (_, _) => { timeout.Stop(); app.Shutdown(1); };
        main.Loaded += (_, _) => app.Dispatcher.BeginInvoke(() =>
        {
            other.Show(); other.Hide(); main.Hide();
            requested = true;
            typeof(MainWindow).GetMethod("ExitForUpdate", flags)!.Invoke(main, null);
        });
        timeout.Start();
        var result = app.Run(main);
        timeout.Stop();
        if (result != 0 || !requested || !mainClosed || !otherClosed) throw new Exception("Update shutdown left windows or tray lifecycle alive.");
        Console.WriteLine("Update shutdown closes hidden windows and exits the WPF loop with KeepInTray enabled: OK.");
    }
}
