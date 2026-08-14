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
        var fadeStep = Math.Max(0.01, Math.Min(0.1, _duration.TotalSeconds / 100));
        FadeInEditor.IsTimeValue = true;
        FadeInEditor.Minimum = 0;
        FadeInEditor.Maximum = Math.Max(fadeStep, _duration.TotalSeconds);
        FadeInEditor.Step = fadeStep;
        FadeInEditor.SetValue(fadeInSeconds);
        FadeOutEditor.IsTimeValue = true;
        FadeOutEditor.Minimum = 0;
        FadeOutEditor.Maximum = Math.Max(fadeStep, _duration.TotalSeconds);
        FadeOutEditor.Step = fadeStep;
        FadeOutEditor.SetValue(fadeOutSeconds);
        VolumeEditor.SetValue(volume);
    }

    public VideoFitMode FitMode { get; private set; }

    public double FadeInSeconds { get; private set; }

    public double FadeOutSeconds { get; private set; }

    public double Volume { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (FitModeComboBox.SelectedItem is not VideoFitMode fitMode ||
            !TryParse(FadeInEditor.Text, 0, _duration.TotalSeconds, out var fadeIn) ||
            !TryParse(FadeOutEditor.Text, 0, _duration.TotalSeconds, out var fadeOut) ||
            !TryParse(VolumeEditor.Text, 0, 4, out var volume))
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
