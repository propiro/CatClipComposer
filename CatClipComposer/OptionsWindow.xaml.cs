using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private const string CompatibleFfmpegBuildsUrl =
        "https://github.com/BtbN/FFmpeg-Builds/releases/tag/latest";
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
        SmallThumbnailSizeTextBox.Text = _workingSettings.SmallThumbnailSize.ToString(CultureInfo.CurrentCulture);
        LargeThumbnailSizeTextBox.Text = _workingSettings.LargeThumbnailSize.ToString(CultureInfo.CurrentCulture);
        FfmpegPathTextBox.Text = _workingSettings.FfmpegPath;
        CustomFontFolderTextBox.Text = _workingSettings.CustomFontFolder;
        IncludeSubfoldersCheckBox.IsChecked = _workingSettings.IncludeSubfolders;
        ShowFileNamesCheckBox.IsChecked = _workingSettings.ShowFileNames;
        RescanOnStartupCheckBox.IsChecked = _workingSettings.RescanLibraryOnStartup;
        MissingFfmpegNotice.Visibility = File.Exists(GetBundledFfmpegPath())
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public ObservableCollection<string> SourceFolders { get; }

    public ApplicationSettings? ResultSettings { get; private set; }

    private void AddSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose video source folders", Multiselect = true };
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
        foreach (var folder in SourceFoldersList.SelectedItems.Cast<string>().ToList())
        {
            SourceFolders.Remove(folder);
        }
    }

    private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolderIntoTextBox(OutputFolderTextBox, "Choose the final video output folder");

    private void BrowseProjectFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolderIntoTextBox(ProjectFolderTextBox, "Choose the editable project folder");

    private void BrowseMetadataFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolderIntoTextBox(MetadataFolderTextBox, "Choose the catalog and preview metadata folder");

    private void BrowseFontFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolderIntoTextBox(CustomFontFolderTextBox, "Choose the custom font folder");

    private void OpenFontFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = string.IsNullOrWhiteSpace(CustomFontFolderTextBox.Text)
            ? Path.Combine(AppContext.BaseDirectory, "fonts")
            : Path.GetFullPath(CustomFontFolderTextBox.Text.Trim());
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose ffmpeg.exe",
            Filter = "FFmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executable files (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            FfmpegPathTextBox.Text = dialog.FileName;
        }
    }

    private void DownloadFfmpeg_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(CompatibleFfmpegBuildsUrl) { UseShellExecute = true });

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OutputFolderTextBox.Text) ||
            string.IsNullOrWhiteSpace(ProjectFolderTextBox.Text) ||
            string.IsNullOrWhiteSpace(MetadataFolderTextBox.Text) ||
            !int.TryParse(PreviewSlideCountTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture,
                out var previewSlideCount) || previewSlideCount is < 1 or > 24 ||
            !int.TryParse(SmallThumbnailSizeTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture,
                out var smallThumbnailSize) || smallThumbnailSize is < 80 or > 200 ||
            !int.TryParse(LargeThumbnailSizeTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture,
                out var largeThumbnailSize) || largeThumbnailSize is < 140 or > 360 ||
            largeThumbnailSize < smallThumbnailSize + 20)
        {
            MessageBox.Show(this,
                "Choose the output, project, and metadata folders; use 1–24 contact-sheet slides; and choose " +
                "thumbnail widths in range with Large at least 20 px wider than Small.",
                "Invalid preferences", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _workingSettings.SourceFolders = SourceFolders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _workingSettings.OutputFolder = OutputFolderTextBox.Text.Trim();
        _workingSettings.ProjectFolder = ProjectFolderTextBox.Text.Trim();
        _workingSettings.MetadataFolder = MetadataFolderTextBox.Text.Trim();
        _workingSettings.PreviewSlideCount = previewSlideCount;
        _workingSettings.SmallThumbnailSize = smallThumbnailSize;
        _workingSettings.LargeThumbnailSize = largeThumbnailSize;
        _workingSettings.FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPathTextBox.Text)
            ? "ffmpeg.exe"
            : FfmpegPathTextBox.Text.Trim();
        _workingSettings.CustomFontFolder = string.IsNullOrWhiteSpace(CustomFontFolderTextBox.Text)
            ? Path.Combine(AppContext.BaseDirectory, "fonts")
            : CustomFontFolderTextBox.Text.Trim();
        _workingSettings.IncludeSubfolders = IncludeSubfoldersCheckBox.IsChecked == true;
        _workingSettings.ShowFileNames = ShowFileNamesCheckBox.IsChecked == true;
        _workingSettings.RescanLibraryOnStartup = RescanOnStartupCheckBox.IsChecked == true;
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

    private static string GetBundledFfmpegPath() => Path.Combine(
        AppContext.BaseDirectory, "thirdparty", "ffmpeg", "ffmpeg.exe");
}
