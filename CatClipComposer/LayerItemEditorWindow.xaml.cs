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
    private bool _loadingTransformFields = true;
    private bool _existingCustomTransform;
    private bool _transformEdited;
    private bool _positionPresetChanged;

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
            ? $"Sliders and ± buttons use one frame ({framesPerSecond:0.###} fps); typed values stay exact."
            : $"Sliders and ± buttons use {_snapSeconds:0.###} second increments; typed values stay exact.";
        var defaultDuration = TimeSpan.FromSeconds(Math.Max(
            _snapSeconds,
            Math.Min(5, projectDuration.TotalSeconds)));
        TimeRangeEditor.Configure(TimeSpan.Zero, defaultDuration, projectDuration, _snapSeconds);
        FontSizeEditor.SetValue(42);
        VolumeEditor.SetValue(0.35);
        FadeInEditor.Minimum = 0;
        FadeInEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        FadeInEditor.Step = _snapSeconds;
        FadeInEditor.SetValue(0);
        FadeOutEditor.Minimum = 0;
        FadeOutEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        FadeOutEditor.Step = _snapSeconds;
        FadeOutEditor.SetValue(0);
        OverlayFadeInEditor.Minimum = 0;
        OverlayFadeInEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        OverlayFadeInEditor.Step = _snapSeconds;
        OverlayFadeInEditor.SetValue(0);
        OverlayFadeOutEditor.Minimum = 0;
        OverlayFadeOutEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        OverlayFadeOutEditor.Step = _snapSeconds;
        OverlayFadeOutEditor.SetValue(0);
        ProgressHeightEditor.SetValue(10);
        PositionComboBox.ItemsSource = Enum.GetValues<OverlayPosition>();
        PositionComboBox.SelectedItem = OverlayPosition.Center;
        SetTransformEditorValues(0.5, 0.5, 1, 0);
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
        ConfigureFields();
        if (selectedSegmentStart.HasValue && selectedSegmentDuration > TimeSpan.Zero)
        {
            TimeRangeEditor.Configure(
                selectedSegmentStart.Value,
                selectedSegmentDuration.Value,
                projectDuration,
                _snapSeconds);
            if (_kind == LayerEditorKind.Progress)
            {
                ProgressTimingComboBox.SelectedItem = ProgressTimeMode.SourceSegment;
            }
        }

        _loadingTransformFields = false;
    }

    public LayerItemEditorWindow(
        ProjectTimelineItem item,
        TimeSpan projectDuration,
        string customFontFolder,
        TimelineSnapMode snapMode,
        double framesPerSecond)
        : this(GetEditorKind(item.Kind), projectDuration, customFontFolder, snapMode, framesPerSecond)
    {
        _loadingTransformFields = true;
        _existingId = item.Id;
        _existingCustomTransform = item.HasCustomOverlayTransform;
        TitleText.Text = $"Edit {GetDisplayName(_kind)}";
        AddButton.Content = "Apply";
        SourceTextBox.Text = item.SourcePath;
        OverlayTextBox.Text = item.Text;
        TimeRangeEditor.Configure(item.Start, item.Duration, projectDuration, _snapSeconds);
        VolumeEditor.SetValue(item.Volume);
        FadeInEditor.SetValue(item.FadeInSeconds);
        FadeOutEditor.SetValue(item.FadeOutSeconds);
        OverlayFadeInEditor.SetValue(item.FadeInSeconds);
        OverlayFadeOutEditor.SetValue(item.FadeOutSeconds);
        FontSizeEditor.SetValue(item.FontSize);
        PositionComboBox.SelectedItem = item.Position;
        var (overlayX, overlayY) = item.HasCustomOverlayTransform
            ? (item.OverlayX, item.OverlayY)
            : OverlayTransformValues.GetPresetCenter(item.Position);
        SetTransformEditorValues(
            overlayX,
            overlayY,
            item.HasCustomOverlayTransform ? item.OverlayScale : 1,
            item.HasCustomOverlayTransform ? item.OverlayRotationDegrees : 0);
        ProgressTimingComboBox.SelectedItem = item.ProgressTimeMode;
        ProgressStyleComboBox.SelectedItem = item.ProgressBarStyle;
        ProgressPositionComboBox.SelectedItem = item.ProgressBarPosition;
        ProgressColorTextBox.Text = item.ProgressColor;
        ProgressHeightEditor.SetValue(item.ProgressHeight);

        if (_kind == LayerEditorKind.Text && FontComboBox.ItemsSource is IEnumerable<FontChoice> fonts)
        {
            FontComboBox.SelectedItem = fonts.FirstOrDefault(font =>
                (!string.IsNullOrWhiteSpace(item.FontPath) &&
                 font.FilePath.Equals(item.FontPath, StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrWhiteSpace(item.FontPath) &&
                 font.FamilyName.Equals(item.FontFamily, StringComparison.OrdinalIgnoreCase))) ??
                FontComboBox.SelectedItem;
        }

        _loadingTransformFields = false;
    }

    public ProjectTrackKind TrackKind => _kind switch
    {
        LayerEditorKind.Audio => ProjectTrackKind.Audio,
        LayerEditorKind.Progress => ProjectTrackKind.Progress,
        _ => ProjectTrackKind.Overlay
    };

    public ProjectTimelineItem? ResultItem { get; private set; }

    public void ApplyProgressTemplate(ProjectTimelineItem item)
    {
        if (_kind != LayerEditorKind.Progress || item.Kind != ProjectItemKind.ProgressBar)
        {
            return;
        }

        TimeRangeEditor.Configure(item.Start, item.Duration, _projectDuration, _snapSeconds);
        ProgressTimingComboBox.SelectedItem = item.ProgressTimeMode;
        ProgressStyleComboBox.SelectedItem = item.ProgressBarStyle;
        ProgressPositionComboBox.SelectedItem = item.ProgressBarPosition;
        ProgressColorTextBox.Text = item.ProgressColor;
        ProgressHeightEditor.SetValue(item.ProgressHeight);
        Tag = item.Name;
    }

    private void ConfigureFields()
    {
        TitleText.Text = $"Add {GetDisplayName(_kind)}";
        SourceFields.Visibility = Visible(_kind is LayerEditorKind.Image or LayerEditorKind.Audio);
        TextFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        FontFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        FontSizeField.Visibility = Visible(_kind == LayerEditorKind.Text);
        TextPlacementFields.Visibility = Visible(_kind is LayerEditorKind.Text or LayerEditorKind.Image);
        OverlayFadeFields.Visibility = Visible(_kind is LayerEditorKind.Text or LayerEditorKind.Image);
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
        var fontSizeValid = int.TryParse(FontSizeEditor.Text, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var fontSize) && fontSize is >= 8 and <= 240;
        var volumeValid = TryParse(VolumeEditor.Text, 0, 4, out var volume);
        var fadeInValid = TryParse(FadeInEditor.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var fadeIn);
        var fadeOutValid = TryParse(FadeOutEditor.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var fadeOut);
        var overlayFadeInValid = TryParse(
            OverlayFadeInEditor.Text,
            0,
            TimeSpan.MaxValue.TotalSeconds,
            out var overlayFadeIn);
        var overlayFadeOutValid = TryParse(
            OverlayFadeOutEditor.Text,
            0,
            TimeSpan.MaxValue.TotalSeconds,
            out var overlayFadeOut);
        var progressHeightValid = int.TryParse(ProgressHeightEditor.Text, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var progressHeight) && progressHeight is >= 2 and <= 100;
        var progressColorValid = TryNormalizeColor(ProgressColorTextBox.Text, out var progressColor);
        var overlayXValid = TryParse(
            OverlayXEditor.Text,
            OverlayTransformValues.MinimumCoordinate * 100,
            OverlayTransformValues.MaximumCoordinate * 100,
            out var overlayXPercent);
        var overlayYValid = TryParse(
            OverlayYEditor.Text,
            OverlayTransformValues.MinimumCoordinate * 100,
            OverlayTransformValues.MaximumCoordinate * 100,
            out var overlayYPercent);
        var overlayScaleValid = TryParse(
            OverlayScaleEditor.Text,
            OverlayTransformValues.MinimumScale * 100,
            OverlayTransformValues.MaximumScale * 100,
            out var overlayScalePercent);
        var overlayRotationValid = TryParse(
            OverlayRotationEditor.Text,
            -360000,
            360000,
            out var overlayRotation);

        if (!TimeRangeEditor.TryGetRange(out var startTime, out var durationTime) ||
            (_kind == LayerEditorKind.Text &&
             (string.IsNullOrWhiteSpace(OverlayTextBox.Text) || !fontSizeValid || FontComboBox.SelectedItem is not FontChoice)) ||
            (_kind is LayerEditorKind.Image or LayerEditorKind.Audio && !File.Exists(SourceTextBox.Text)) ||
            (_kind is LayerEditorKind.Text or LayerEditorKind.Image &&
             (!overlayXValid || !overlayYValid || !overlayScaleValid || !overlayRotationValid ||
              !overlayFadeInValid || !overlayFadeOutValid ||
              overlayFadeIn > durationTime.TotalSeconds || overlayFadeOut > durationTime.TotalSeconds)) ||
            (_kind == LayerEditorKind.Audio && (!volumeValid || !fadeInValid || !fadeOutValid ||
                                                 fadeIn > durationTime.TotalSeconds || fadeOut > durationTime.TotalSeconds)) ||
            (_kind == LayerEditorKind.Progress && (!progressHeightValid || !progressColorValid)))
        {
            MessageBox.Show(this,
                "Check the required source/text, start, positive duration, font size 8–240, overlay transform/fades, audio values, and progress color/height.",
                "Invalid layer item", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var start = startTime.TotalSeconds;
        var duration = durationTime.TotalSeconds;

        var progressMode = ProgressTimingComboBox.SelectedItem is ProgressTimeMode timing
            ? timing
            : ProgressTimeMode.CustomRange;
        if (_kind == LayerEditorKind.Progress && progressMode == ProgressTimeMode.WholeProject)
        {
            start = 0;
            duration = Math.Max(0.1, _projectDuration.TotalSeconds);
        }

        var font = FontComboBox.SelectedItem as FontChoice;
        var position = PositionComboBox.SelectedItem is OverlayPosition selectedPosition
            ? selectedPosition
            : OverlayPosition.Center;
        var useCustomTransform = _kind is LayerEditorKind.Text or LayerEditorKind.Image &&
                                 (_transformEdited || (!_positionPresetChanged && _existingCustomTransform));
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
            LayerEditorKind.Progress => Tag as string ?? "Progress bar",
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
            Position = position,
            HasCustomOverlayTransform = useCustomTransform,
            OverlayX = OverlayTransformValues.NormalizeCoordinate(overlayXPercent / 100),
            OverlayY = OverlayTransformValues.NormalizeCoordinate(overlayYPercent / 100),
            OverlayScale = OverlayTransformValues.NormalizeScale(overlayScalePercent / 100),
            OverlayRotationDegrees = OverlayTransformValues.NormalizeRotation(overlayRotation),
            Volume = _kind == LayerEditorKind.Audio ? volume : 1,
            FadeInSeconds = _kind == LayerEditorKind.Audio
                ? fadeIn
                : _kind is LayerEditorKind.Text or LayerEditorKind.Image ? overlayFadeIn : 0,
            FadeOutSeconds = _kind == LayerEditorKind.Audio
                ? fadeOut
                : _kind is LayerEditorKind.Text or LayerEditorKind.Image ? overlayFadeOut : 0,
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

    private void PositionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingTransformFields || PositionComboBox.SelectedItem is not OverlayPosition position)
        {
            return;
        }

        var (x, y) = OverlayTransformValues.GetPresetCenter(position);
        _loadingTransformFields = true;
        SetTransformEditorValues(x, y, 1, 0);
        _loadingTransformFields = false;
        _positionPresetChanged = true;
        _transformEdited = false;
    }

    private void TransformEditor_Edited(object? sender, EventArgs e)
    {
        if (!_loadingTransformFields)
        {
            _transformEdited = true;
            _positionPresetChanged = false;
        }
    }

    private void SetTransformEditorValues(double x, double y, double scale, double rotation)
    {
        OverlayXEditor.SetValue(x * 100);
        OverlayYEditor.SetValue(y * 100);
        OverlayScaleEditor.SetValue(scale * 100);
        OverlayRotationEditor.SetValue(rotation);
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
