using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;
using CatClipComposer.Workspace;
using Microsoft.Win32;

namespace CatClipComposer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IMediaCatalog _catalog;
    private readonly WorkspaceLayoutController _workspaceLayout;
    private Point _catalogDragStart;

    public MainWindow(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        IProjectStore projectStore,
        IMediaCatalog catalog,
        IMediaScanner scanner,
        ICompositionExporter compositionExporter)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _catalog = catalog;
        _viewModel = new MainViewModel(
            settings,
            settingsStore,
            projectStore,
            catalog,
            scanner,
            compositionExporter);
        _workspaceLayout = new WorkspaceLayoutController(
            ContentBrowserPanel,
            PreviewPanel,
            LayersPanel,
            TimelinePanel);
        _workspaceLayout.Apply(settings);
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
            DesktopDialogs.ShowError(this, "Could not load the media catalog.", exception);
        }
    }

    private async void Options_Click(object sender, RoutedEventArgs e)
    {
        var previousMetadataFolder = _viewModel.Settings.MetadataFolder;
        var dialog = new OptionsWindow(_viewModel.Settings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultSettings is null)
        {
            return;
        }

        try
        {
            await _viewModel.ApplySettingsAsync(dialog.ResultSettings);
            _workspaceLayout.Apply(_viewModel.Settings);
            if (!previousMetadataFolder.Equals(
                    _viewModel.Settings.MetadataFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    this,
                    "The metadata folder will be used after the application is restarted. Existing catalog files are not moved automatically.",
                    "Restart required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not save the options.", exception);
        }
    }

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Timeline.Clips.Count > 0 &&
            MessageBox.Show(
                this,
                "Start a new project? The current timeline remains recoverable until the new project is created.",
                "New project",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _viewModel.NewProjectAsync();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not create a new project.", exception);
        }
    }

    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Cat Clip Composer project",
            InitialDirectory = _viewModel.Settings.ProjectFolder,
            Filter = "Cat Clip Composer project (*.ccproject)|*.ccproject|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _viewModel.OpenProjectAsync(dialog.FileName);
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not open the project.", exception);
        }
    }

    private async void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var projectPath = _viewModel.ProjectFilePath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            Directory.CreateDirectory(_viewModel.Settings.ProjectFolder);
            var dialog = new SaveFileDialog
            {
                Title = "Save Cat Clip Composer project",
                InitialDirectory = _viewModel.Settings.ProjectFolder,
                FileName = $"CatProject-{DateTime.Now:yyyyMMdd-HHmm}.ccproject",
                DefaultExt = ".ccproject",
                Filter = "Cat Clip Composer project (*.ccproject)|*.ccproject",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            projectPath = dialog.FileName;
        }

        try
        {
            await _viewModel.SaveProjectAsync(projectPath);
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not save the project.", exception);
        }
    }

    private void OutputSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OutputSettingsWindow(_viewModel.OutputSettings) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultSettings is not null)
        {
            _viewModel.ApplyOutputSettings(dialog.ResultSettings);
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
            DesktopDialogs.ShowError(this, "The catalog scan failed.", exception);
        }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _viewModel.CancelOperation();

    private void CatalogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        _viewModel.AddSelectedToTimeline();

    private void CatalogListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _catalogDragStart = e.GetPosition(CatalogListBox);

    private void CatalogListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _viewModel.SelectedMedia is null)
        {
            return;
        }

        var position = e.GetPosition(CatalogListBox);
        if (Math.Abs(position.X - _catalogDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _catalogDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(typeof(MediaCardViewModel), _viewModel.SelectedMedia);
        DragDrop.DoDragDrop(CatalogListBox, data, DragDropEffects.Copy);
    }

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

        DesktopShell.ShowFileInExplorer(_viewModel.SelectedMedia.FullPath);
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

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !Enum.TryParse<LayerEditorKind>(button.Tag?.ToString(), out var kind))
        {
            return;
        }

        var dialog = new LayerItemEditorWindow(kind, _viewModel.Timeline.Duration) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultItem is not null)
        {
            _viewModel.AddLayerItem(dialog.TrackKind, dialog.ResultItem);
        }
    }

    private void ClipEffects_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Timeline.SelectedClip is null)
        {
            MessageBox.Show(
                this,
                "Select a video or still screen on the main timeline first.",
                "No selected clip",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new ClipEffectsWindow(_viewModel.Timeline.SelectedClip) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.UpdateSelectedClipEffects(
                dialog.FitMode,
                dialog.FadeInSeconds,
                dialog.FadeOutSeconds,
                dialog.Volume);
        }
    }

    private void EditLayer_Click(object sender, RoutedEventArgs e)
    {
        var row = _viewModel.SelectedProjectLayer;
        if (row?.Item is null)
        {
            return;
        }

        if (row.Track.Kind == ProjectTrackKind.Video)
        {
            if (_viewModel.Timeline.Select(row.Item.Id))
            {
                ClipEffects_Click(sender, e);
            }
            return;
        }

        var dialog = new LayerItemEditorWindow(row.Item, _viewModel.Timeline.Duration) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultItem is not null)
        {
            _viewModel.UpdateSelectedLayerItem(dialog.ResultItem);
        }
    }

    private void RemoveLayer_Click(object sender, RoutedEventArgs e) =>
        _viewModel.RemoveSelectedLayerItem();

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

        DesktopShell.ShowFileInExplorer(outputPath);
    }

    private async void ClipMetadata_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedMedia is null)
        {
            return;
        }

        var dialog = new ClipMetadataWindow(_viewModel.SelectedMedia, _catalog) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _viewModel.UpdateSelectedTagsAsync(dialog.Tags);
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not save clip tags.", exception);
        }
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
                DesktopShell.ShowFileInExplorer(result.OutputPath);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "The compilation could not be exported.", exception);
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

    private void TimelineListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(MediaCardViewModel))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void TimelineListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(MediaCardViewModel)) is MediaCardViewModel media)
        {
            _viewModel.AddMediaToTimeline(media);
        }

        e.Handled = true;
    }

    private void BrowserExpand_Click(object sender, RoutedEventArgs e)
    {
        var isExpanded = BrowserBody.Visibility == Visibility.Visible;
        BrowserBody.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
        BrowserBodyRow.Height = isExpanded ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        BrowserExpandButton.Content = isExpanded ? "+" : "-";
    }

    private void PanelDock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !Enum.TryParse<WorkspacePanelKind>(button.Tag?.ToString(), out var panel))
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };
        foreach (var slot in Enum.GetValues<WorkspaceDockSlot>())
        {
            var item = new MenuItem
            {
                Header = $"Move to {slot.ToString().ToLowerInvariant()}",
                Tag = new PanelDockRequest(panel, slot)
            };
            item.Click += MovePanelMenuItem_Click;
            menu.Items.Add(item);
        }

        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private async void MovePanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: PanelDockRequest request })
        {
            return;
        }

        try
        {
            var settings = _viewModel.Settings.Copy();
            WorkspaceLayoutController.MovePanel(settings, request.Panel, request.Slot);
            await _viewModel.ApplySettingsAsync(settings);
            _workspaceLayout.Apply(_viewModel.Settings);
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not save the workspace layout.", exception);
        }
    }

    private sealed record PanelDockRequest(
        WorkspacePanelKind Panel,
        WorkspaceDockSlot Slot);

}
