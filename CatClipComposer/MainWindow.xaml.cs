using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Core.Plugins;
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
    private Point _timelineDragStart;
    private TimelineLaneItemViewModel? _timelineDragItem;
    private WorkspacePanelKind? _expandedPanel;
    private WorkspacePanelKind _focusedPanel = WorkspacePanelKind.ContentBrowser;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _projectPreviewTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _previewSeekDragging;
    private bool _projectPreviewSeekDragging;
    private bool _timelinePlayheadDragging;
    private bool _allowClose;
    private bool _closeSaveInProgress;

    private sealed record TimelineDragData(IReadOnlyList<Guid> ItemIds);
    private sealed record MediaDragData(IReadOnlyList<MediaCardViewModel> MediaFiles);

    public MainWindow(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        IProjectStore projectStore,
        IMediaCatalog catalog,
        IMediaScanner scanner,
        IVideoRenderer videoRenderer,
        ICompositionExporter compositionExporter,
        IPluginCatalog plugins)
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
            videoRenderer,
            compositionExporter,
            plugins);
        _workspaceLayout = new WorkspaceLayoutController(
            ContentBrowserPanel,
            PreviewPanel,
            LayersPanel,
            TimelinePanel);
        _workspaceLayout.Apply(settings);
        DataContext = _viewModel;
        _previewTimer.Tick += PreviewTimer_Tick;
        _projectPreviewTimer.Tick += ProjectPreviewTimer_Tick;
        Closed += (_, _) =>
        {
            _previewTimer.Stop();
            _projectPreviewTimer.Stop();
            _viewModel.CancelOperation();
        };
    }

    public async Task InitializeAsync(IProgress<StartupProgress>? progress = null)
    {
        try
        {
            await _viewModel.InitializeAsync(progress);
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
            _workspaceLayout.Apply(_viewModel.Settings, _expandedPanel);
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
            DesktopDialogs.ShowError(this, "Could not save the preferences.", exception);
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
            Filter = "Cat Clip Composer project (*.nya)|*.nya|All files (*.*)|*.*",
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
        await SaveProjectWithDialogAsync();
    }

    private async Task<bool> SaveProjectWithDialogAsync()
    {
        var projectPath = _viewModel.ProjectFilePath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            Directory.CreateDirectory(_viewModel.Settings.ProjectFolder);
            var dialog = new SaveFileDialog
            {
                Title = "Save Cat Clip Composer project",
                InitialDirectory = _viewModel.Settings.ProjectFolder,
                FileName = $"CatProject-{DateTime.Now:yyyyMMdd-HHmm}.nya",
                DefaultExt = ".nya",
                Filter = "Cat Clip Composer project (*.nya)|*.nya",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true)
            {
                return false;
            }

            projectPath = dialog.FileName;
        }

        try
        {
            await _viewModel.SaveProjectAsync(projectPath);
            return true;
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not save the project.", exception);
            return false;
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_viewModel.IsDirty)
        {
            return;
        }

        e.Cancel = true;
        if (_closeSaveInProgress)
        {
            return;
        }

        var choice = MessageBox.Show(
            this,
            "Save changes to the current project before closing?",
            "Unsaved project changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);
        if (choice == MessageBoxResult.Cancel)
        {
            return;
        }

        if (choice == MessageBoxResult.No)
        {
            _allowClose = true;
            Close();
            return;
        }

        _closeSaveInProgress = true;
        try
        {
            if (await SaveProjectWithDialogAsync())
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closeSaveInProgress = false;
        }
    }

    private void OutputSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OutputSettingsWindow(
            _viewModel.OutputSettings,
            _viewModel.ProjectName,
            _viewModel.TargetDurationMinutes,
            _viewModel.BackgroundColor,
            _viewModel.ProjectCreatedUtc,
            _viewModel.ProjectFilePath)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true &&
            dialog.ResultSettings is not null &&
            dialog.ResultProjectName is not null)
        {
            _viewModel.ApplyProjectSettings(
                dialog.ResultProjectName,
                dialog.ResultTargetDurationMinutes,
                dialog.ResultBackgroundColor,
                dialog.ResultSettings);
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
                "Add at least one source folder in Preferences before updating the catalog.",
                "No source folders",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Options_Click(sender, e);
            return;
        }

        var refreshDialog = new RefreshLibraryWindow { Owner = this };
        if (refreshDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var splash = new SplashWindow(canCancel: true)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            splash.CancelRequested += (_, _) => _viewModel.CancelOperation();
            splash.Report(new StartupProgress(0, "Discovering video files…"));
            splash.Show();
            IsEnabled = false;
            ScanResult result;
            try
            {
                var progress = new Progress<ScanProgress>(update =>
                {
                    var percent = update.Total == 0 ? 0 : update.Processed * 100d / update.Total;
                    var message = string.IsNullOrWhiteSpace(update.CurrentFile)
                        ? "Finalizing the library catalog…"
                        : $"Scanning {update.Processed + 1} of {update.Total}: {update.CurrentFile}";
                    splash.Report(new StartupProgress(percent, message));
                });
                result = await _viewModel.ScanAsync(refreshDialog.RegeneratePreviews, progress);
                splash.Report(new StartupProgress(100, "Library refresh complete."));
            }
            finally
            {
                await splash.WaitForMinimumDisplayAsync();
                IsEnabled = true;
                splash.Topmost = false;
                splash.Close();
                Activate();
            }

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
        AddSelectedCatalogItems();

    private void CatalogListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _catalogDragStart = e.GetPosition(CatalogListBox);

    private void CatalogListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || CatalogListBox.SelectedItems.Count == 0)
        {
            return;
        }

        var position = e.GetPosition(CatalogListBox);
        if (Math.Abs(position.X - _catalogDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _catalogDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var selected = CatalogListBox.SelectedItems.Cast<MediaCardViewModel>().ToList();
        var data = new DataObject(typeof(MediaDragData), new MediaDragData(selected));
        DragDrop.DoDragDrop(CatalogListBox, data, DragDropEffects.Copy);
    }

    private void CatalogListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is not null && !item.IsSelected)
        {
            CatalogListBox.SelectedItems.Clear();
            item.IsSelected = true;
        }
    }

    private void CatalogListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogListBox.SelectedItems.Count > 0)
        {
            _viewModel.SelectedMedia = CatalogListBox.SelectedItems
                .Cast<MediaCardViewModel>()
                .Last();
        }

        PreviewPlayer.Stop();
        PreviewPlayer.IsMuted = true;
        PreviewMuteButton.Content = "Unmute";
        PreviewSeekSlider.Value = 0;
        PreviewPositionText.Text = "0:00";
        PreviewDurationText.Text = "0:00";
        PreviewPlayer.Source = _viewModel.SelectedMedia is null
            ? null
            : new Uri(_viewModel.SelectedMedia.FullPath, UriKind.Absolute);
    }

    private void PreviewPlay_Click(object sender, RoutedEventArgs e)
    {
        PreviewPlayer.Play();
        _previewTimer.Start();
    }

    private void PreviewPause_Click(object sender, RoutedEventArgs e)
    {
        PreviewPlayer.Pause();
        _previewTimer.Stop();
    }

    private void PreviewMute_Click(object sender, RoutedEventArgs e)
    {
        PreviewPlayer.IsMuted = !PreviewPlayer.IsMuted;
        PreviewMuteButton.Content = PreviewPlayer.IsMuted ? "Unmute" : "Mute";
    }

    private void PreviewVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreviewPlayer is not null)
        {
            PreviewPlayer.Volume = e.NewValue;
        }
    }

    private void PreviewPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        var duration = PreviewPlayer.NaturalDuration.HasTimeSpan
            ? PreviewPlayer.NaturalDuration.TimeSpan
            : TimeSpan.Zero;
        PreviewSeekSlider.Maximum = Math.Max(0.001, duration.TotalSeconds);
        PreviewDurationText.Text = FormatPreviewTime(duration);
    }

    private void PreviewPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _previewTimer.Stop();
        PreviewPlayer.Position = TimeSpan.Zero;
        UpdatePreviewPosition();
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (!_previewSeekDragging)
        {
            UpdatePreviewPosition();
        }
    }

    private void PreviewSeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _previewSeekDragging = true;

    private void PreviewSeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        PreviewPlayer.Position = TimeSpan.FromSeconds(PreviewSeekSlider.Value);
        _previewSeekDragging = false;
        UpdatePreviewPosition();
    }

    private void UpdatePreviewPosition()
    {
        PreviewSeekSlider.Value = Math.Clamp(
            PreviewPlayer.Position.TotalSeconds,
            PreviewSeekSlider.Minimum,
            PreviewSeekSlider.Maximum);
        PreviewPositionText.Text = FormatPreviewTime(PreviewPlayer.Position);
    }

    private static string FormatPreviewTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");

    private void PreviewPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Windows could not preview this codec. The file can still be cataloged and processed by FFmpeg.",
            "Preview unavailable",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void ProjectPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        try
        {
            ProjectPreviewStatusText.Text = "Rendering layered project preview…";
            PreviewTabs.SelectedItem = ProjectPreviewTab;
            ProjectPreviewPlayer.Stop();
            ProjectPreviewPlayer.Source = null;
            var result = await _viewModel.RenderProjectPreviewAsync();
            ProjectPreviewPlayer.Source = new Uri(result.OutputPath, UriKind.Absolute);
            ProjectPreviewPlayer.IsMuted = true;
            ProjectPreviewMuteButton.Content = "Unmute";
            ProjectPreviewStatusText.Text = "Preview ready — muted by default";
            ProjectPreviewPlayer.Play();
            _projectPreviewTimer.Start();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ProjectPreviewStatusText.Text = "Preview could not be rendered.";
            DesktopDialogs.ShowError(this, "The project preview could not be rendered.", exception);
        }
    }

    private void ProjectPreviewPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        var duration = ProjectPreviewPlayer.NaturalDuration.HasTimeSpan
            ? ProjectPreviewPlayer.NaturalDuration.TimeSpan
            : TimeSpan.Zero;
        ProjectPreviewSeekSlider.Maximum = Math.Max(0.001, duration.TotalSeconds);
        ProjectPreviewDurationText.Text = FormatPreviewTime(duration);
        SeekProjectPreview(_viewModel.Timeline.Playhead);
    }

    private void ProjectPreviewPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _projectPreviewTimer.Stop();
        SeekProjectPreview(TimeSpan.Zero);
    }

    private void ProjectPreviewPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        ProjectPreviewStatusText.Text = "Windows could not play the rendered preview codec.";
        MessageBox.Show(
            this,
            "The preview rendered, but Windows could not play its codec.",
            "Project preview unavailable",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ProjectPreviewPlay_Click(object sender, RoutedEventArgs e)
    {
        ProjectPreviewPlayer.Play();
        _projectPreviewTimer.Start();
    }

    private void ProjectPreviewPause_Click(object sender, RoutedEventArgs e)
    {
        ProjectPreviewPlayer.Pause();
        _projectPreviewTimer.Stop();
    }

    private void ProjectPreviewMute_Click(object sender, RoutedEventArgs e)
    {
        ProjectPreviewPlayer.IsMuted = !ProjectPreviewPlayer.IsMuted;
        ProjectPreviewMuteButton.Content = ProjectPreviewPlayer.IsMuted ? "Unmute" : "Mute";
    }

    private void ProjectPreviewPreviousFrame_Click(object sender, RoutedEventArgs e) => StepProjectPreviewFrame(-1);

    private void ProjectPreviewNextFrame_Click(object sender, RoutedEventArgs e) => StepProjectPreviewFrame(1);

    private void StepProjectPreviewFrame(int direction)
    {
        ProjectPreviewPlayer.Pause();
        _projectPreviewTimer.Stop();
        _viewModel.Timeline.StepFrame(direction);
        SeekProjectPreview(_viewModel.Timeline.Playhead);
    }

    private void ProjectPreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (_projectPreviewSeekDragging)
        {
            return;
        }

        var position = ProjectPreviewPlayer.Position;
        ProjectPreviewSeekSlider.Value = Math.Clamp(
            position.TotalSeconds,
            ProjectPreviewSeekSlider.Minimum,
            ProjectPreviewSeekSlider.Maximum);
        ProjectPreviewPositionText.Text = FormatPreviewTime(position);
        _viewModel.Timeline.SetPlayhead(position);
    }

    private void ProjectPreviewSeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _projectPreviewSeekDragging = true;

    private void ProjectPreviewSeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _projectPreviewSeekDragging = false;
        SeekProjectPreview(TimeSpan.FromSeconds(ProjectPreviewSeekSlider.Value));
    }

    private void SeekProjectPreview(TimeSpan position)
    {
        var maximum = TimeSpan.FromSeconds(ProjectPreviewSeekSlider.Maximum);
        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position > maximum ? maximum : position;
        ProjectPreviewPlayer.Position = clamped;
        ProjectPreviewSeekSlider.Value = clamped.TotalSeconds;
        ProjectPreviewPositionText.Text = FormatPreviewTime(clamped);
        _viewModel.Timeline.SetPlayhead(clamped);
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

    private void AddSelectedCatalogItems_Click(object sender, RoutedEventArgs e) => AddSelectedCatalogItems();

    private void AddSelectedCatalogItems()
    {
        var selected = CatalogListBox.SelectedItems.Cast<MediaCardViewModel>().ToList();
        if (selected.Count == 0 && _viewModel.SelectedMedia is not null)
        {
            selected.Add(_viewModel.SelectedMedia);
        }

        _viewModel.AddMediaToTimeline(selected);
    }

    private async void EditSelectedTags_Click(object sender, RoutedEventArgs e)
    {
        var selected = CatalogListBox.SelectedItems.Cast<MediaCardViewModel>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var distinctTags = selected.Select(media => media.Media.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var dialog = new BulkTagsWindow(selected.Count, distinctTags.Count == 1 ? distinctTags[0] : string.Empty)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _viewModel.UpdateTagsAsync(selected, dialog.Tags);
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not save clip tags.", exception);
        }
    }

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

        var dialog = new LayerItemEditorWindow(
            kind,
            _viewModel.Timeline.Duration,
            _viewModel.Settings.CustomFontFolder,
            _viewModel.Timeline.SnapMode,
            _viewModel.Timeline.FramesPerSecond,
            _viewModel.Timeline.SelectedClipStart,
            _viewModel.Timeline.SelectedClip?.Duration)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.ResultItem is not null)
        {
            _viewModel.AddLayerItem(dialog.TrackKind, dialog.ResultItem);
        }
    }

    private void AddTrack_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TrackEditorWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultName is not null)
        {
            _viewModel.AddTrack(dialog.ResultKind, dialog.ResultName);
        }
    }

    private void LayerHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: ProjectLayerRowViewModel row })
        {
            _viewModel.SelectedProjectLayer = row;
        }
    }

    private void ProjectLayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: ProjectLayerRowViewModel row })
        {
            _viewModel.SelectedProjectLayer = row;
        }
    }

    private void TrackMoveUp_Click(object sender, RoutedEventArgs e)
    {
        SelectLayerCommandTarget(sender);
        _viewModel.MoveSelectedTrack(-1);
    }

    private void TrackMoveDown_Click(object sender, RoutedEventArgs e)
    {
        SelectLayerCommandTarget(sender);
        _viewModel.MoveSelectedTrack(1);
    }

    private void RemoveTrackContext_Click(object sender, RoutedEventArgs e)
    {
        SelectLayerCommandTarget(sender);
        RemoveTrack_Click(sender, e);
    }

    private void EditLayerContext_Click(object sender, RoutedEventArgs e)
    {
        SelectLayerCommandTarget(sender);
        EditLayer_Click(sender, e);
    }

    private void RemoveLayerContext_Click(object sender, RoutedEventArgs e)
    {
        SelectLayerCommandTarget(sender);
        _viewModel.RemoveSelectedLayerItem();
    }

    private void ColorCode_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectLayerCommandTarget(sender) ?? _viewModel.SelectedProjectLayer;
        if (row is null)
        {
            return;
        }

        var currentColor = row.Item?.Color ?? row.Track.Color;
        var dialog = new ColorCodeWindow(currentColor) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (row.Item is null)
        {
            _viewModel.SetTrackColor(row.Track.Id, dialog.ResultColor);
        }
        else
        {
            _viewModel.SetItemColor(row.Item.Id, dialog.ResultColor);
        }
    }

    private ProjectLayerRowViewModel? SelectLayerCommandTarget(object sender)
    {
        if (sender is MenuItem { CommandParameter: ProjectLayerRowViewModel row })
        {
            _viewModel.SelectedProjectLayer = row;
            return row;
        }

        return _viewModel.SelectedProjectLayer;
    }

    private void AddPluginEffect_Click(object sender, RoutedEventArgs e)
    {
        var track = _viewModel.SelectedProjectLayer?.Track;
        if (track is null || track.Kind is not (ProjectTrackKind.Background or ProjectTrackKind.Effects))
        {
            track = _viewModel.ProjectLayers
                .FirstOrDefault(row => row.IsTrackHeader && row.Track.Kind == ProjectTrackKind.Effects)
                ?.Track;
        }

        if (track is null)
        {
            return;
        }

        var dialog = new PluginEffectEditorWindow(
            _viewModel.Plugins,
            track,
            _viewModel.Timeline.Duration,
            _viewModel.Timeline.SnapMode,
            _viewModel.Timeline.FramesPerSecond)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.ResultItem is not null)
        {
            _viewModel.AddLayerItem(track.Id, dialog.ResultItem);
        }
    }

    private void RemoveTrack_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.RemoveSelectedTrack())
        {
            MessageBox.Show(
                this,
                "Select an empty timeline header. At least one timeline of every type is retained.",
                "Timeline not removed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
                return;
            }

            var layerClipDialog = new ClipEffectsWindow(row.Item) { Owner = this };
            if (layerClipDialog.ShowDialog() == true)
            {
                _viewModel.UpdateSelectedLayerClipEffects(
                    layerClipDialog.FitMode,
                    layerClipDialog.FadeInSeconds,
                    layerClipDialog.FadeOutSeconds,
                    layerClipDialog.Volume);
            }

            return;
        }

        if (row.Item.Kind == ProjectItemKind.Effect)
        {
            var pluginDialog = new PluginEffectEditorWindow(
                _viewModel.Plugins,
                row.Track,
                _viewModel.Timeline.Duration,
                _viewModel.Timeline.SnapMode,
                _viewModel.Timeline.FramesPerSecond,
                row.Item)
            {
                Owner = this
            };
            if (pluginDialog.ShowDialog() == true && pluginDialog.ResultItem is not null)
            {
                _viewModel.UpdateSelectedLayerItem(pluginDialog.ResultItem);
            }

            return;
        }

        var dialog = new LayerItemEditorWindow(
            row.Item,
            _viewModel.Timeline.Duration,
            _viewModel.Settings.CustomFontFolder,
            _viewModel.Timeline.SnapMode,
            _viewModel.Timeline.FramesPerSecond)
        {
            Owner = this
        };
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

    private void TimelineRulerMode_Click(object sender, RoutedEventArgs e) =>
        _viewModel.CycleTimelineRulerMode();

    private void TimelineSnapMode_Click(object sender, RoutedEventArgs e) =>
        _viewModel.CycleTimelineSnapMode();

    private void TimelineRuler_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border ruler)
        {
            return;
        }

        _timelinePlayheadDragging = true;
        ruler.CaptureMouse();
        SetPlayheadFromRuler(ruler, e);
        e.Handled = true;
    }

    private void TimelineRuler_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_timelinePlayheadDragging && e.LeftButton == MouseButtonState.Pressed && sender is Border ruler)
        {
            SetPlayheadFromRuler(ruler, e);
            e.Handled = true;
        }
    }

    private void TimelineRuler_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border ruler && _timelinePlayheadDragging)
        {
            SetPlayheadFromRuler(ruler, e);
            ruler.ReleaseMouseCapture();
            _timelinePlayheadDragging = false;
            e.Handled = true;
        }
    }

    private void SetPlayheadFromRuler(Border ruler, MouseEventArgs e)
    {
        var x = Math.Clamp(e.GetPosition(ruler).X, 0, ruler.ActualWidth);
        var position = TimeSpan.FromSeconds(x / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond));
        _viewModel.Timeline.SetPlayhead(position);
        if (ProjectPreviewPlayer.Source is not null)
        {
            ProjectPreviewPlayer.Pause();
            _projectPreviewTimer.Stop();
            SeekProjectPreview(_viewModel.Timeline.Playhead);
        }
    }

    private void FitTimelineHorizontally_Click(object sender, RoutedEventArgs e) =>
        _viewModel.FitTimelineHorizontally(Math.Max(100, TimelineDropSurface.ActualWidth - 76));

    private void FitTimelineVertically_Click(object sender, RoutedEventArgs e) =>
        _viewModel.FitTimelineVertically(Math.Max(100, TimelineDropSurface.ActualHeight));

    private void TimelineLaneItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: TimelineLaneItemViewModel item })
        {
            _viewModel.SelectTimelineItem(item.Id, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            _timelineDragStart = e.GetPosition(this);
            _timelineDragItem = item;
            TimelineDropSurface.Focus();
        }
    }

    private void TimelineLaneItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not Border { Tag: TimelineLaneItemViewModel item } ||
            _timelineDragItem?.Id != item.Id)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _timelineDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _timelineDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var selected = _viewModel.SelectedTimelineItemIds.Contains(item.Id)
            ? _viewModel.SelectedTimelineItemIds.ToList()
            : [item.Id];
        _timelineDragItem = null;
        var data = new DataObject(typeof(TimelineDragData), new TimelineDragData(selected));
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
    }

    private void TimelineTrackMoveUp_Click(object sender, RoutedEventArgs e)
    {
        SelectTimelineTrack(sender);
        _viewModel.MoveSelectedTrack(-1);
    }

    private void TimelineTrackMoveDown_Click(object sender, RoutedEventArgs e)
    {
        SelectTimelineTrack(sender);
        _viewModel.MoveSelectedTrack(1);
    }

    private void TimelineTrackColor_Click(object sender, RoutedEventArgs e)
    {
        SelectTimelineTrack(sender);
        ColorCode_Click(sender, e);
    }

    private void SelectTimelineTrack(object sender)
    {
        if (sender is not MenuItem { CommandParameter: TimelineLaneViewModel lane })
        {
            return;
        }

        _viewModel.SelectedProjectLayer = _viewModel.ProjectLayers
            .FirstOrDefault(row => row.IsTrackHeader && row.Track.Id == lane.TrackId);
    }

    private void TimelineItemEdit_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectTimelineItem(sender);
        if (item is null)
        {
            return;
        }

        _viewModel.SelectedProjectLayer = _viewModel.ProjectLayers.FirstOrDefault(row => row.Item?.Id == item.Id);
        EditLayer_Click(sender, e);
    }

    private void TimelineItemColor_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectTimelineItem(sender);
        if (item is null)
        {
            return;
        }

        var row = _viewModel.ProjectLayers.FirstOrDefault(candidate => candidate.Item?.Id == item.Id);
        if (row is null)
        {
            return;
        }

        _viewModel.SelectedProjectLayer = row;
        ColorCode_Click(sender, e);
    }

    private void TimelineItemRemove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectTimelineItem(sender) is not null)
        {
            _viewModel.RemoveSelectedTimelineItems();
        }
    }

    private TimelineLaneItemViewModel? SelectTimelineItem(object sender)
    {
        if (sender is not MenuItem { CommandParameter: TimelineLaneItemViewModel item })
        {
            return null;
        }

        _viewModel.SelectTimelineItem(item.Id);
        return item;
    }

    private void TimelineLane_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: TimelineLaneViewModel lane })
        {
            e.Effects = DragDropEffects.None;
        }
        else if (e.Data.GetDataPresent(typeof(MediaDragData)) || e.Data.GetDataPresent(typeof(MediaCardViewModel)))
        {
            e.Effects = lane.TrackKind == ProjectTrackKind.Video
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }
        else
        {
            e.Effects = e.Data.GetDataPresent(typeof(TimelineDragData))
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void TimelineLane_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: TimelineLaneViewModel lane } laneBorder)
        {
            return;
        }

        var start = TimeSpan.FromSeconds(Math.Max(
            0,
            e.GetPosition(laneBorder).X / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond)));
        if (e.Data.GetData(typeof(MediaDragData)) is MediaDragData mediaData &&
            lane.TrackKind == ProjectTrackKind.Video)
        {
            foreach (var media in mediaData.MediaFiles)
            {
                _viewModel.AddMediaToTrack(media, lane.TrackId, start);
                start += media.Media.Duration;
            }
        }
        else if (e.Data.GetData(typeof(MediaCardViewModel)) is MediaCardViewModel media &&
                 lane.TrackKind == ProjectTrackKind.Video)
        {
            _viewModel.AddMediaToTrack(media, lane.TrackId, start);
        }
        else if (e.Data.GetData(typeof(TimelineDragData)) is TimelineDragData timelineData)
        {
            _viewModel.MoveTimelineItems(timelineData.ItemIds, lane.TrackId, start);
        }

        e.Handled = true;
    }

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
            _viewModel.RemoveSelectedTimelineItems();
            e.Handled = true;
        }
    }

    private void TimelineListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(MediaDragData)) || e.Data.GetDataPresent(typeof(MediaCardViewModel))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void TimelineListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(MediaDragData)) is MediaDragData mediaData)
        {
            _viewModel.AddMediaToTimeline(mediaData.MediaFiles);
        }
        else if (e.Data.GetData(typeof(MediaCardViewModel)) is MediaCardViewModel media)
        {
            _viewModel.AddMediaToTimeline(media);
        }

        e.Handled = true;
    }

    private void BrowserExpand_Click(object sender, RoutedEventArgs e)
    {
        TogglePanelExpansion(WorkspacePanelKind.ContentBrowser);
    }

    private void WorkspacePanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element &&
            Enum.TryParse<WorkspacePanelKind>(element.Tag?.ToString(), out var panel))
        {
            _focusedPanel = panel;
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || IsEditingControl(Keyboard.FocusedElement as DependencyObject))
        {
            return;
        }

        var panel = ResolveFocusedPanel(Keyboard.FocusedElement as DependencyObject) ?? _focusedPanel;
        if (panel is WorkspacePanelKind.ContentBrowser or WorkspacePanelKind.Layers or WorkspacePanelKind.Timeline)
        {
            TogglePanelExpansion(panel);
            e.Handled = true;
        }
    }

    private WorkspacePanelKind? ResolveFocusedPanel(DependencyObject? element)
    {
        if (IsDescendantOf(element, ContentBrowserPanel))
        {
            return WorkspacePanelKind.ContentBrowser;
        }

        if (IsDescendantOf(element, LayersPanel))
        {
            return WorkspacePanelKind.Layers;
        }

        if (IsDescendantOf(element, TimelinePanel))
        {
            return WorkspacePanelKind.Timeline;
        }

        return null;
    }

    private static bool IsDescendantOf(DependencyObject? element, DependencyObject ancestor)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, ancestor))
            {
                return true;
            }

            element = GetParent(element);
        }

        return false;
    }

    private void TogglePanelExpansion(WorkspacePanelKind panel)
    {
        _expandedPanel = _expandedPanel == panel ? null : panel;
        _workspaceLayout.Apply(_viewModel.Settings, _expandedPanel);
        var browserExpanded = _expandedPanel == WorkspacePanelKind.ContentBrowser;
        BrowserExpandButton.Content = browserExpanded ? "←" : "→";
        BrowserExpandButton.ToolTip = browserExpanded
            ? "Restore compact content browser"
            : "Expand content browser to full workspace width";
        AutomationProperties.SetName(
            BrowserExpandButton,
            browserExpanded ? "Restore compact content browser" : "Expand content browser");
    }

    private static bool IsEditingControl(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is TextBoxBase or ComboBox or ButtonBase or Slider)
            {
                return true;
            }

            element = GetParent(element);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = GetParent(element);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        try
        {
            return VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }
        catch (InvalidOperationException)
        {
            return LogicalTreeHelper.GetParent(element);
        }
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
            _workspaceLayout.Apply(_viewModel.Settings, _expandedPanel);
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
