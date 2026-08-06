using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Presentation;
using Microsoft.Win32;

namespace CatClipComposer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IMediaCatalog _catalog;

    public MainWindow(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        IMediaCatalog catalog,
        IMediaScanner scanner,
        ICompositionExporter compositionExporter)
    {
        InitializeComponent();
        _catalog = catalog;
        _viewModel = new MainViewModel(settings, settingsStore, catalog, scanner, compositionExporter);
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _viewModel.CancelOperation();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            ShowError("Could not load the media catalog.", exception);
        }
    }

    private async void Options_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OptionsWindow(_viewModel.Settings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultSettings is null)
        {
            return;
        }

        try
        {
            await _viewModel.ApplySettingsAsync(dialog.ResultSettings);
        }
        catch (Exception exception)
        {
            ShowError("Could not save the options.", exception);
        }
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HistoryWindow(_catalog) { Owner = this };
        dialog.ShowDialog();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        if (_viewModel.Settings.SourceFolders.Count == 0)
        {
            MessageBox.Show(
                this,
                "Add at least one source folder in Options before updating the catalog.",
                "No source folders",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Options_Click(sender, e);
            return;
        }

        try
        {
            var result = await _viewModel.ScanAsync();
            if (result.Errors.Count > 0)
            {
                var shownErrors = string.Join(Environment.NewLine, result.Errors.Take(8));
                var remaining = result.Errors.Count > 8
                    ? $"{Environment.NewLine}…and {result.Errors.Count - 8} more."
                    : string.Empty;
                MessageBox.Show(
                    this,
                    $"The scan completed, but some items could not be processed:{Environment.NewLine}{Environment.NewLine}{shownErrors}{remaining}",
                    "Scan completed with warnings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowError("The catalog scan failed.", exception);
        }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _viewModel.CancelOperation();

    private void CatalogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        _viewModel.AddSelectedToTimeline();

    private void CatalogListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PreviewPlayer.Stop();
        PreviewPlayer.Source = _viewModel.SelectedMedia is null
            ? null
            : new Uri(_viewModel.SelectedMedia.FullPath, UriKind.Absolute);
    }

    private void PreviewPlay_Click(object sender, RoutedEventArgs e) => PreviewPlayer.Play();

    private void PreviewPause_Click(object sender, RoutedEventArgs e) => PreviewPlayer.Pause();

    private void PreviewPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Windows could not preview this codec. The file can still be cataloged and processed by FFmpeg.",
            "Preview unavailable",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenSource_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedMedia is null)
        {
            return;
        }

        ShowInExplorer(_viewModel.SelectedMedia.FullPath);
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e) =>
        _viewModel.AddSelectedToTimeline();

    private void AddStillScreen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new StillScreenWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ImagePath is not null)
        {
            _viewModel.AddStillImageToTimeline(dialog.ImagePath, dialog.Duration);
        }
    }

    private void OpenLastOutput_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = _viewModel.SelectedMedia?.Media.LastOutputPath;
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
        {
            MessageBox.Show(
                this,
                "There is no available exported compilation recorded for this clip yet.",
                "No previous output",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        ShowInExplorer(outputPath);
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        if (_viewModel.Timeline.Clips.Count == 0)
        {
            MessageBox.Show(
                this,
                "Add at least one clip to the timeline before exporting.",
                "Empty timeline",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(_viewModel.Settings.OutputFolder);
            var dialog = new SaveFileDialog
            {
                Title = "Export compilation",
                InitialDirectory = _viewModel.Settings.OutputFolder,
                FileName = $"CatCompilation-{DateTime.Now:yyyyMMdd-HHmm}.mp4",
                DefaultExt = ".mp4",
                Filter = "MP4 video (*.mp4)|*.mp4",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var result = await _viewModel.ExportAsync(dialog.FileName);
            if (MessageBox.Show(
                    this,
                    $"Compilation saved successfully:{Environment.NewLine}{Environment.NewLine}{result.OutputPath}{Environment.NewLine}{Environment.NewLine}Show it in File Explorer?",
                    "Export complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                ShowInExplorer(result.OutputPath);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowError("The compilation could not be exported.", exception);
        }
    }

    private void MoveLeft_Click(object sender, RoutedEventArgs e) =>
        _viewModel.MoveSelectedTimelineClip(-1);

    private void MoveRight_Click(object sender, RoutedEventArgs e) =>
        _viewModel.MoveSelectedTimelineClip(1);

    private void RemoveTimeline_Click(object sender, RoutedEventArgs e) =>
        _viewModel.RemoveSelectedTimelineClip();

    private void ClearTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Timeline.Clips.Count == 0 ||
            MessageBox.Show(
                this,
                "Remove every clip from the compilation timeline?",
                "Clear timeline",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _viewModel.ClearTimeline();
    }

    private void TimelineListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            _viewModel.RemoveSelectedTimelineClip();
            e.Handled = true;
        }
    }

    private static void ShowInExplorer(string filePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add($"/select,{filePath}");
        Process.Start(startInfo);
    }

    private void ShowError(string message, Exception exception) =>
        MessageBox.Show(
            this,
            $"{message}{Environment.NewLine}{Environment.NewLine}{exception.Message}",
            "Cat Clip Composer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
}
