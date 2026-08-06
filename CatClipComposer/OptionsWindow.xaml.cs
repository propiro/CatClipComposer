using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;
using Microsoft.Win32;

namespace CatClipComposer;

public partial class OptionsWindow : Window
{
    private readonly ApplicationSettings _workingSettings;

    public OptionsWindow(ApplicationSettings settings)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _workingSettings = settings.Copy();
        SourceFolders = new ObservableCollection<string>(_workingSettings.SourceFolders);
        DataContext = this;

        OutputFolderTextBox.Text = _workingSettings.OutputFolder;
        ProjectFolderTextBox.Text = _workingSettings.ProjectFolder;
        MetadataFolderTextBox.Text = _workingSettings.MetadataFolder;
        PreviewSlideCountTextBox.Text = _workingSettings.PreviewSlideCount.ToString(CultureInfo.CurrentCulture);
        FfmpegPathTextBox.Text = _workingSettings.FfmpegPath;
        TargetMinutesTextBox.Text = _workingSettings.TargetDurationMinutes.ToString(
            "0.##",
            CultureInfo.CurrentCulture);
        OrientationComboBox.ItemsSource = Enum.GetValues<OutputOrientation>();
        OrientationComboBox.SelectedItem = _workingSettings.Orientation;
        VideoEncoderComboBox.ItemsSource = EncoderChoices;
        VideoEncoderComboBox.SelectedItem = EncoderChoices.First(choice => choice.Value == _workingSettings.VideoEncoder);
        ProgressStyleComboBox.ItemsSource = Enum.GetValues<VideoProgressStyle>();
        ProgressStyleComboBox.SelectedItem = _workingSettings.ProgressStyle;
        OverlayPositionComboBox.ItemsSource = Enum.GetValues<OverlayPosition>();
        OverlayPositionComboBox.SelectedItem = _workingSettings.OverlayPosition;
        OverlayImagePathTextBox.Text = _workingSettings.OverlayImagePath;
        OverlayTextTextBox.Text = _workingSettings.OverlayText;
        OverlayFontPathTextBox.Text = _workingSettings.OverlayFontPath;
        OverlayTextSizeTextBox.Text = _workingSettings.OverlayTextSize.ToString(CultureInfo.CurrentCulture);
        IncludeSubfoldersCheckBox.IsChecked = _workingSettings.IncludeSubfolders;
        ShowFileNamesCheckBox.IsChecked = _workingSettings.ShowFileNames;
    }

    public ObservableCollection<string> SourceFolders { get; }

    public ApplicationSettings? ResultSettings { get; private set; }

    private void AddSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose one or more video source folders",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var folder in dialog.FolderNames)
        {
            if (!SourceFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                SourceFolders.Add(folder);
            }
        }
    }

    private void RemoveSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = SourceFoldersList.SelectedItems.Cast<string>().ToList();
        foreach (var folder in selected)
        {
            SourceFolders.Remove(folder);
        }
    }

    private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the compilation output folder",
            InitialDirectory = Directory.Exists(OutputFolderTextBox.Text)
                ? OutputFolderTextBox.Text
                : null
        };
        if (dialog.ShowDialog(this) == true)
        {
            OutputFolderTextBox.Text = dialog.FolderName;
        }
    }

    private void BrowseProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderIntoTextBox(
            ProjectFolderTextBox,
            "Choose the folder for editable project files");
    }

    private void BrowseMetadataFolder_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderIntoTextBox(
            MetadataFolderTextBox,
            "Choose the catalog, preview, and recovery metadata folder");
    }

    private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose ffmpeg.exe",
            Filter = "FFmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            FfmpegPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseOverlayImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a PNG overlay",
            Filter = "PNG image (*.png)|*.png|Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            OverlayImagePathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseOverlayFont_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a TrueType or OpenType font",
            Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            OverlayFontPathTextBox.Text = dialog.FileName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(
                TargetMinutesTextBox.Text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var targetMinutes) ||
            targetMinutes is < 1 or > 720)
        {
            MessageBox.Show(
                this,
                "Timeline target must be between 1 and 720 minutes.",
                "Invalid timeline target",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TargetMinutesTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolderTextBox.Text))
        {
            MessageBox.Show(
                this,
                "Choose an output folder.",
                "Output folder required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(OverlayTextSizeTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var textSize) ||
            textSize is < 8 or > 200)
        {
            MessageBox.Show(
                this,
                "Overlay text size must be between 8 and 200 pixels.",
                "Invalid text size",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            OverlayTextSizeTextBox.Focus();
            return;
        }

        if (!int.TryParse(
                PreviewSlideCountTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var previewSlideCount) ||
            previewSlideCount is < 1 or > 12)
        {
            MessageBox.Show(
                this,
                "Preview slide count must be between 1 and 12.",
                "Invalid preview slide count",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            PreviewSlideCountTextBox.Focus();
            return;
        }

        _workingSettings.SourceFolders = SourceFolders
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _workingSettings.OutputFolder = OutputFolderTextBox.Text.Trim();
        _workingSettings.ProjectFolder = ProjectFolderTextBox.Text.Trim();
        _workingSettings.MetadataFolder = MetadataFolderTextBox.Text.Trim();
        _workingSettings.PreviewSlideCount = previewSlideCount;
        _workingSettings.FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPathTextBox.Text)
            ? "ffmpeg.exe"
            : FfmpegPathTextBox.Text.Trim();
        _workingSettings.TargetDurationMinutes = targetMinutes;
        _workingSettings.Orientation = OrientationComboBox.SelectedItem is OutputOrientation orientation
            ? orientation
            : OutputOrientation.Landscape;
        _workingSettings.VideoEncoder = VideoEncoderComboBox.SelectedItem is EncoderChoice encoder
            ? encoder.Value
            : VideoEncoderPreset.NativeMpeg4;
        _workingSettings.ProgressStyle = ProgressStyleComboBox.SelectedItem is VideoProgressStyle progressStyle
            ? progressStyle
            : VideoProgressStyle.None;
        _workingSettings.OverlayPosition = OverlayPositionComboBox.SelectedItem is OverlayPosition overlayPosition
            ? overlayPosition
            : OverlayPosition.TopRight;
        _workingSettings.OverlayImagePath = OverlayImagePathTextBox.Text.Trim();
        _workingSettings.OverlayText = OverlayTextTextBox.Text.Trim();
        _workingSettings.OverlayFontPath = OverlayFontPathTextBox.Text.Trim();
        _workingSettings.OverlayTextSize = textSize;
        _workingSettings.IncludeSubfolders = IncludeSubfoldersCheckBox.IsChecked == true;
        _workingSettings.ShowFileNames = ShowFileNamesCheckBox.IsChecked == true;

        ResultSettings = _workingSettings.Copy();
        DialogResult = true;
    }

    private void BrowseFolderIntoTextBox(TextBox textBox, string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = Directory.Exists(textBox.Text) ? textBox.Text : null
        };
        if (dialog.ShowDialog(this) == true)
        {
            textBox.Text = dialog.FolderName;
        }
    }

    private static IReadOnlyList<EncoderChoice> EncoderChoices { get; } =
    [
        new(VideoEncoderPreset.NativeMpeg4, "FFmpeg native MPEG-4 — non-GPL default"),
        new(VideoEncoderPreset.WindowsMediaFoundationH264, "Windows Media Foundation H.264 — non-GPL"),
        new(VideoEncoderPreset.Libx264Gpl, "libx264 H.264 — GPL opt-in")
    ];

    private sealed record EncoderChoice(VideoEncoderPreset Value, string Label)
    {
        public override string ToString() => Label;
    }
}
