using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;
using Microsoft.Win32;

namespace CatClipComposer;

public enum LayerEditorKind
{
    Text,
    Image,
    MovingOverlay,
    Audio,
    Progress
}

public partial class LayerItemEditorWindow : Window
{
    private readonly LayerEditorKind _kind;
    private readonly TimeSpan _projectDuration;
    private readonly Guid? _existingId;
    private readonly bool _existingTransformLocked;
    private readonly double _snapSeconds;
    private bool _loadingTransformFields = true;
    private bool _existingCustomTransform;
    private bool _transformEdited;
    private bool _positionPresetChanged;
    private readonly Func<ProjectTimelineItem, IProgress<RenderProgress>, CancellationToken, Task<RenderResult>>? _framePreviewRenderer;
    private readonly TimeSpan? _previewFrame;
    private readonly SemaphoreSlim _framePreviewGate = new(1, 1);
    private EffectFramePreviewWindow? _framePreviewWindow;
    private CancellationTokenSource? _framePreviewCancellation;
    private readonly List<TextOverlayPreset> _textPresets;
    private readonly Func<TextOverlayPreset, Task>? _saveTextPreset;

    private sealed record TextPresetChoice(TextOverlayPreset Preset, BitmapSource Thumbnail)
    {
        public string Name => Preset.Name;
    }

    public LayerItemEditorWindow(
        LayerEditorKind kind,
        TimeSpan projectDuration,
        string customFontFolder,
        TimelineSnapMode snapMode,
        double framesPerSecond,
        TimeSpan? selectedSegmentStart = null,
        TimeSpan? selectedSegmentDuration = null,
        TimeSpan? previewFrame = null,
        Func<ProjectTimelineItem, IProgress<RenderProgress>, CancellationToken, Task<RenderResult>>? framePreviewRenderer = null,
        IReadOnlyList<TextOverlayPreset>? textPresets = null,
        Func<TextOverlayPreset, Task>? saveTextPreset = null)
    {
        _previewFrame = previewFrame;
        _framePreviewRenderer = framePreviewRenderer;
        _textPresets = textPresets?.Select(ClonePreset).ToList() ?? [];
        _saveTextPreset = saveTextPreset;
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        PreviewFrameButton.IsEnabled = previewFrame.HasValue && framePreviewRenderer is not null;
        if (!PreviewFrameButton.IsEnabled)
        {
            PreviewFrameButton.ToolTip = "Add project video content before prerendering this item's frame with its background.";
        }
        LocationChanged += (_, _) => _framePreviewWindow?.SnapBeside(this);
        SizeChanged += (_, _) => _framePreviewWindow?.SnapBeside(this);
        Closed += (_, _) => CloseFramePreview();
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
        TextStrokeEnabledCheckBox.IsChecked = true;
        TextStrokeColorTextBox.Text = "#000000";
        TextStrokeWidthEditor.SetValue(3);
        TextStrokeSmoothnessEditor.SetValue(0);
        VolumeEditor.SetValue(0.35);
        FadeInEditor.Minimum = 0;
        FadeInEditor.IsTimeValue = true;
        FadeInEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        FadeInEditor.Step = _snapSeconds;
        FadeInEditor.SetValue(0);
        FadeOutEditor.Minimum = 0;
        FadeOutEditor.IsTimeValue = true;
        FadeOutEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        FadeOutEditor.Step = _snapSeconds;
        FadeOutEditor.SetValue(0);
        OverlayFadeInEditor.Minimum = 0;
        OverlayFadeInEditor.IsTimeValue = true;
        OverlayFadeInEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        OverlayFadeInEditor.Step = _snapSeconds;
        OverlayFadeInEditor.SetValue(0);
        OverlayFadeOutEditor.Minimum = 0;
        OverlayFadeOutEditor.IsTimeValue = true;
        OverlayFadeOutEditor.Maximum = Math.Max(_snapSeconds, projectDuration.TotalSeconds);
        OverlayFadeOutEditor.Step = _snapSeconds;
        OverlayFadeOutEditor.SetValue(0);
        ProgressHeightEditor.SetValue(10);
        PositionComboBox.ItemsSource = Enum.GetValues<OverlayPosition>();
        PositionComboBox.SelectedItem = OverlayPosition.Center;
        OverlayOpacityEditor.SetValue(100);
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
        RefreshTextPresetChoices();
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
        double framesPerSecond,
        TimeSpan? previewFrame = null,
        Func<ProjectTimelineItem, IProgress<RenderProgress>, CancellationToken, Task<RenderResult>>? framePreviewRenderer = null,
        IReadOnlyList<TextOverlayPreset>? textPresets = null,
        Func<TextOverlayPreset, Task>? saveTextPreset = null)
        : this(
            GetEditorKind(item.Kind),
            projectDuration,
            customFontFolder,
            snapMode,
            framesPerSecond,
            previewFrame: previewFrame,
            framePreviewRenderer: framePreviewRenderer,
            textPresets: textPresets,
            saveTextPreset: saveTextPreset)
    {
        _loadingTransformFields = true;
        _existingId = item.Id;
        _existingTransformLocked = item.IsTransformLocked;
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
        OverlayOpacityEditor.SetValue(item.OverlayOpacity * 100);
        FontSizeEditor.SetValue(item.FontSize);
        TextStrokeEnabledCheckBox.IsChecked = item.TextStrokeEnabled;
        TextStrokeColorTextBox.Text = item.TextStrokeColor;
        TextStrokeWidthEditor.SetValue(item.TextStrokeWidth);
        TextStrokeSmoothnessEditor.SetValue(item.TextStrokeSmoothness);
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
        SourceFields.Visibility = Visible(_kind is LayerEditorKind.Image or LayerEditorKind.MovingOverlay or LayerEditorKind.Audio);
        ImagePreviewFields.Visibility = Visible(_kind is LayerEditorKind.Image or LayerEditorKind.MovingOverlay);
        TextFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        FontFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        FontSizeField.Visibility = Visible(_kind == LayerEditorKind.Text);
        TextStrokeFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        TextPresetFields.Visibility = Visible(_kind == LayerEditorKind.Text);
        OverlayOpacityField.Visibility = Visible(_kind is LayerEditorKind.Text or LayerEditorKind.Image or LayerEditorKind.MovingOverlay);
        TextPlacementFields.Visibility = Visible(_kind is LayerEditorKind.Text or LayerEditorKind.Image or LayerEditorKind.MovingOverlay);
        OverlayFadeFields.Visibility = Visible(_kind is LayerEditorKind.Text or LayerEditorKind.Image or LayerEditorKind.MovingOverlay);
        AudioFields.Visibility = Visible(_kind == LayerEditorKind.Audio);
        ProgressFields.Visibility = Visible(_kind == LayerEditorKind.Progress);
        if (_kind == LayerEditorKind.Audio)
        {
            SourceLabel.Text = "Music / audio file";
        }
        else if (_kind == LayerEditorKind.Image)
        {
            SourceLabel.Text = "PNG / image file";
            UpdateImagePreview();
        }
        else if (_kind == LayerEditorKind.MovingOverlay)
        {
            SourceLabel.Text = "GIF / video file";
            UpdateImagePreview();
        }
    }

    private void ApplyTextPreset_Click(object sender, RoutedEventArgs e)
    {
        if (TextPresetComboBox.SelectedItem is not TextPresetChoice choice)
        {
            return;
        }

        var preset = choice.Preset;
        _loadingTransformFields = true;
        OverlayTextBox.Text = preset.Text;
        FontSizeEditor.SetValue(preset.FontSize);
        TextStrokeEnabledCheckBox.IsChecked = preset.StrokeEnabled;
        TextStrokeColorTextBox.Text = preset.StrokeColor;
        TextStrokeWidthEditor.SetValue(preset.StrokeWidth);
        TextStrokeSmoothnessEditor.SetValue(preset.StrokeSmoothness);
        PositionComboBox.SelectedItem = preset.Position;
        if (FontComboBox.ItemsSource is IEnumerable<FontChoice> fonts)
        {
            FontComboBox.SelectedItem = fonts.FirstOrDefault(font =>
                (!string.IsNullOrWhiteSpace(preset.FontPath) &&
                 font.FilePath.Equals(preset.FontPath, StringComparison.OrdinalIgnoreCase)) ||
                font.FamilyName.Equals(preset.FontFamily, StringComparison.OrdinalIgnoreCase)) ??
                FontComboBox.SelectedItem;
        }

        SetTransformEditorValues(preset.X, preset.Y, preset.Scale, preset.RotationDegrees);
        OverlayOpacityEditor.SetValue(preset.Opacity * 100);
        OverlayFadeInEditor.SetValue(preset.FadeInSeconds);
        OverlayFadeOutEditor.SetValue(preset.FadeOutSeconds);
        _existingCustomTransform = preset.HasCustomTransform;
        _transformEdited = preset.HasCustomTransform;
        _positionPresetChanged = false;
        _loadingTransformFields = false;
    }

    private async void SaveTextPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_kind != LayerEditorKind.Text || _saveTextPreset is null)
        {
            return;
        }

        if (!TryCreateTextPreset(out var preset, out var error))
        {
            MessageBox.Show(this, error, "Cannot save text preset", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _saveTextPreset(preset);
            var index = _textPresets.FindIndex(candidate =>
                candidate.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _textPresets[index] = ClonePreset(preset);
            }
            else
            {
                _textPresets.Add(ClonePreset(preset));
            }

            RefreshTextPresetChoices(preset.Name);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not save text preset",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool TryCreateTextPreset(out TextOverlayPreset preset, out string error)
    {
        preset = null!;
        error = string.Empty;
        var text = OverlayTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text) || FontComboBox.SelectedItem is not FontChoice font ||
            !int.TryParse(FontSizeEditor.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fontSize) ||
            fontSize is < 8 or > 240 ||
            !TryParse(OverlayXEditor.Text, OverlayTransformValues.MinimumCoordinate * 100,
                OverlayTransformValues.MaximumCoordinate * 100, out var x) ||
            !TryParse(OverlayYEditor.Text, OverlayTransformValues.MinimumCoordinate * 100,
                OverlayTransformValues.MaximumCoordinate * 100, out var y) ||
            !TryParse(OverlayScaleEditor.Text, OverlayTransformValues.MinimumScale * 100,
                OverlayTransformValues.MaximumScale * 100, out var scale) ||
            !TryParse(OverlayRotationEditor.Text, -360000, 360000, out var rotation) ||
            !TryParse(OverlayOpacityEditor.Text, 0, 100, out var opacity) ||
            !TryNormalizeColor(TextStrokeColorTextBox.Text, out var strokeColor) ||
            !TryParse(TextStrokeWidthEditor.Text, 0, 20, out var strokeWidth) ||
            !TryParse(TextStrokeSmoothnessEditor.Text, 0, 10, out var strokeSmoothness) ||
            !TryParse(OverlayFadeInEditor.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var fadeIn) ||
            !TryParse(OverlayFadeOutEditor.Text, 0, TimeSpan.MaxValue.TotalSeconds, out var fadeOut))
        {
            error = "Enter text and valid font, transform, opacity, and fade values first.";
            return false;
        }

        var oneLine = string.Join(" ", text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        var baseName = oneLine.Length <= 42 ? oneLine : $"{oneLine[..39]}...";
        var selectedPreset = (TextPresetComboBox.SelectedItem as TextPresetChoice)?.Preset;
        var name = selectedPreset?.Name ?? baseName;
        if (selectedPreset is null)
        {
            for (var suffix = 2; _textPresets.Any(candidate =>
                     candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); suffix++)
            {
                name = $"{baseName} ({suffix})";
            }
        }

        preset = new TextOverlayPreset
        {
            Id = selectedPreset?.Id ?? Guid.NewGuid(),
            Name = name,
            Text = text,
            FontPath = font.FilePath,
            FontFamily = font.FamilyName,
            FontSize = fontSize,
            StrokeEnabled = TextStrokeEnabledCheckBox.IsChecked == true,
            StrokeColor = strokeColor,
            StrokeWidth = strokeWidth,
            StrokeSmoothness = strokeSmoothness,
            Position = PositionComboBox.SelectedItem is OverlayPosition position ? position : OverlayPosition.Center,
            HasCustomTransform = _transformEdited || _existingCustomTransform,
            X = x / 100,
            Y = y / 100,
            Scale = scale / 100,
            RotationDegrees = rotation,
            Opacity = opacity / 100,
            FadeInSeconds = fadeIn,
            FadeOutSeconds = fadeOut
        };
        return true;
    }

    private void RefreshTextPresetChoices(string? selectedName = null)
    {
        var choices = _textPresets
            .OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(preset => new TextPresetChoice(preset, CreateTextPresetThumbnail(preset)))
            .ToList();
        TextPresetComboBox.ItemsSource = choices;
        TextPresetComboBox.SelectedItem = choices.FirstOrDefault(choice =>
            choice.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
    }

    private static BitmapSource CreateTextPresetThumbnail(TextOverlayPreset preset)
    {
        const int width = 180;
        const int height = 64;
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(28, 28, 26)), null, new Rect(0, 0, width, height));
            var formatted = new FormattedText(
                preset.Text.ReplaceLineEndings(" "),
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily(string.IsNullOrWhiteSpace(preset.FontFamily) ? "Segoe UI" : preset.FontFamily),
                    FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                Math.Clamp(preset.FontSize * 0.45, 12, 28),
                Brushes.White,
                1)
            {
                MaxTextWidth = width - 16,
                MaxTextHeight = height - 12,
                Trimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center
            };
            drawing.PushClip(new RectangleGeometry(new Rect(6, 6, width - 12, height - 12)));
            var origin = new Point(8, Math.Max(6, (height - formatted.Height) / 2));
            var geometry = formatted.BuildGeometry(origin);
            Pen? stroke = null;
            if (preset.StrokeEnabled && preset.StrokeWidth > 0)
            {
                var color = (Color)ColorConverter.ConvertFromString(preset.StrokeColor);
                stroke = new Pen(new SolidColorBrush(color), Math.Clamp(preset.StrokeWidth * 0.45, 0.5, 9))
                {
                    LineJoin = PenLineJoin.Round
                };
            }

            drawing.DrawGeometry(Brushes.White, stroke, geometry);
            drawing.Pop();
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static TextOverlayPreset ClonePreset(TextOverlayPreset preset) => new()
    {
        Id = preset.Id,
        Name = preset.Name,
        Text = preset.Text,
        FontPath = preset.FontPath,
        FontFamily = preset.FontFamily,
        FontSize = preset.FontSize,
        StrokeEnabled = preset.StrokeEnabled,
        StrokeColor = preset.StrokeColor,
        StrokeWidth = preset.StrokeWidth,
        StrokeSmoothness = preset.StrokeSmoothness,
        Position = preset.Position,
        HasCustomTransform = preset.HasCustomTransform,
        X = preset.X,
        Y = preset.Y,
        Scale = preset.Scale,
        RotationDegrees = preset.RotationDegrees,
        Opacity = preset.Opacity,
        FadeInSeconds = preset.FadeInSeconds,
        FadeOutSeconds = preset.FadeOutSeconds
    };

    private static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = _kind switch
            {
                LayerEditorKind.Audio => "Choose music/audio",
                LayerEditorKind.MovingOverlay => "Choose GIF/video overlay",
                _ => "Choose PNG/JPEG overlay"
            },
            Filter = _kind switch
            {
                LayerEditorKind.Audio => "Audio files (*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac)|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac|All files (*.*)|*.*",
                LayerEditorKind.MovingOverlay => "Moving image/video (*.gif;*.mp4;*.webm;*.mov;*.mkv;*.avi;*.m4v)|*.gif;*.mp4;*.webm;*.mov;*.mkv;*.avi;*.m4v|All files (*.*)|*.*",
                _ => "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
            },
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            SourceTextBox.Text = dialog.FileName;
        }
    }

    private void SourceTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_kind is LayerEditorKind.Image or LayerEditorKind.MovingOverlay)
        {
            UpdateImagePreview();
        }
    }

    private void OverlayOpacityEditor_Edited(object? sender, EventArgs e)
    {
        if (_kind is LayerEditorKind.Image or LayerEditorKind.MovingOverlay &&
            TryParse(OverlayOpacityEditor.Text, 0, 100, out var opacityPercent))
        {
            ImageOverlayPreview.Opacity = opacityPercent / 100;
        }
    }

    private void UpdateImagePreview()
    {
        ImageOverlayPreview.Source = null;
        ImagePreviewStatusText.Visibility = Visibility.Visible;
        var path = SourceTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ImagePreviewStatusText.Text = _kind == LayerEditorKind.MovingOverlay
                ? "Choose a GIF or video. GIFs show their first frame here; use frame prerender for video content."
                : "Choose a PNG or image to preview it here.";
            return;
        }

        if (!File.Exists(path))
        {
            ImagePreviewStatusText.Text = "Image file not found.";
            return;
        }

        try
        {
            var preview = new BitmapImage();
            preview.BeginInit();
            preview.CacheOption = BitmapCacheOption.OnLoad;
            preview.DecodePixelWidth = 900;
            preview.UriSource = new Uri(path, UriKind.Absolute);
            preview.EndInit();
            preview.Freeze();
            ImageOverlayPreview.Source = preview;
            if (TryParse(OverlayOpacityEditor.Text, 0, 100, out var opacityPercent))
            {
                ImageOverlayPreview.Opacity = opacityPercent / 100;
            }
            ImagePreviewStatusText.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            ImagePreviewStatusText.Text = _kind == LayerEditorKind.MovingOverlay
                ? "Use Prerender frame with background to preview this moving file."
                : "This image cannot be previewed.";
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateResultItem(out var resultItem, out var error))
        {
            MessageBox.Show(this, error, "Invalid layer item", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultItem = resultItem;
        DialogResult = true;
    }

    private bool TryCreateResultItem(out ProjectTimelineItem resultItem, out string error)
    {
        resultItem = null!;
        error = string.Empty;
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
        var overlayOpacityValid = TryParse(OverlayOpacityEditor.Text, 0, 100, out var overlayOpacityPercent);
        var strokeColorValid = TryNormalizeColor(TextStrokeColorTextBox.Text, out var textStrokeColor);
        var strokeWidthValid = TryParse(TextStrokeWidthEditor.Text, 0, 20, out var textStrokeWidth);
        var strokeSmoothnessValid = TryParse(
            TextStrokeSmoothnessEditor.Text,
            0,
            10,
            out var textStrokeSmoothness);

        if (!TimeRangeEditor.TryGetRange(out var startTime, out var durationTime) ||
            (_kind == LayerEditorKind.Text &&
             (string.IsNullOrWhiteSpace(OverlayTextBox.Text) || !fontSizeValid ||
              FontComboBox.SelectedItem is not FontChoice || !strokeColorValid ||
              !strokeWidthValid || !strokeSmoothnessValid)) ||
            (_kind is LayerEditorKind.Image or LayerEditorKind.MovingOverlay or LayerEditorKind.Audio && !File.Exists(SourceTextBox.Text)) ||
            (_kind is LayerEditorKind.Text or LayerEditorKind.Image or LayerEditorKind.MovingOverlay &&
             (!overlayXValid || !overlayYValid || !overlayScaleValid || !overlayRotationValid || !overlayOpacityValid ||
              !overlayFadeInValid || !overlayFadeOutValid ||
              overlayFadeIn > durationTime.TotalSeconds || overlayFadeOut > durationTime.TotalSeconds)) ||
            (_kind == LayerEditorKind.Audio && (!volumeValid || !fadeInValid || !fadeOutValid ||
                                                 fadeIn > durationTime.TotalSeconds || fadeOut > durationTime.TotalSeconds)) ||
            (_kind == LayerEditorKind.Progress && (!progressHeightValid || !progressColorValid)))
        {
            error = "Check the required source/text, start, positive duration, font size 8–240, text stroke, overlay transform/opacity/fades, audio values, and progress color/height.";
            return false;
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
        var useCustomTransform = _kind is LayerEditorKind.Text or LayerEditorKind.Image or LayerEditorKind.MovingOverlay &&
                                 (_transformEdited || (!_positionPresetChanged && _existingCustomTransform));
        var itemKind = _kind switch
        {
            LayerEditorKind.Text => ProjectItemKind.TextOverlay,
            LayerEditorKind.Image => ProjectItemKind.ImageOverlay,
            LayerEditorKind.MovingOverlay => ProjectItemKind.VideoOverlay,
            LayerEditorKind.Audio => ProjectItemKind.Audio,
            _ => ProjectItemKind.ProgressBar
        };
        var defaultName = _kind switch
        {
            LayerEditorKind.Text => OverlayTextBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Text",
            LayerEditorKind.Progress => Tag as string ?? "Progress bar",
            _ => Path.GetFileName(SourceTextBox.Text)
        };

        resultItem = new ProjectTimelineItem
        {
            Id = _existingId ?? Guid.NewGuid(),
            Kind = itemKind,
            Name = defaultName,
            SourcePath = SourceTextBox.Text,
            StartTicks = TimeSpan.FromSeconds(start).Ticks,
            DurationTicks = TimeSpan.FromSeconds(duration).Ticks,
            IsTransformLocked = _existingTransformLocked,
            Text = OverlayTextBox.Text,
            FontPath = _kind == LayerEditorKind.Text ? font?.FilePath ?? string.Empty : string.Empty,
            FontFamily = _kind == LayerEditorKind.Text ? font?.FamilyName ?? "Segoe UI" : "Segoe UI",
            FontSize = _kind == LayerEditorKind.Text ? fontSize : 42,
            TextStrokeEnabled = _kind == LayerEditorKind.Text && TextStrokeEnabledCheckBox.IsChecked == true,
            TextStrokeColor = _kind == LayerEditorKind.Text ? textStrokeColor : "#000000",
            TextStrokeWidth = _kind == LayerEditorKind.Text ? textStrokeWidth : 3,
            TextStrokeSmoothness = _kind == LayerEditorKind.Text ? textStrokeSmoothness : 0,
            Position = position,
            HasCustomOverlayTransform = useCustomTransform,
            OverlayX = OverlayTransformValues.NormalizeCoordinate(overlayXPercent / 100),
            OverlayY = OverlayTransformValues.NormalizeCoordinate(overlayYPercent / 100),
            OverlayScale = OverlayTransformValues.NormalizeScale(overlayScalePercent / 100),
            OverlayRotationDegrees = OverlayTransformValues.NormalizeRotation(overlayRotation),
            OverlayOpacity = Math.Clamp(overlayOpacityPercent / 100, 0, 1),
            Volume = _kind == LayerEditorKind.Audio ? volume : 1,
            FadeInSeconds = _kind == LayerEditorKind.Audio
                ? fadeIn
                : _kind is LayerEditorKind.Text or LayerEditorKind.Image or LayerEditorKind.MovingOverlay ? overlayFadeIn : 0,
            FadeOutSeconds = _kind == LayerEditorKind.Audio
                ? fadeOut
                : _kind is LayerEditorKind.Text or LayerEditorKind.Image or LayerEditorKind.MovingOverlay ? overlayFadeOut : 0,
            ProgressTimeMode = progressMode,
            ProgressBarStyle = ProgressStyleComboBox.SelectedItem is ProgressBarStyle style ? style : ProgressBarStyle.Solid,
            ProgressBarPosition = ProgressPositionComboBox.SelectedItem is ProgressBarPosition barPosition
                ? barPosition
                : ProgressBarPosition.Bottom,
            ProgressColor = progressColor,
            ProgressHeight = progressHeight
        };
        return true;
    }

    private async void PreviewFrame_Click(object sender, RoutedEventArgs e)
    {
        if (!_previewFrame.HasValue || _framePreviewRenderer is null)
        {
            return;
        }

        EnsureFramePreviewWindow();
        if (!TryCreateResultItem(out var previewItem, out var error))
        {
            _framePreviewWindow?.ShowError(error);
            return;
        }

        _framePreviewCancellation?.Cancel();
        await _framePreviewGate.WaitAsync();
        try
        {
            if (_framePreviewWindow is null)
            {
                return;
            }

            _framePreviewCancellation?.Dispose();
            _framePreviewCancellation = new CancellationTokenSource();
            _framePreviewWindow.SetLoading(_previewFrame.Value);
            await Dispatcher.Yield(DispatcherPriority.Render);
            try
            {
                var progress = new Progress<RenderProgress>(update => _framePreviewWindow?.ReportProgress(update));
                var result = await _framePreviewRenderer(previewItem, progress, _framePreviewCancellation.Token);
                _framePreviewWindow?.ShowPreview(result.OutputPath, _previewFrame.Value);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _framePreviewWindow?.ShowError($"Frame preview failed: {exception.Message}");
            }
        }
        finally
        {
            _framePreviewGate.Release();
        }
    }

    private void EnsureFramePreviewWindow()
    {
        if (_framePreviewWindow is not null)
        {
            _framePreviewWindow.Activate();
            return;
        }

        _framePreviewWindow = new EffectFramePreviewWindow { Owner = this };
        _framePreviewWindow.Closed += (_, _) =>
        {
            _framePreviewCancellation?.Cancel();
            _framePreviewWindow = null;
        };
        _framePreviewWindow.Show();
        _framePreviewWindow.SnapBeside(this);
    }

    private void CloseFramePreview()
    {
        _framePreviewCancellation?.Cancel();
        _framePreviewCancellation?.Dispose();
        _framePreviewCancellation = null;
        if (_framePreviewWindow is not null)
        {
            var window = _framePreviewWindow;
            _framePreviewWindow = null;
            window.Close();
        }
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
        ProjectItemKind.VideoOverlay => LayerEditorKind.MovingOverlay,
        ProjectItemKind.Audio => LayerEditorKind.Audio,
        ProjectItemKind.ProgressBar => LayerEditorKind.Progress,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "This project item is not edited here.")
    };

    private static string GetDisplayName(LayerEditorKind kind) => kind switch
    {
        LayerEditorKind.Image => "PNG / image layer",
        LayerEditorKind.MovingOverlay => "GIF / video layer",
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
