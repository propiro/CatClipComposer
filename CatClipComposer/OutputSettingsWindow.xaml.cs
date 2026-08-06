using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class OutputSettingsWindow : Window
{
    private bool _initializing;

    public OutputSettingsWindow(ProjectOutputSettings current)
    {
        _initializing = true;
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        PresetComboBox.ItemsSource = OutputPresetCatalog.Common;
        EncoderComboBox.ItemsSource = Enum.GetValues<VideoEncoderPreset>();
        EncoderComboBox.SelectedItem = current.VideoEncoder;
        WidthTextBox.Text = current.Width.ToString(CultureInfo.InvariantCulture);
        HeightTextBox.Text = current.Height.ToString(CultureInfo.InvariantCulture);
        FrameRateTextBox.Text = current.FramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture);
        QualityTextBox.Text = current.QualityPercent.ToString(CultureInfo.InvariantCulture);
        VideoBitrateTextBox.Text = current.VideoBitrateKbps.ToString(CultureInfo.InvariantCulture);
        AudioBitrateTextBox.Text = current.AudioBitrateKbps.ToString(CultureInfo.InvariantCulture);
        PresetComboBox.SelectedItem = OutputPresetCatalog.Common.FirstOrDefault(preset =>
            preset.Name.Equals(current.PresetName, StringComparison.OrdinalIgnoreCase)) ??
            OutputPresetCatalog.Common[^1];
        _initializing = false;
        UpdateDescription();
    }

    public ProjectOutputSettings? ResultSettings { get; private set; }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || PresetComboBox.SelectedItem is not OutputPreset preset)
        {
            return;
        }

        if (!preset.Name.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            WidthTextBox.Text = preset.Width.ToString(CultureInfo.InvariantCulture);
            HeightTextBox.Text = preset.Height.ToString(CultureInfo.InvariantCulture);
            FrameRateTextBox.Text = preset.FramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture);
            VideoBitrateTextBox.Text = preset.VideoBitrateKbps.ToString(CultureInfo.InvariantCulture);
            AudioBitrateTextBox.Text = preset.AudioBitrateKbps.ToString(CultureInfo.InvariantCulture);
        }

        UpdateDescription();
    }

    private void UpdateDescription()
    {
        PresetDescriptionText.Text = PresetComboBox.SelectedItem is OutputPreset preset
            ? $"{preset.Description}. Native MPEG-4 is the redistributable-friendly default; Windows H.264 depends on FFmpeg/Windows support; libx264 is GPL opt-in."
            : string.Empty;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseInt(WidthTextBox.Text, 16, 7680, out var width) || width % 2 != 0 ||
            !TryParseInt(HeightTextBox.Text, 16, 7680, out var height) || height % 2 != 0 ||
            !double.TryParse(FrameRateTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) ||
            !double.IsFinite(fps) || fps is < 1 or > 240 ||
            !TryParseInt(QualityTextBox.Text, 1, 100, out var quality) ||
            !TryParseInt(VideoBitrateTextBox.Text, 500, 150000, out var videoBitrate) ||
            !TryParseInt(AudioBitrateTextBox.Text, 64, 512, out var audioBitrate) ||
            EncoderComboBox.SelectedItem is not VideoEncoderPreset encoder)
        {
            MessageBox.Show(
                this,
                "Use even dimensions from 16–7680, frame rate 1–240, quality 1–100, video bitrate 500–150000, and audio bitrate 64–512.",
                "Invalid output settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        ResultSettings = new ProjectOutputSettings
        {
            PresetName = (PresetComboBox.SelectedItem as OutputPreset)?.Name ?? "Custom",
            Width = width,
            Height = height,
            FramesPerSecond = fps,
            VideoEncoder = encoder,
            QualityPercent = quality,
            VideoBitrateKbps = videoBitrate,
            AudioBitrateKbps = audioBitrate
        };
        DialogResult = true;
    }

    private static bool TryParseInt(string value, int minimum, int maximum, out int parsed) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) &&
        parsed >= minimum && parsed <= maximum;
}
