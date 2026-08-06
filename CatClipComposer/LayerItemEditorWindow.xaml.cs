using System.Globalization;
using System.IO;
using System.Windows;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;
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
    private readonly double _snapSeconds;

    public LayerItemEditorWindow(
        LayerEditorKind kind,
        TimeSpan projectDuration,
        string customFontFolder,
        TimelineSnapMode snapMode,
        double framesPerSecond,
        TimeSpan? selectedSegmentStart = null,
        TimeSpan? selectedSegmentDuration = null)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _kind = kind;
        _projectDuration = projectDuration;
        _snapSeconds = snapMode switch
        {
            TimelineSnapMode.Frame => 1 / Math.Clamp(framesPerSecond, 1, 240),
            TimelineSnapMode.TenthSecond => 0.1,
            TimelineSnapMode.HalfSecond => 0.5,
            _ => 1
        };
        SnapDescriptionText.Text = snapMode == TimelineSnapMode.Frame
            ? $"Times snap to one frame ({framesPerSecond:0.###} fps)."
            : $"Times snap to {_snapSeconds:0.###} second increments.";
        PositionComboBox.ItemsSource = Enum.GetValues<OverlayPosition>();
        PositionComboBox.SelectedItem = OverlayPosition.Center;
        ProgressTimingComboBox.ItemsSource = Enum.GetValues<ProgressTimeMode>();
        ProgressTimingComboBox.SelectedItem = ProgressTimeMode.CustomRange;
        ProgressStyleComboBox.ItemsSource = Enum.GetValues<ProgressBarStyle>();
        ProgressStyleComboBox.SelectedItem = ProgressBarStyle.Solid;
        ProgressPositionComboBox.ItemsSource = Enum.GetValues<ProgressBarPosition>();
        ProgressPositionComboBox.SelectedItem = ProgressBarPosition.Bottom;
        FontComboBox.ItemsSource = FontCatalog.Load(customFontFolder);
        FontComboBox.SelectedItem = ((IEnumerable<FontChoice>)FontComboBox.ItemsSource)
            .FirstOrDefault(font => font.FamilyName.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase)) ??
            ((IEnumerable<FontChoice>)FontComboBox.ItemsSource).FirstOrDefault();
        DurationTextBox.Text = Math.Max(1, Math.Min(5, projectDuration.TotalSeconds))
            .ToString("0.###", CultureInfo.InvariantCulture);
        ConfigureFields();
        if (_kind == LayerEditorKind.Progress &&
            selectedSegmentStart.HasValue && selectedSegmentDuration > TimeSpan.Zero)
        {
            ProgressTimingComboBox.SelectedItem = ProgressTimeMode.SourceSegment;
            StartTextBox.Text = selectedSegmentStart.Value.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
            DurationTextBox.Text = selectedSegmentDuration.Value.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }

    public LayerItemEditorWindow(
        ProjectTimelineItem item,
        TimeSpan projectDuration,
        string customFontFolder,
        TimelineSnapMode snapMode,
        double framesPerSecond)
        : this(GetEditorKind(item.Kind), projectDuration, customFontFolder, snapMode, framesPerSecond)
    {
        _existingId = item.Id;
        TitleText.Text = $"Edit {GetDisplayName(_kind)}";
        AddButton.Content = "Apply";
        SourceTextBox.Text = item.SourcePath;
        OverlayTextBox.Text = item.Text;
        StartTextBox.Text = item.Start.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        DurationTextBox.Text = item.Duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        VolumeTextBox.Text = item.Volume.ToString("0.###", CultureInfo.InvariantCulture);
        FadeInTextBox.Text = item.FadeInSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        FadeOutTextBox.Text = item.FadeOutSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        FontSizeTextBox.Text = item.FontSize.ToString(CultureInfo.InvariantCulture);
        PositionComboBox.SelectedItem = item.Position;
        ProgressTimingComboBox.SelectedItem = item.ProgressTimeMode;
        ProgressStyleComboBox.SelectedItem = item.ProgressBarStyle;
        ProgressPositionComboBox.SelectedItem = item.ProgressBarPosition;
        ProgressColorTextBox.Text = item.ProgressColor;
        ProgressHeightTextBox.Text = item.ProgressHeight.ToString(CultureInfo.InvariantCulture);

        if (_kind == LayerEditorKind.Text && FontComboBox.ItemsSource is IEnumerable<FontChoice> fonts)
        {
            FontComboBox.SelectedItem = fonts.FirstOrDefault(font =>
                (!string.IsNullOrWhiteSpace(item.FontPath) &&
                 font.FilePath.Equals(item.FontPath, StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrWhiteSpace(item.FontPath) &&
                 font.FamilyName.Equals(item.FontFamily, StringComparison.OrdinalIgnoreCase))) ??
                FontComboBox.SelectedItem;
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
        TitleText.Text = $"Add {GetDisplayName(_kind)}";
        SourceFields.Visibility = Visible(_kind is LayerEditorKind.Image or LayerEditorKind.Audio);
        TextFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        FontFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        TextPlacementFields.Visibility = Visible(_kind is LayerEditorKind.Text or LayerEditorKind.Image);
        AudioFields.Visibility = Visible(_kind == LayerEditorKind.Audio);
        ProgressFields.Visibility = Visible(_kind == LayerEditorKind.Progress);
        if (_kind == LayerEditorKind.Audio)
        {
            SourceLabel.Text = "Music / audio file";
        }
        else if (_kind == LayerEditorKind.Image)
        {
            SourceLabel.Text = "PNG / image file";
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

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var fontSizeValid = int.TryParse(FontSizeTextBox.Text, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var fontSize) && fontSize is >= 8 and <= 240;
        var volumeValid = TryParse(VolumeTextBox.Text, 0, 4, out var volume);
        var fadeInValid = TryParse(FadeInTextBox.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var fadeIn);
        var fadeOutValid = TryParse(FadeOutTextBox.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var fadeOut);
        var progressHeightValid = int.TryParse(ProgressHeightTextBox.Text, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var progressHeight) && progressHeight is >= 2 and <= 100;
        var progressColorValid = TryNormalizeColor(ProgressColorTextBox.Text, out var progressColor);

        if (!TryParse(StartTextBox.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var start) ||
            !TryParse(DurationTextBox.Text, _snapSeconds, TimeSpan.MaxValue.TotalSeconds, out var duration) ||
            (_kind == LayerEditorKind.Text &&
             (string.IsNullOrWhiteSpace(OverlayTextBox.Text) || !fontSizeValid || FontComboBox.SelectedItem is not FontChoice)) ||
            (_kind is LayerEditorKind.Image or LayerEditorKind.Audio && !File.Exists(SourceTextBox.Text)) ||
            (_kind == LayerEditorKind.Audio && (!volumeValid || !fadeInValid || !fadeOutValid ||
                                                fadeIn > duration || fadeOut > duration)) ||
            (_kind == LayerEditorKind.Progress && (!progressHeightValid || !progressColorValid)))
        {
            MessageBox.Show(this,
                "Check the required source/text, start, positive duration, font size 8–240, audio values, and progress color/height.",
                "Invalid layer item", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        start = Math.Round(start / _snapSeconds) * _snapSeconds;
        duration = Math.Max(_snapSeconds, Math.Round(duration / _snapSeconds) * _snapSeconds);

        var progressMode = ProgressTimingComboBox.SelectedItem is ProgressTimeMode timing
            ? timing
            : ProgressTimeMode.CustomRange;
        if (_kind == LayerEditorKind.Progress && progressMode == ProgressTimeMode.WholeProject)
        {
            start = 0;
            duration = Math.Max(0.1, _projectDuration.TotalSeconds);
        }

        var font = FontComboBox.SelectedItem as FontChoice;
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

        ResultItem = new ProjectTimelineItem
        {
            Id = _existingId ?? Guid.NewGuid(),
            Kind = itemKind,
            Name = defaultName,
            SourcePath = SourceTextBox.Text,
            StartTicks = TimeSpan.FromSeconds(start).Ticks,
            DurationTicks = TimeSpan.FromSeconds(duration).Ticks,
            Text = OverlayTextBox.Text,
            FontPath = _kind == LayerEditorKind.Text ? font?.FilePath ?? string.Empty : string.Empty,
            FontFamily = _kind == LayerEditorKind.Text ? font?.FamilyName ?? "Segoe UI" : "Segoe UI",
            FontSize = _kind == LayerEditorKind.Text ? fontSize : 42,
            Position = PositionComboBox.SelectedItem is OverlayPosition position ? position : OverlayPosition.Center,
            Volume = _kind == LayerEditorKind.Audio ? volume : 1,
            FadeInSeconds = _kind == LayerEditorKind.Audio ? fadeIn : 0,
            FadeOutSeconds = _kind == LayerEditorKind.Audio ? fadeOut : 0,
            ProgressTimeMode = progressMode,
            ProgressBarStyle = ProgressStyleComboBox.SelectedItem is ProgressBarStyle style ? style : ProgressBarStyle.Solid,
            ProgressBarPosition = ProgressPositionComboBox.SelectedItem is ProgressBarPosition barPosition
                ? barPosition
                : ProgressBarPosition.Bottom,
            ProgressColor = progressColor,
            ProgressHeight = progressHeight
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

    private static string GetDisplayName(LayerEditorKind kind) => kind switch
    {
        LayerEditorKind.Image => "PNG / image layer",
        LayerEditorKind.Audio => "music layer",
        LayerEditorKind.Progress => "progress effect",
        _ => "text layer"
    };

    private static bool TryNormalizeColor(string value, out string normalized)
    {
        normalized = value.Trim().ToUpperInvariant();
        if (!normalized.StartsWith('#'))
        {
            normalized = $"#{normalized}";
        }

        return normalized.Length == 7 && normalized[1..].All(Uri.IsHexDigit);
    }

    private static bool TryParse(string value, double minimum, double maximum, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
        double.IsFinite(parsed) && parsed >= minimum && parsed <= maximum;
}
