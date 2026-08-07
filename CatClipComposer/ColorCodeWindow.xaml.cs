using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace CatClipComposer;

public partial class ColorCodeWindow : Window
{
    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

    public ColorCodeWindow(string currentColor)
    {
        InitializeComponent();
        Desktop.DesktopWindowTheme.Apply(this);
        ColorTextBox.Text = currentColor;
    }

    public string ResultColor { get; private set; } = string.Empty;

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color })
        {
            ColorTextBox.Text = color;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ResultColor = string.Empty;
        DialogResult = true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var color = ColorTextBox.Text.Trim();
        if (!ColorPattern.IsMatch(color))
        {
            MessageBox.Show(this, "Enter a color as #RRGGBB.", "Invalid color", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ResultColor = color.ToUpperInvariant();
        DialogResult = true;
    }
}
