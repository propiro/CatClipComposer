using System.Globalization;
using System.IO;
using System.Windows;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;
using Microsoft.Win32;

namespace CatClipComposer;

public enum LayerEditorKind
{
    Text,
    Image,
    Audio,
    Progress
}

public partial class LayerItemEditorWindow : Window
{
    private readonly LayerEditorKind _kind;
    private readonly TimeSpan _projectDuration;
    private readonly Guid? _existingId;

    public LayerItemEditorWindow(LayerEditorKind kind, TimeSpan projectDuration)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _kind = kind;
        _projectDuration = projectDuration;
        PositionComboBox.ItemsSource = Enum.GetValues<OverlayPosition>();
        PositionComboBox.SelectedItem = OverlayPosition.Center;
        DurationTextBox.Text = Math.Max(1, Math.Min(5, projectDuration.TotalSeconds))
            .ToString("0.###", CultureInfo.InvariantCulture);
        ConfigureFields();
    }

    public LayerItemEditorWindow(ProjectTimelineItem item, TimeSpan projectDuration)
        : this(GetEditorKind(item.Kind), projectDuration)
    {
        _existingId = item.Id;
        TitleText.Text = $"Edit {item.Kind.ToString().ToLowerInvariant()}";
        AddButton.Content = "Apply";
        SourceTextBox.Text = item.SourcePath;
        OverlayTextBox.Text = item.Text;
        StartTextBox.Text = item.Start.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        DurationTextBox.Text = item.Duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        VolumeTextBox.Text = item.Volume.ToString("0.###", CultureInfo.InvariantCulture);
        if (_kind == LayerEditorKind.Text)
        {
            FontTextBox.Text = item.FontPath;
            FontSizeTextBox.Text = item.FontSize.ToString(CultureInfo.InvariantCulture);
            PositionComboBox.SelectedItem = item.Position;
        }
        else if (_kind == LayerEditorKind.Image)
        {
            PositionComboBox.SelectedItem = item.Position;
        }
        else if (_kind == LayerEditorKind.Audio)
        {
            FontTextBox.Text = item.FadeInSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            FontSizeTextBox.Text = item.FadeOutSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        }
        else
        {
            PositionComboBox.SelectedItem = item.ProgressTimeMode;
        }
    }

    public ProjectTrackKind TrackKind => _kind switch
    {
        LayerEditorKind.Audio => ProjectTrackKind.Audio,
        LayerEditorKind.Progress => ProjectTrackKind.Progress,
        _ => ProjectTrackKind.Overlay
    };

    public ProjectTimelineItem? ResultItem { get; private set; }

    private void ConfigureFields()
    {
        TitleText.Text = $"Add {_kind.ToString().ToLowerInvariant()} layer";
        var isText = _kind == LayerEditorKind.Text;
        var hasSource = _kind is LayerEditorKind.Image or LayerEditorKind.Audio;
        var hasPosition = _kind is LayerEditorKind.Text or LayerEditorKind.Image;
        var isAudio = _kind == LayerEditorKind.Audio;
        var isProgress = _kind == LayerEditorKind.Progress;

        SourceLabel.Visibility = SourceTextBox.Visibility = BrowseButton.Visibility = Visible(hasSource);
        TextLabel.Visibility = OverlayTextBox.Visibility = Visible(isText);
        FontLabel.Visibility = FontTextBox.Visibility = Visible(isText || isAudio);
        BrowseFontButton.Visibility = Visible(isText);
        FontSizeLabel.Visibility = FontSizeTextBox.Visibility = Visible(isText || isAudio);
        PositionLabel.Visibility = PositionComboBox.Visibility = Visible(hasPosition || isProgress);
        VolumeLabel.Visibility = VolumeTextBox.Visibility = Visible(isAudio);
        if (isAudio)
        {
            FontLabel.Text = "Fade in (seconds)";
            FontTextBox.Text = "0";
            FontSizeLabel.Text = "Fade out (seconds)";
            FontSizeTextBox.Text = "0";
        }
        else if (isProgress)
        {
            PositionLabel.Text = "Timing mode";
            PositionComboBox.ItemsSource = Enum.GetValues<ProgressTimeMode>();
            PositionComboBox.SelectedItem = ProgressTimeMode.CustomRange;
        }
    }

    private static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = _kind == LayerEditorKind.Audio ? "Choose music/audio" : "Choose PNG/JPEG overlay",
            Filter = _kind == LayerEditorKind.Audio
                ? "Audio files (*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac)|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac|All files (*.*)|*.*"
                : "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            SourceTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseFont_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a font file",
            Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            FontTextBox.Text = dialog.FileName;
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var fontSizeValid = int.TryParse(
            FontSizeTextBox.Text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var fontSize);
        var volumeValid = TryParse(VolumeTextBox.Text, 0, 4, out var volume);
        var audioFadeInValid = TryParse(FontTextBox.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var audioFadeIn);
        var audioFadeOutValid = TryParse(FontSizeTextBox.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var audioFadeOut);
        if (!TryParse(StartTextBox.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var start) ||
            !TryParse(DurationTextBox.Text, 0.1, TimeSpan.MaxValue.TotalSeconds, out var duration) ||
            (_kind == LayerEditorKind.Text && string.IsNullOrWhiteSpace(OverlayTextBox.Text)) ||
            (_kind is LayerEditorKind.Image or LayerEditorKind.Audio && !File.Exists(SourceTextBox.Text)) ||
            (_kind == LayerEditorKind.Text && !string.IsNullOrWhiteSpace(FontTextBox.Text) && !File.Exists(FontTextBox.Text)) ||
            (_kind == LayerEditorKind.Text && !fontSizeValid) ||
            (_kind == LayerEditorKind.Text && fontSize is < 8 or > 240) ||
            (_kind == LayerEditorKind.Audio && (!volumeValid || !audioFadeInValid || !audioFadeOutValid ||
                                                audioFadeIn > duration || audioFadeOut > duration)))
        {
            MessageBox.Show(
                this,
                "Check the required source/text, start, positive duration, optional font, font size 8–240, and volume 0–4.",
                "Invalid layer item",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var itemKind = _kind switch
        {
            LayerEditorKind.Text => ProjectItemKind.TextOverlay,
            LayerEditorKind.Image => ProjectItemKind.ImageOverlay,
            LayerEditorKind.Audio => ProjectItemKind.Audio,
            _ => ProjectItemKind.ProgressBar
        };
        var defaultName = _kind switch
        {
            LayerEditorKind.Text => OverlayTextBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Text",
            LayerEditorKind.Progress => "Progress bar",
            _ => Path.GetFileName(SourceTextBox.Text)
        };
        var progressMode = PositionComboBox.SelectedItem is ProgressTimeMode selectedProgressMode
            ? selectedProgressMode
            : ProgressTimeMode.CustomRange;
        if (_kind == LayerEditorKind.Progress && progressMode == ProgressTimeMode.WholeProject)
        {
            start = 0;
            duration = Math.Max(0.1, _projectDuration.TotalSeconds);
        }

        ResultItem = new ProjectTimelineItem
        {
            Id = _existingId ?? Guid.NewGuid(),
            Kind = itemKind,
            Name = defaultName,
            SourcePath = SourceTextBox.Text,
            StartTicks = TimeSpan.FromSeconds(start).Ticks,
            DurationTicks = TimeSpan.FromSeconds(duration).Ticks,
            Text = OverlayTextBox.Text,
            FontPath = _kind == LayerEditorKind.Text ? FontTextBox.Text : string.Empty,
            FontSize = _kind == LayerEditorKind.Text ? fontSize : 42,
            Position = PositionComboBox.SelectedItem is OverlayPosition position ? position : OverlayPosition.Center,
            Volume = _kind == LayerEditorKind.Audio ? volume : 1,
            FadeInSeconds = _kind == LayerEditorKind.Audio ? audioFadeIn : 0,
            FadeOutSeconds = _kind == LayerEditorKind.Audio ? audioFadeOut : 0,
            ProgressTimeMode = progressMode
        };
        DialogResult = true;
    }

    private static LayerEditorKind GetEditorKind(ProjectItemKind kind) => kind switch
    {
        ProjectItemKind.TextOverlay => LayerEditorKind.Text,
        ProjectItemKind.ImageOverlay => LayerEditorKind.Image,
        ProjectItemKind.Audio => LayerEditorKind.Audio,
        ProjectItemKind.ProgressBar => LayerEditorKind.Progress,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "This project item is not edited here.")
    };

    private static bool TryParse(string value, double minimum, double maximum, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
        double.IsFinite(parsed) && parsed >= minimum && parsed <= maximum;
}
