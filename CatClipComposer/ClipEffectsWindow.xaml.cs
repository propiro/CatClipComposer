using System.Globalization;
using System.Windows;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;

namespace CatClipComposer;

public partial class ClipEffectsWindow : Window
{
    private readonly TimeSpan _duration;

    public ClipEffectsWindow(TimelineClipViewModel clip)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _duration = clip.Duration;
        TitleText.Text = clip.FileName;
        FitModeComboBox.ItemsSource = Enum.GetValues<VideoFitMode>();
        FitModeComboBox.SelectedItem = clip.FitMode;
        FadeInTextBox.Text = clip.FadeInSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        FadeOutTextBox.Text = clip.FadeOutSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        VolumeTextBox.Text = clip.Volume.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public VideoFitMode FitMode { get; private set; }

    public double FadeInSeconds { get; private set; }

    public double FadeOutSeconds { get; private set; }

    public double Volume { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (FitModeComboBox.SelectedItem is not VideoFitMode fitMode ||
            !TryParse(FadeInTextBox.Text, 0, _duration.TotalSeconds, out var fadeIn) ||
            !TryParse(FadeOutTextBox.Text, 0, _duration.TotalSeconds, out var fadeOut) ||
            !TryParse(VolumeTextBox.Text, 0, 4, out var volume))
        {
            MessageBox.Show(
                this,
                $"Fades must be between 0 and {_duration.TotalSeconds:0.###} seconds; volume must be 0–4.",
                "Invalid clip effects",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        FitMode = fitMode;
        FadeInSeconds = fadeIn;
        FadeOutSeconds = fadeOut;
        Volume = volume;
        DialogResult = true;
    }

    private static bool TryParse(string value, double minimum, double maximum, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
        double.IsFinite(parsed) && parsed >= minimum && parsed <= maximum;
}
