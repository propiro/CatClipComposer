using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CatClipComposer.Desktop;
using Microsoft.Win32;

namespace CatClipComposer;

public partial class StillScreenWindow : Window
{
    public StillScreenWindow()
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
    }

    public string? ImagePath { get; private set; }

    public TimeSpan Duration { get; private set; }

    private void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a splash, mid-roll, or outro image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ImagePathTextBox.Text = dialog.FileName;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(dialog.FileName, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        ImagePreview.Source = image;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ImagePathTextBox.Text) || !File.Exists(ImagePathTextBox.Text))
        {
            MessageBox.Show(
                this,
                "Choose an image first.",
                "Image required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(
                DurationTextBox.Text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var durationSeconds) ||
            durationSeconds is < 0.25 or > 300)
        {
            MessageBox.Show(
                this,
                "Screen duration must be between 0.25 and 300 seconds.",
                "Invalid duration",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            DurationTextBox.Focus();
            return;
        }

        ImagePath = ImagePathTextBox.Text;
        Duration = TimeSpan.FromSeconds(durationSeconds);
        DialogResult = true;
    }
}
