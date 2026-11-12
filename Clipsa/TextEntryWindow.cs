using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace Clipsa;

internal sealed class TextEntryWindow : Window
{
    private readonly TextBox input = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    public string Value => input.Text;
    public TextEntryWindow()
    {
        Title = "Añadir texto · Clipsa"; Width = 420; Height = 250; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Escribe el texto. Ctrl+Enter para añadirlo.", Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(input);
        var actions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(4) };
        var accept = new Button { Content = "Añadir", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(4) };
        accept.Click += (_, _) => DialogResult = true; actions.Children.Add(cancel); actions.Children.Add(accept); panel.Children.Add(actions); Content = panel;
        Loaded += (_, _) => input.Focus();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; DialogResult = true; } };
    }
}
