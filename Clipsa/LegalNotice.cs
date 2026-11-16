using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using TabControl = System.Windows.Controls.TabControl;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace Clipsa;

internal static class LegalNotice
{
    public static void Show(Window owner)
    {
        var panel = new DockPanel { Margin = new Thickness(18) };
        var heading = new TextBlock { Text = "Clipsa · © 2026 Francisco Serrano Jiménez\nMotor de capturas adaptado de ShareX · © 2007–2026 ShareX Team\nSoftware libre bajo GPL versión 3, sin garantía. Puedes redistribuirlo bajo sus términos.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), Foreground = Brushes.White };
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        var source = new Button { Content = "Abrir el código fuente y las versiones en GitHub", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 10, 0, 0) };
        source.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/peesejota/clipsa") { UseShellExecute = true });
        DockPanel.SetDock(source, Dock.Bottom); panel.Children.Add(source);
        var tabs = new TabControl();
        foreach (var entry in new[] { ("Licencia GPL-3.0", "Clipsa.Legal.License"), ("Créditos y componentes", "Clipsa.Legal.Credits") })
        {
            using var stream = typeof(LegalNotice).Assembly.GetManifestResourceStream(entry.Item2) ?? throw new InvalidOperationException("Missing bundled legal notice.");
            using var reader = new StreamReader(stream);
            tabs.Items.Add(new TabItem { Header = entry.Item1, Content = new TextBox { Text = reader.ReadToEnd(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(23, 26, 38)), Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 11 } });
        }
        panel.Children.Add(tabs);
        new Window { Title = "Clipsa · Licencia y créditos", Owner = owner, Width = 700, Height = 540, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(23, 26, 38)), Content = panel }.ShowDialog();
    }
}
