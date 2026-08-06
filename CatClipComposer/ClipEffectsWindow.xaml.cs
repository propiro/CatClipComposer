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
        Configure(clip.FileName, clip.FitMode, clip.FadeInSeconds, clip.FadeOutSeconds, clip.Volume);
    }

    public ClipEffectsWindow(ProjectTimelineItem item)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _duration = item.Duration;
        Configure(item.Name, item.FitMode, item.FadeInSeconds, item.FadeOutSeconds, item.Volume);
    }

    private void Configure(
        string title,
        VideoFitMode fitMode,
        double fadeInSeconds,
        double fadeOutSeconds,
        double volume)
    {
        TitleText.Text = title;
        FitModeComboBox.ItemsSource = Enum.GetValues<VideoFitMode>()
            .Where(mode => mode != VideoFitMode.BlurBackground);
        FitModeComboBox.SelectedItem = fitMode == VideoFitMode.BlurBackground
            ? VideoFitMode.Fit
            : fitMode;
        FadeInTextBox.Text = fadeInSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        FadeOutTextBox.Text = fadeOutSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        VolumeTextBox.Text = volume.ToString("0.###", CultureInfo.InvariantCulture);
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
