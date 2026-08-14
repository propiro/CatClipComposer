using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Threading;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Core.Plugins;
using CatClipComposer.Controls;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;
using CatClipComposer.Workspace;
using Microsoft.Win32;

namespace CatClipComposer;

public partial class MainWindow : Window
{
    private static readonly int[] PreviewQualityLevels = [10, 25, 50, 75, 90, 100];
    private readonly MainViewModel _viewModel;
    private readonly IMediaCatalog _catalog;
    private readonly WorkspaceLayoutController _workspaceLayout;
    private Point _catalogDragStart;
    private Point _timelineDragStart;
    private TimelineLaneItemViewModel? _timelineDragItem;
    private TimeSpan _timelineDragGrabOffset;
    private Point _trackDragStart;
    private TimelineLaneViewModel? _trackDragLane;
    private TimelineLaneItemViewModel? _timelineResizeItem;
    private bool _timelineResizeMovesStart;
    private TimeSpan _timelineResizeOriginalStart;
    private TimeSpan _timelineResizeOriginalEnd;
    private TimeSpan _timelineResizePreviewStart;
    private TimeSpan _timelineResizePreviewDuration;
    private double _timelineResizePointerStartX;
    private WorkspacePanelKind? _expandedPanel;
    private WorkspacePanelKind _focusedPanel = WorkspacePanelKind.ContentBrowser;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _projectPreviewTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _previewSeekDragging;
    private bool _clipPreviewPlaying;
    private bool _clipPreviewAutoplayPending;
    private bool _previewsSplit;
    private bool _projectPreviewSeekDragging;
    private bool _projectPreviewPlaying;
    private bool _projectPreviewAutoplayPending;
    private readonly ProjectPreviewChunkCatalog _projectPreviewChunks = new();
    private ProjectPreviewChunk? _activeProjectPreviewChunk;
    private TimeSpan? _projectPreviewPendingSeek;
    private TimeSpan _projectPreviewPlaybackStart;
    private TimeSpan _projectPreviewPlaybackEnd;
    private bool _projectOverlayRefreshQueued;
    private Guid? _overlayTransformEditItemId;
    private TimeSpan _projectPreviewTimelineOffset;
    private TimeSpan _projectPreviewTimelineEnd;
    private bool _timelinePlayheadDragging;
    private bool _timelineRangeSelecting;
    private TimeSpan _timelineRangeAnchor;
    private TimeSpan _timelineRangeClickAnchor;
    private TimeSpan _rangeHandleStartAnchor;
    private TimeSpan _rangeHandleEndAnchor;
    private double _rangeHandleDragPixels;
    private bool _rangeHandleMovesStart;
    private bool _allowClose;
    private bool _closeSaveInProgress;
    private int _catalogSelectionAnchor = -1;
    private ProjectTimelineItem? _copiedProgressStyle;

    private sealed record TimelineDragData(IReadOnlyList<Guid> ItemIds, TimeSpan GrabOffset);
    private sealed record TrackDragData(Guid TrackId);
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
        ApplyPersistedWindowGeometry(settings);
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
        _expandedPanel = FromSettingsPanel(settings.ExpandedWorkspacePanel);
        _focusedPanel = FromSettingsPanel(settings.ActiveWorkspacePanel);
        ApplyPersistedWorkspaceGeometry(settings);
        _workspaceLayout.Apply(settings, _expandedPanel);
        DataContext = _viewModel;
        var effectsView = new ListCollectionView(CreateEffectCatalogEntries().ToList());
        effectsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(EffectCatalogEntry.Category)));
        EffectsCatalogItemsControl.ItemsSource = effectsView;
        PreviewTabs.SelectedIndex = Math.Clamp(settings.ActivePreviewTab, 0, 1);
        SetPreviewLayout(settings.PreviewsSplit);
        var previewQualityIndex = Array.IndexOf(PreviewQualityLevels, settings.PreviewQualityPercent);
        PreviewQualitySlider.Value = previewQualityIndex >= 0 ? previewQualityIndex : 2;
        PreviewQualityValueText.Text = $"{settings.PreviewQualityPercent}%";
        PreserveSelectedPreviewObjectCheckBox.IsChecked = settings.PreserveSelectedPreviewObjectQuality;
        UpdateExpandedPanelButton();
        _previewTimer.Tick += PreviewTimer_Tick;
        _projectPreviewTimer.Tick += ProjectPreviewTimer_Tick;
        _viewModel.Timeline.Changed += (_, _) => ResetProjectPreviewCache();
        _viewModel.ProjectLayers.CollectionChanged += (_, _) => QueueProjectPreviewOverlayRefresh();
        _viewModel.Timeline.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TimelineViewModel.Playhead))
            {
                QueueProjectPreviewOverlayRefresh();
            }
        };
        _viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MainViewModel.SelectedProjectLayer))
            {
                var selectedItemId = _viewModel.SelectedProjectLayer?.Item?.Id;
                if (_overlayTransformEditItemId.HasValue && selectedItemId != _overlayTransformEditItemId)
                {
                    var cancelledItemId = _overlayTransformEditItemId.Value;
                    _overlayTransformEditItemId = null;
                    _viewModel.CancelOverlayTransformEdit();
                    ProjectPreviewOverlayCanvas.CompleteEdit(cancelledItemId, accepted: false);
                }

                ProjectPreviewOverlayCanvas.Select(selectedItemId);
            }
            else if (eventArgs.PropertyName == nameof(MainViewModel.OutputSettings))
            {
                QueueProjectPreviewOverlayRefresh();
            }
        };
        Loaded += (_, _) => QueueProjectPreviewOverlayRefresh();
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
            await RestoreCachedProjectPreviewAsync();
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
            ResetProjectPreviewCache();
            ProjectPreviewStatusText.Text = "No project prerender is available yet.";
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not create a new project.", exception);
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        menu.Items.Add(CreateContextMenuItem("Open from disk…", async (_, _) => await OpenProjectFromDiskAsync()));
        var recent = _viewModel.RecentProjectPaths.Where(File.Exists).ToList();
        if (recent.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var path in recent)
            {
                var recentItem = new MenuItem { Header = Path.GetFileNameWithoutExtension(path), ToolTip = path };
                recentItem.Click += async (_, _) => await OpenProjectPathAsync(path);
                menu.Items.Add(recentItem);
            }
        }

        menu.IsOpen = true;
    }

    private async Task OpenProjectFromDiskAsync()
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
            await OpenProjectPathAsync(dialog.FileName);
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not open the project.", exception);
        }
    }

    private async Task OpenProjectPathAsync(string projectPath)
    {
        try
        {
            await _viewModel.OpenProjectAsync(projectPath);
            ResetProjectPreviewCache();
            await RestoreCachedProjectPreviewAsync();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not open the project.", exception);
        }
    }

    private async Task RestoreCachedProjectPreviewAsync()
    {
        var cachedPreviews = await _viewModel.LoadProjectPreviewCacheEntriesAsync();
        if (cachedPreviews.Count == 0)
        {
            return;
        }

        _projectPreviewChunks.Replace(cachedPreviews.Select(entry => new ProjectPreviewChunk(
            entry.OutputPath,
            entry.RangeStart,
            entry.Duration,
            entry.RenderedUtc,
            entry.PreviewQualityPercent)));
        _viewModel.MarkProjectPreviewRangesRendered(
            cachedPreviews.Select(entry => (entry.RangeStart, entry.RangeEnd)));
        var cachedPreview = _projectPreviewChunks.Find(_viewModel.Timeline.Playhead) ??
                            _projectPreviewChunks.MostRecent!;
        ActivateProjectPreviewChunk(
            cachedPreview,
            cachedPreview.Start,
            autoplay: false,
            $"Restored {_projectPreviewChunks.Count} cached prerender chunk(s); active " +
            $"{cachedPreview.QualityPercent}% from {cachedPreview.RenderedUtc.ToLocalTime():g}");
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
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_closeSaveInProgress)
        {
            return;
        }

        _closeSaveInProgress = true;
        try
        {
            CancelActiveOverlayTransformEdit();
            if (_viewModel.IsDirty)
            {
                var prompt = new UnsavedChangesWindow(_viewModel.ProjectName) { Owner = this };
                prompt.ShowDialog();
                if (prompt.Choice == UnsavedProjectChoice.Cancel ||
                    (prompt.Choice == UnsavedProjectChoice.Save && !await SaveProjectWithDialogAsync()))
                {
                    return;
                }
            }

            await _viewModel.ApplySettingsAsync(CaptureWorkspaceSettings());
            await _viewModel.CompleteCleanSessionAsync();
            _allowClose = true;
            Close();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not finish closing the editor session.", exception);
        }
        finally
        {
            _closeSaveInProgress = false;
        }
    }

    private bool CancelActiveOverlayTransformEdit()
    {
        if (!_overlayTransformEditItemId.HasValue)
        {
            return false;
        }

        var itemId = _overlayTransformEditItemId.Value;
        _overlayTransformEditItemId = null;
        _viewModel.CancelOverlayTransformEdit();
        ProjectPreviewOverlayCanvas.CompleteEdit(itemId, accepted: false);
        return true;
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
            splash.Report(new StartupProgress(0, "Discovering video files…", "LIBRARY REFRESH"));
            splash.Show();
            IsEnabled = false;
            await splash.WaitForOpeningDisplayAsync();
            ScanResult result;
            try
            {
                var progress = new Progress<ScanProgress>(update =>
                {
                    var percent = update.Total == 0 ? 0 : update.Processed * 100d / update.Total;
                    var message = string.IsNullOrWhiteSpace(update.CurrentFile)
                        ? "Finalizing the library catalog…"
                        : $"Scanning clip {update.Processed + 1:N0} of {update.Total:N0} " +
                          $"({percent:0.0}%): {update.CurrentFile}";
                    splash.Report(new StartupProgress(percent, message, "LIBRARY REFRESH"));
                });
                result = await _viewModel.ScanAsync(refreshDialog.RegeneratePreviews, progress);
                splash.Report(new StartupProgress(100, "Library refresh complete.", "LIBRARY REFRESH COMPLETE"));
            }
            finally
            {
                await splash.WaitForCompletionDisplayAsync();
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

    private async void BrowserViewMode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.CycleBrowserViewModeAsync();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "Could not save the Content Browser view.", exception);
        }
    }

    private void CatalogListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _catalogDragStart = e.GetPosition(CatalogListBox);
        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is not MediaCardViewModel clickedMedia)
        {
            return;
        }

        var index = CatalogListBox.Items.IndexOf(clickedMedia);
        if (index < 0)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Shift) && _catalogSelectionAnchor >= 0)
        {
            var start = Math.Min(_catalogSelectionAnchor, index);
            var end = Math.Max(_catalogSelectionAnchor, index);
            if (!modifiers.HasFlag(ModifierKeys.Control))
            {
                CatalogListBox.SelectedItems.Clear();
            }
            for (var itemIndex = start; itemIndex <= end; itemIndex++)
            {
                var item = CatalogListBox.Items[itemIndex];
                if (!CatalogListBox.SelectedItems.Contains(item))
                {
                    CatalogListBox.SelectedItems.Add(item);
                }
            }
        }
        else if (modifiers.HasFlag(ModifierKeys.Control))
        {
            if (CatalogListBox.SelectedItems.Contains(clickedMedia))
            {
                CatalogListBox.SelectedItems.Remove(clickedMedia);
            }
            else
            {
                CatalogListBox.SelectedItems.Add(clickedMedia);
            }

            if (_catalogSelectionAnchor < 0)
            {
                _catalogSelectionAnchor = index;
            }
        }
        else
        {
            CatalogListBox.SelectedItems.Clear();
            CatalogListBox.SelectedItems.Add(clickedMedia);
            _catalogSelectionAnchor = index;
        }

        CatalogListBox.Focus();
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            AddSelectedCatalogItems();
        }
    }

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
        else
        {
            _viewModel.SelectedMedia = null;
        }

        LoadClipPreview(_viewModel.SelectedMedia?.FullPath);
    }

    private void LoadClipPreview(string? sourcePath, bool autoplay = false)
    {
        PreviewPlayer.Stop();
        _previewTimer.Stop();
        SetClipPlaybackState(false);
        _clipPreviewAutoplayPending = autoplay;
        PreviewPlayer.IsMuted = true;
        UpdateMuteButton(PreviewMuteButton, PreviewPlayer.IsMuted);
        PreviewSeekSlider.Value = 0;
        PreviewPositionText.Text = "0:00";
        PreviewDurationText.Text = "0:00";
        PreviewPlayer.Source = null;
        PreviewPlayer.Source = string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)
            ? null
            : new Uri(sourcePath, UriKind.Absolute);
    }

    private void PreviewPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_clipPreviewPlaying)
        {
            PreviewPlayer.Pause();
            _previewTimer.Stop();
            SetClipPlaybackState(false);
        }
        else if (PreviewPlayer.Source is not null)
        {
            PreviewPlayer.Play();
            _previewTimer.Start();
            SetClipPlaybackState(true);
        }
    }

    private void PreviewMute_Click(object sender, RoutedEventArgs e)
    {
        PreviewPlayer.IsMuted = !PreviewPlayer.IsMuted;
        UpdateMuteButton(PreviewMuteButton, PreviewPlayer.IsMuted);
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
        if (_clipPreviewAutoplayPending)
        {
            _clipPreviewAutoplayPending = false;
            PreviewPlayer.Play();
            _previewTimer.Start();
            SetClipPlaybackState(true);
        }
    }

    private void PreviewPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _previewTimer.Stop();
        SetClipPlaybackState(false);
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

    private void PreviewLayout_Click(object sender, RoutedEventArgs e) =>
        SetPreviewLayout(!_previewsSplit);

    private void SetPreviewLayout(bool split)
    {
        _previewsSplit = split;

        if (_previewsSplit)
        {
            ClipPreviewTabHost.Content = null;
            ProjectPreviewTabHost.Content = null;
            SplitClipPreviewHost.Content = ClipPreviewPane;
            SplitProjectPreviewHost.Content = ProjectPreviewPane;
            PreviewTabs.Visibility = Visibility.Collapsed;
            PreviewSplitGrid.Visibility = Visibility.Visible;
            PreviewLayoutButton.Content = "Join";
            PreviewLayoutButton.ToolTip = "Join the preview viewports as tabs";
            return;
        }

        SplitClipPreviewHost.Content = null;
        SplitProjectPreviewHost.Content = null;
        ClipPreviewTabHost.Content = ClipPreviewPane;
        ProjectPreviewTabHost.Content = ProjectPreviewPane;
        PreviewSplitGrid.Visibility = Visibility.Collapsed;
        PreviewTabs.Visibility = Visibility.Visible;
        PreviewLayoutButton.Content = "Split";
        PreviewLayoutButton.ToolTip = "Show clip and project previews side by side";
    }

    private void SetClipPlaybackState(bool playing)
    {
        _clipPreviewPlaying = playing;
        PreviewPlayPauseButton.Content = playing ? "⏸" : "▶";
        PreviewPlayPauseButton.ToolTip = playing ? "Pause clip preview" : "Play clip preview";
    }

    private static void UpdateMuteButton(Button button, bool isMuted)
    {
        button.Content = isMuted ? "🔇" : "🔊";
        button.ToolTip = isMuted ? "Muted — click for sound" : "Sound on — click to mute";
    }

    private void PreviewPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _previewTimer.Stop();
        SetClipPlaybackState(false);
        MessageBox.Show(
            this,
            "Windows could not preview this codec. The file can still be cataloged and processed by FFmpeg.",
            "Preview unavailable",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void PrerenderFrameLowQuality_Click(object sender, RoutedEventArgs e) =>
        await PrerenderSelectedFrameAsync(highQuality: false);

    private async void PrerenderFrameHighQuality_Click(object sender, RoutedEventArgs e) =>
        await PrerenderSelectedFrameAsync(highQuality: true);

    private async void PrerenderPreviewLowQuality_Click(object sender, RoutedEventArgs e) =>
        await PrerenderCurrentSelectionAsync(highQuality: false);

    private async void PrerenderPreviewHighQuality_Click(object sender, RoutedEventArgs e) =>
        await PrerenderCurrentSelectionAsync(highQuality: true);

    private async void PrerenderAllLowQuality_Click(object sender, RoutedEventArgs e) =>
        await RenderAndPlayProjectPreviewAsync(null, null, highQuality: false);

    private async void PrerenderAllHighQuality_Click(object sender, RoutedEventArgs e) =>
        await RenderAndPlayProjectPreviewAsync(null, null, highQuality: true);

    private async Task PrerenderCurrentSelectionAsync(bool highQuality)
    {
        if (_viewModel.Timeline.HasRangeSelection)
        {
            await RenderAndPlayProjectPreviewAsync(
                _viewModel.Timeline.RangeStart,
                _viewModel.Timeline.RangeEnd,
                highQuality: highQuality);
            return;
        }

        await PrerenderSelectedFrameAsync(highQuality);
    }

    private async Task PrerenderSelectedFrameAsync(bool highQuality = false)
    {
        var duration = _viewModel.Timeline.Duration;
        if (duration <= TimeSpan.Zero)
        {
            MessageBox.Show(
                this,
                "Add at least one video or image clip before prerendering a frame.",
                "Nothing to prerender",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var frameDuration = TimeSpan.FromSeconds(Math.Max(
            0.1,
            1 / Math.Clamp(_viewModel.OutputSettings.FramesPerSecond, 1, 240)));
        var start = _viewModel.Timeline.Playhead;
        if (start >= duration)
        {
            start = duration > frameDuration ? duration - frameDuration : TimeSpan.Zero;
        }

        var end = start + frameDuration;
        if (end > duration)
        {
            end = duration;
        }

        await RenderAndPlayProjectPreviewAsync(start, end, autoplay: false, isFrame: true, highQuality: highQuality);
    }

    private async Task RenderAndPlayProjectPreviewAsync(
        TimeSpan? rangeStart,
        TimeSpan? rangeEnd,
        bool autoplay = true,
        bool isFrame = false,
        bool highQuality = false)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        try
        {
            ProjectPreviewStatusText.Text = isFrame
                ? "Prerendering selected frame…"
                : "Prerendering layered project preview…";
            PreviewTabs.SelectedItem = ProjectPreviewTab;
            ProjectPreviewPlayer.Pause();
            _projectPreviewTimer.Stop();
            SetProjectPlaybackState(false);
            var result = await _viewModel.RenderProjectPreviewAsync(rangeStart, rangeEnd, highQuality);
            var previewOffset = rangeStart ?? TimeSpan.Zero;
            var qualityLabel = highQuality ? "HQ" : $"LQ {_viewModel.Settings.PreviewQualityPercent}%";
            var status = isFrame
                ? $"Frame {qualityLabel} prerendered at {FormatPreviewTime(previewOffset)}"
                : !rangeStart.HasValue
                    ? $"Full project {qualityLabel} prerender ready"
                    : _viewModel.Timeline.HasRangeSelection &&
                      rangeStart == _viewModel.Timeline.RangeStart && rangeEnd == _viewModel.Timeline.RangeEnd
                        ? $"Selected range {qualityLabel} prerendered: {_viewModel.Timeline.RangeText}"
                        : $"Preview {qualityLabel} prerendered from {FormatPreviewTime(rangeStart.Value)}";
            RegisterAndActivateProjectPreview(
                result.OutputPath,
                previewOffset,
                result.Duration,
                autoplay,
                status,
                highQuality ? 100 : _viewModel.Settings.PreviewQualityPercent);
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

    private void RegisterAndActivateProjectPreview(
        string outputPath,
        TimeSpan rangeStart,
        TimeSpan duration,
        bool autoplay,
        string status,
        int qualityPercent)
    {
        var chunk = new ProjectPreviewChunk(
            outputPath,
            rangeStart,
            duration,
            DateTime.UtcNow,
            qualityPercent);
        _projectPreviewChunks.Add(chunk);
        ActivateProjectPreviewChunk(chunk, rangeStart, autoplay, status);
    }

    private void ActivateProjectPreviewChunk(
        ProjectPreviewChunk chunk,
        TimeSpan position,
        bool autoplay,
        string status)
    {
        var sameSource = ProjectPreviewPlayer.Source is not null &&
                         ProjectPreviewPlayer.Source.LocalPath.Equals(
                             Path.GetFullPath(chunk.OutputPath),
                             StringComparison.OrdinalIgnoreCase);
        _activeProjectPreviewChunk = chunk;
        _projectPreviewTimelineOffset = chunk.Start;
        _projectPreviewTimelineEnd = chunk.End;
        _projectPreviewPlaybackStart = chunk.Start;
        _projectPreviewPlaybackEnd = chunk.End;
        _viewModel.MarkProjectPreviewRendered(chunk.Start, chunk.End);
        _projectPreviewPendingSeek = position;
        _projectPreviewAutoplayPending = autoplay;
        ProjectPreviewOverlayCanvas.MarkPreviewRendered();
        ProjectPreviewPlayer.IsMuted = true;
        UpdateMuteButton(ProjectPreviewMuteButton, ProjectPreviewPlayer.IsMuted);
        ProjectPreviewStatusText.Text = status;

        if (sameSource)
        {
            _projectPreviewPendingSeek = null;
            SeekProjectPreview(position);
            if (autoplay)
            {
                StartProjectPreviewPlayback();
            }
            else
            {
                ProjectPreviewPlayer.Pause();
                SetProjectPlaybackState(false);
            }

            return;
        }

        ProjectPreviewPlayer.Stop();
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
        ProjectPreviewPlayer.Source = new Uri(chunk.OutputPath, UriKind.Absolute);
        ProjectPreviewPlayer.Play();
    }

    private async void PreviewFromPlayhead_Click(object sender, RoutedEventArgs e)
    {
        var start = _viewModel.Timeline.Playhead;
        var end = _viewModel.Timeline.Duration;
        if (end <= start)
        {
            MessageBox.Show(
                this,
                "Move the current-frame needle onto rendered project content before using Play from here.",
                "Nothing to preview",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await RenderAndPlayProjectPreviewAsync(start, end);
    }

    private async void PreviewSelectedRange_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Timeline.HasRangeSelection)
        {
            return;
        }

        await RenderAndPlayProjectPreviewAsync(
            _viewModel.Timeline.RangeStart,
            _viewModel.Timeline.RangeEnd);
    }

    private void ProjectPreviewPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        var duration = _activeProjectPreviewChunk?.Duration ??
                       (ProjectPreviewPlayer.NaturalDuration.HasTimeSpan
                           ? ProjectPreviewPlayer.NaturalDuration.TimeSpan
                           : TimeSpan.Zero);
        ProjectPreviewSeekSlider.Maximum = Math.Max(0.001, duration.TotalSeconds);
        _projectPreviewTimelineEnd = _projectPreviewTimelineOffset + duration;
        ProjectPreviewDurationText.Text = FormatPreviewTime(_projectPreviewTimelineEnd);
        SeekProjectPreview(_projectPreviewPendingSeek ?? _projectPreviewTimelineOffset);
        _projectPreviewPendingSeek = null;
        var shouldAutoplay = _projectPreviewAutoplayPending;
        _projectPreviewAutoplayPending = false;
        if (shouldAutoplay)
        {
            StartProjectPreviewPlayback();
        }
        else
        {
            ProjectPreviewPlayer.Pause();
            _projectPreviewTimer.Stop();
            SetProjectPlaybackState(false);
        }

        QueueProjectPreviewOverlayRefresh();
    }

    private void ProjectPreviewOverlayCanvas_OverlaySelected(
        object? sender,
        PreviewOverlaySelectedEventArgs e)
    {
        PauseProjectPreviewForOverlayEditing();
        if (_overlayTransformEditItemId.HasValue && _overlayTransformEditItemId != e.ItemId)
        {
            var cancelledItemId = _overlayTransformEditItemId.Value;
            _overlayTransformEditItemId = null;
            _viewModel.CancelOverlayTransformEdit();
            ProjectPreviewOverlayCanvas.CompleteEdit(cancelledItemId, accepted: false);
        }

        if (!_viewModel.BeginOverlayTransformEdit(e.ItemId))
        {
            _viewModel.SelectTimelineItem(e.ItemId);
            ProjectPreviewOverlayCanvas.Select(e.ItemId);
            ProjectPreviewStatusText.Text = "Overlay selected. Its transform is locked.";
            return;
        }

        _overlayTransformEditItemId = e.ItemId;
        _viewModel.SelectTimelineItem(e.ItemId);
        ProjectPreviewOverlayCanvas.Select(e.ItemId);
    }

    private void ProjectPreviewOverlayCanvas_OverlayTransformChanged(
        object? sender,
        PreviewOverlayTransformEventArgs e)
    {
        if (!_viewModel.PreviewOverlayTransform(
                e.ItemId,
                e.X,
                e.Y,
                e.Scale,
                e.RotationDegrees))
        {
            return;
        }

        ProjectPreviewStatusText.Text =
            $"Overlay: X {e.X * 100:0.#}% · Y {e.Y * 100:0.#}% · " +
            $"scale {e.Scale * 100:0.#}% · rotate {e.RotationDegrees:0.#}° — press OK/Enter to apply.";
    }

    private void ProjectPreviewOverlayCanvas_OverlayOpenEditorRequested(
        object? sender,
        PreviewOverlayOpenEditorEventArgs e)
    {
        CancelActiveOverlayTransformEdit();
        _viewModel.SelectTimelineItem(e.ItemId);
        EditLayer_Click(ProjectPreviewOverlayCanvas, new RoutedEventArgs());
    }

    private void ProjectPreviewOverlayCanvas_OverlayEditAccepted(
        object? sender,
        PreviewOverlayEditEventArgs e)
    {
        if (_viewModel.CommitOverlayTransformEdit(e.ItemId))
        {
            _overlayTransformEditItemId = null;
            ProjectPreviewOverlayCanvas.CompleteEdit(e.ItemId, accepted: true);
            ProjectPreviewStatusText.Text = "Overlay transform applied — prerender to refresh the composition.";
            QueueProjectPreviewOverlayRefresh();
        }
    }

    private void ProjectPreviewOverlayCanvas_OverlayEditCanceled(
        object? sender,
        PreviewOverlayEditEventArgs e)
    {
        if (_overlayTransformEditItemId != e.ItemId)
        {
            return;
        }

        _overlayTransformEditItemId = null;
        _viewModel.CancelOverlayTransformEdit();
        ProjectPreviewOverlayCanvas.CompleteEdit(e.ItemId, accepted: false);
        ProjectPreviewStatusText.Text = "Overlay transform cancelled.";
        QueueProjectPreviewOverlayRefresh();
    }

    private void PauseProjectPreviewForOverlayEditing()
    {
        ProjectPreviewPlayer.Pause();
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
    }

    private void QueueProjectPreviewOverlayRefresh()
    {
        if (_projectOverlayRefreshQueued)
        {
            return;
        }

        _projectOverlayRefreshQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _projectOverlayRefreshQueued = false;
            ProjectPreviewOverlayCanvas.Configure(
                _viewModel.OutputSettings.Width,
                _viewModel.OutputSettings.Height,
                _viewModel.GetActivePositionableOverlayItems(_viewModel.Timeline.Playhead),
                _viewModel.SelectedProjectLayer?.Item?.Id,
                ProjectPreviewPlayer.Source is not null);
        }));
    }

    private void ProjectPreviewPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        CompleteProjectPreviewPlayback();
    }

    private void ProjectPreviewPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
        ProjectPreviewStatusText.Text = "Windows could not play the rendered preview codec.";
        MessageBox.Show(
            this,
            "The preview rendered, but Windows could not play its codec.",
            "Project preview unavailable",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ProjectPreviewPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_projectPreviewPlaying)
        {
            ProjectPreviewPlayer.Pause();
            _projectPreviewTimer.Stop();
            SetProjectPlaybackState(false);
        }
        else
        {
            var chunk = _activeProjectPreviewChunk?.Contains(_viewModel.Timeline.Playhead) == true
                ? _activeProjectPreviewChunk
                : _projectPreviewChunks.Find(_viewModel.Timeline.Playhead);
            if (chunk is null)
            {
                ProjectPreviewStatusText.Text =
                    $"No prerendered chunk contains {FormatPreviewTime(_viewModel.Timeline.Playhead)}.";
                return;
            }

            if (_activeProjectPreviewChunk != chunk || ProjectPreviewPlayer.Source is null)
            {
                ActivateProjectPreviewChunk(
                    chunk,
                    _viewModel.Timeline.Playhead,
                    autoplay: true,
                    $"Playing cached {chunk.QualityPercent}% prerender");
                return;
            }

            StartProjectPreviewPlayback();
        }
    }

    private void StartProjectPreviewPlayback()
    {
        if (_activeProjectPreviewChunk is null || ProjectPreviewPlayer.Source is null)
        {
            return;
        }

        _projectPreviewPlaybackStart = _activeProjectPreviewChunk.Start;
        _projectPreviewPlaybackEnd = _activeProjectPreviewChunk.End;
        var frame = TimeSpan.FromSeconds(1 / Math.Max(1, _viewModel.Timeline.FramesPerSecond));
        if (_viewModel.Timeline.Playhead < _projectPreviewPlaybackStart ||
            _viewModel.Timeline.Playhead >= _projectPreviewPlaybackEnd - frame)
        {
            SeekProjectPreview(_projectPreviewPlaybackStart);
        }

        ProjectPreviewPlayer.Play();
        _projectPreviewTimer.Start();
        SetProjectPlaybackState(true);
    }

    private void CompleteProjectPreviewPlayback()
    {
        ProjectPreviewPlayer.Pause();
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
        if (_activeProjectPreviewChunk is null)
        {
            return;
        }

        SeekProjectPreview(_projectPreviewPlaybackStart);
        ProjectPreviewStatusText.Text = "Preview complete";
    }

    private void ProjectPreviewMute_Click(object sender, RoutedEventArgs e)
    {
        ProjectPreviewPlayer.IsMuted = !ProjectPreviewPlayer.IsMuted;
        UpdateMuteButton(ProjectPreviewMuteButton, ProjectPreviewPlayer.IsMuted);
    }

    private void ProjectPreviewPreviousFrame_Click(object sender, RoutedEventArgs e) => StepProjectPreviewFrame(-1);

    private void ProjectPreviewNextFrame_Click(object sender, RoutedEventArgs e) => StepProjectPreviewFrame(1);

    private void StepProjectPreviewFrame(int direction)
    {
        ProjectPreviewPlayer.Pause();
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
        _viewModel.Timeline.StepFrame(direction);
        PauseAndSeekProjectPreview(_viewModel.Timeline.Playhead);
    }

    private void ProjectPreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (_projectPreviewSeekDragging)
        {
            return;
        }

        var position = ProjectPreviewPlayer.Position;
        var timelinePosition = _projectPreviewTimelineOffset + position;
        if (_projectPreviewPlaying && timelinePosition >= _projectPreviewPlaybackEnd)
        {
            CompleteProjectPreviewPlayback();
            return;
        }

        ProjectPreviewSeekSlider.Value = Math.Clamp(
            position.TotalSeconds,
            ProjectPreviewSeekSlider.Minimum,
            ProjectPreviewSeekSlider.Maximum);
        ProjectPreviewPositionText.Text = FormatPreviewTime(timelinePosition);
        _viewModel.Timeline.SetPlayhead(timelinePosition);
    }

    private void ProjectPreviewSeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _projectPreviewSeekDragging = true;

    private void ProjectPreviewSeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _projectPreviewSeekDragging = false;
        SeekProjectPreview(
            _projectPreviewTimelineOffset + TimeSpan.FromSeconds(ProjectPreviewSeekSlider.Value));
    }

    private void SeekProjectPreview(TimeSpan position)
    {
        var maximum = TimeSpan.FromSeconds(ProjectPreviewSeekSlider.Maximum);
        var local = position - _projectPreviewTimelineOffset;
        var clampedLocal = local < TimeSpan.Zero ? TimeSpan.Zero : local > maximum ? maximum : local;
        var timelinePosition = _projectPreviewTimelineOffset + clampedLocal;
        ProjectPreviewPlayer.Position = clampedLocal;
        ProjectPreviewSeekSlider.Value = clampedLocal.TotalSeconds;
        ProjectPreviewPositionText.Text = FormatPreviewTime(timelinePosition);
        _viewModel.Timeline.SetPlayhead(timelinePosition);
    }

    private void PauseAndSeekProjectPreview(TimeSpan position)
    {
        _viewModel.Timeline.SetPlayhead(position);
        var chunk = _activeProjectPreviewChunk?.Contains(position) == true
            ? _activeProjectPreviewChunk
            : _projectPreviewChunks.Find(position);
        if (chunk is null)
        {
            ProjectPreviewPlayer.Stop();
            ProjectPreviewPlayer.Source = null;
            _activeProjectPreviewChunk = null;
            _projectPreviewTimer.Stop();
            SetProjectPlaybackState(false);
            ProjectPreviewStatusText.Text =
                $"Frame {FormatPreviewTime(position)} has not been prerendered yet.";
            QueueProjectPreviewOverlayRefresh();
            return;
        }

        if (_activeProjectPreviewChunk != chunk || ProjectPreviewPlayer.Source is null)
        {
            ActivateProjectPreviewChunk(
                chunk,
                position,
                autoplay: false,
                $"Cached {chunk.QualityPercent}% prerender: " +
                $"{FormatPreviewTime(chunk.Start)} - {FormatPreviewTime(chunk.End)}");
            return;
        }

        ProjectPreviewPlayer.Pause();
        _projectPreviewAutoplayPending = false;
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
        SeekProjectPreview(position);
    }

    private void SetProjectPlaybackState(bool playing)
    {
        _projectPreviewPlaying = playing;
        ProjectPreviewPlayPauseButton.Content = playing ? "⏸" : "▶";
        ProjectPreviewPlayPauseButton.ToolTip = playing ? "Pause project preview" : "Play project preview";
    }

    private async void PreviewQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var index = Math.Clamp((int)Math.Round(e.NewValue), 0, PreviewQualityLevels.Length - 1);
        var quality = PreviewQualityLevels[index];
        if (PreviewQualityValueText is not null)
        {
            PreviewQualityValueText.Text = $"{quality}%";
        }

        if (!IsLoaded ||
            _viewModel.Settings.PreviewQualityPercent == quality)
        {
            return;
        }

        var settings = _viewModel.Settings.Copy();
        settings.PreviewQualityPercent = quality;
        await _viewModel.ApplySettingsAsync(settings);
        ProjectPreviewStatusText.Text = $"Preview resolution set to {quality}% — prerender to apply.";
    }

    private async void PreserveSelectedPreviewObjectCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var enabled = PreserveSelectedPreviewObjectCheckBox.IsChecked == true;
        if (_viewModel.Settings.PreserveSelectedPreviewObjectQuality == enabled)
        {
            return;
        }

        var settings = _viewModel.Settings.Copy();
        settings.PreserveSelectedPreviewObjectQuality = enabled;
        await _viewModel.ApplySettingsAsync(settings);
        ProjectPreviewStatusText.Text = enabled
            ? "Selected-object preview quality enabled — prerender to apply."
            : "Uniform preview quality enabled — prerender to apply.";
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

    private IReadOnlyList<EffectCatalogEntry> CreateEffectCatalogEntries()
    {
        var entries = new List<EffectCatalogEntry>
        {
            new("AUDIO TIMELINES", "Music / audio", "Add a timed audio file with volume and fades.", ProjectTrackKind.Audio, LayerEditorKind.Audio),
            new("OVERLAY TIMELINES", "Image / PNG overlay", "Add a positionable, scalable, rotatable image overlay.", ProjectTrackKind.Overlay, LayerEditorKind.Image),
            new("OVERLAY TIMELINES", "Text overlay", "Add positionable styled text over the composition.", ProjectTrackKind.Overlay, LayerEditorKind.Text),
            new("PROGRESS TIMELINES", "Progress", "Add a styled progress bar for the selected clip range.", ProjectTrackKind.Progress, LayerEditorKind.Progress)
        };
        foreach (var plugin in _viewModel.Plugins.OfType<ICatClipVideoEffectPlugin>())
        {
            foreach (var trackKind in plugin.Descriptor.CompatibleTracks)
            {
                entries.Add(new EffectCatalogEntry(
                    $"{trackKind.ToString().ToUpperInvariant()} TIMELINES",
                    plugin.Descriptor.Name,
                    plugin.Descriptor.Description,
                    trackKind,
                    PluginId: plugin.Descriptor.Id));
            }
        }

        return entries.OrderBy(entry => entry.Category).ThenBy(entry => entry.Name).ToList();
    }

    private void EffectCatalogAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { CommandParameter: EffectCatalogEntry entry })
        {
            return;
        }

        AddEffectCatalogEntry(entry);
    }

    private void EffectCatalogAddButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EffectCatalogEntry entry })
        {
            AddEffectCatalogEntry(entry);
        }
    }

    private void EffectCatalogEntry_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Border { DataContext: EffectCatalogEntry entry })
        {
            AddEffectCatalogEntry(entry);
            e.Handled = true;
        }
    }

    private void AddEffectCatalogEntry(EffectCatalogEntry entry)
    {

        var row = _viewModel.ProjectLayers.FirstOrDefault(candidate =>
            candidate.IsTrackHeader && candidate.Track.Kind == entry.TrackKind);
        if (row is null)
        {
            return;
        }

        _viewModel.SelectedProjectLayer = row;
        if (!string.IsNullOrWhiteSpace(entry.PluginId))
        {
            OpenCompatibleEffectEditor(row, useSelectedItemTiming: true, initialPluginId: entry.PluginId);
        }
        else if (entry.LayerKind.HasValue)
        {
            OpenLayerItemEditor(entry.LayerKind.Value, row.Track);
        }
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

        OpenLayerItemEditor(kind);
    }

    private async void OpenLayerItemEditor(
        LayerEditorKind kind,
        ProjectTrack? targetTrack = null,
        TimeSpan? initialStart = null,
        TimeSpan? initialDuration = null)
    {
        var selectedRange = _viewModel.GetSelectedVideoRange();
        ProjectTimelineItem? progressTemplate = null;
        if (kind == LayerEditorKind.Progress)
        {
            var start = initialStart ?? selectedRange?.Start ?? _viewModel.Timeline.Playhead;
            var duration = initialDuration ?? selectedRange?.Duration ?? TimeSpan.FromSeconds(Math.Min(
                5,
                Math.Max(_viewModel.Timeline.SnapIncrement, (_viewModel.Timeline.Duration - start).TotalSeconds)));
            progressTemplate = _viewModel.CreateProgressItem(
                start,
                duration,
                _viewModel.GetSelectedVideoProgressName());
        }

        var editorTrackKind = kind switch
        {
            LayerEditorKind.Audio => ProjectTrackKind.Audio,
            LayerEditorKind.Progress => ProjectTrackKind.Progress,
            _ => ProjectTrackKind.Overlay
        };
        var editorTrack = targetTrack ?? _viewModel.ProjectLayers
            .FirstOrDefault(row => row.IsTrackHeader && row.Track.Kind == editorTrackKind)?.Track;
        var previewFrame = _viewModel.Timeline.Duration > TimeSpan.Zero && editorTrack is not null
            ? _viewModel.Timeline.Playhead
            : (TimeSpan?)null;

        var dialog = new LayerItemEditorWindow(
            kind,
            _viewModel.Timeline.Duration,
            _viewModel.Settings.CustomFontFolder,
            _viewModel.Timeline.SnapMode,
            _viewModel.Timeline.FramesPerSecond,
            initialStart ?? selectedRange?.Start,
            initialDuration ?? selectedRange?.Duration,
            previewFrame,
            previewFrame.HasValue && editorTrack is not null
                ? (item, progress, cancellationToken) => _viewModel.RenderEffectFramePreviewAsync(
                    editorTrack.Id,
                    item,
                    previewFrame.Value,
                    progress,
                    cancellationToken)
                : null)
        {
            Owner = this
        };
        if (progressTemplate is not null)
        {
            dialog.ApplyProgressTemplate(progressTemplate);
        }
        if (dialog.ShowDialog() == true && dialog.ResultItem is not null)
        {
            if (targetTrack is null)
            {
                _viewModel.AddLayerItem(dialog.TrackKind, dialog.ResultItem);
            }
            else
            {
                _viewModel.AddLayerItem(targetTrack.Id, dialog.ResultItem);
            }

            if (dialog.ResultItem.Kind == ProjectItemKind.ProgressBar)
            {
                await _viewModel.RememberProgressDefaultsAsync(dialog.ResultItem);
            }
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
            if (row.Item is not null)
            {
                _viewModel.SelectTimelineItem(row.Item.Id);
            }
        }
    }

    private void ProjectLayerList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null &&
            sender is ListBox { SelectedItem: ProjectLayerRowViewModel { Item: not null } })
        {
            EditLayer_Click(sender, e);
            e.Handled = true;
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

    private void AddClipEffectContext_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectLayerCommandTarget(sender);
        OpenCompatibleEffectEditor(row, useSelectedItemTiming: true);
    }

    private void AddCompatibleEffectContext_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectLayerCommandTarget(sender);
        OpenCompatibleEffectEditor(row, useSelectedItemTiming: false);
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
        OpenCompatibleEffectEditor(_viewModel.SelectedProjectLayer, useSelectedItemTiming: true, allowDefaultEffectsTrack: true);
    }

    private void OpenCompatibleEffectEditor(
        ProjectLayerRowViewModel? sourceRow,
        bool useSelectedItemTiming,
        bool allowDefaultEffectsTrack = false,
        TimeSpan? initialStart = null,
        TimeSpan? initialDuration = null,
        string? initialPluginId = null)
    {
        var targetTrack = ResolveEffectTargetTrack(sourceRow, allowDefaultEffectsTrack);
        if (targetTrack is null)
        {
            MessageBox.Show(
                this,
                "No loaded effect module is compatible with this timeline or item.",
                "No compatible effects",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var compatiblePlugins = _viewModel.Plugins
            .OfType<ICatClipVideoEffectPlugin>()
            .Where(plugin => plugin.Descriptor.CompatibleTracks.Contains(targetTrack.Kind))
            .ToList();
        if (compatiblePlugins.Count == 0)
        {
            return;
        }

        var sourceItem = useSelectedItemTiming ? sourceRow?.Item : null;
        var selectedVideoRange = _viewModel.GetSelectedVideoRange();
        var previewFrame = _viewModel.Timeline.Duration > TimeSpan.Zero
            ? _viewModel.Timeline.Playhead
            : (TimeSpan?)null;
        var dialog = new PluginEffectEditorWindow(
            _viewModel.Plugins,
            targetTrack,
            _viewModel.Timeline.Duration,
            _viewModel.Timeline.SnapMode,
            _viewModel.Timeline.FramesPerSecond,
            initialStart: initialStart ?? selectedVideoRange?.Start ?? sourceItem?.Start,
            initialDuration: initialDuration ?? selectedVideoRange?.Duration ?? sourceItem?.Duration,
            initialPluginId: initialPluginId,
            previewFrame: previewFrame,
            framePreviewRenderer: previewFrame.HasValue
                ? (item, progress, cancellationToken) => _viewModel.RenderEffectFramePreviewAsync(
                    targetTrack.Id,
                    item,
                    previewFrame.Value,
                    progress,
                    cancellationToken)
                : null)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.ResultItem is not null)
        {
            _viewModel.AddLayerItem(targetTrack.Id, dialog.ResultItem);
        }
    }

    private ProjectTrack? ResolveEffectTargetTrack(
        ProjectLayerRowViewModel? sourceRow,
        bool allowDefaultEffectsTrack)
    {
        if (sourceRow is not null && _viewModel.Plugins.OfType<ICatClipVideoEffectPlugin>()
                .Any(plugin => plugin.Descriptor.CompatibleTracks.Contains(sourceRow.Track.Kind)))
        {
            return sourceRow.Track;
        }

        if (!allowDefaultEffectsTrack)
        {
            return null;
        }

        return _viewModel.ProjectLayers.FirstOrDefault(row =>
            row.IsTrackHeader && _viewModel.Plugins.OfType<ICatClipVideoEffectPlugin>()
                .Any(plugin => plugin.Descriptor.CompatibleTracks.Contains(row.Track.Kind)))?.Track;
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
        var row = _viewModel.SelectedProjectLayer;
        if (row?.Item is { Kind: ProjectItemKind.Video or ProjectItemKind.StillImage } item)
        {
            if (_viewModel.Timeline.Select(item.Id) && _viewModel.Timeline.SelectedClip is not null)
            {
                EditTimelineClipTransform(_viewModel.Timeline.SelectedClip);
            }
            else
            {
                var layerDialog = new ClipEffectsWindow(item) { Owner = this };
                if (layerDialog.ShowDialog() == true)
                {
                    _viewModel.UpdateSelectedLayerClipEffects(
                        layerDialog.FitMode,
                        layerDialog.FadeInSeconds,
                        layerDialog.FadeOutSeconds,
                        layerDialog.Volume);
                }
            }

            return;
        }

        if (_viewModel.Timeline.SelectedClip is null)
        {
            MessageBox.Show(
                this,
                "Select a video or still screen in Used Clips or on the main timeline first.",
                "No selected clip",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        EditTimelineClipTransform(_viewModel.Timeline.SelectedClip);
    }

    private void EditTimelineClipTransform(TimelineClipViewModel clip)
    {
        var dialog = new ClipEffectsWindow(clip) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.UpdateSelectedClipEffects(
                dialog.FitMode,
                dialog.FadeInSeconds,
                dialog.FadeOutSeconds,
                dialog.Volume);
        }
    }

    private async void EditLayer_Click(object sender, RoutedEventArgs e)
    {
        var row = _viewModel.SelectedProjectLayer;
        if (row?.Item is null)
        {
            return;
        }

        if (row.Item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
        {
            ClipEffects_Click(sender, e);
            return;
        }

        if (row.Item.Kind == ProjectItemKind.Effect)
        {
            var previewFrame = _viewModel.Timeline.Duration > TimeSpan.Zero
                ? _viewModel.Timeline.Playhead
                : (TimeSpan?)null;
            var pluginDialog = new PluginEffectEditorWindow(
                _viewModel.Plugins,
                row.Track,
                _viewModel.Timeline.Duration,
                _viewModel.Timeline.SnapMode,
                _viewModel.Timeline.FramesPerSecond,
                row.Item,
                previewFrame: previewFrame,
                framePreviewRenderer: previewFrame.HasValue
                    ? (item, progress, cancellationToken) => _viewModel.RenderEffectFramePreviewAsync(
                        row.Track.Id,
                        item,
                        previewFrame.Value,
                        progress,
                        cancellationToken)
                    : null)
            {
                Owner = this
            };
            if (pluginDialog.ShowDialog() == true && pluginDialog.ResultItem is not null)
            {
                _viewModel.UpdateSelectedLayerItem(pluginDialog.ResultItem);
                ProjectPreviewOverlayCanvas.MarkItemStale(pluginDialog.ResultItem.Id);
                QueueProjectPreviewOverlayRefresh();
            }

            return;
        }

        var dialog = new LayerItemEditorWindow(
            row.Item,
            _viewModel.Timeline.Duration,
            _viewModel.Settings.CustomFontFolder,
            _viewModel.Timeline.SnapMode,
            _viewModel.Timeline.FramesPerSecond,
            _viewModel.Timeline.Duration > TimeSpan.Zero ? _viewModel.Timeline.Playhead : null,
            _viewModel.Timeline.Duration > TimeSpan.Zero
                ? (item, progress, cancellationToken) => _viewModel.RenderEffectFramePreviewAsync(
                    row.Track.Id,
                    item,
                    _viewModel.Timeline.Playhead,
                    progress,
                    cancellationToken)
                : null)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.ResultItem is not null)
        {
            _viewModel.UpdateSelectedLayerItem(dialog.ResultItem);
            if (dialog.ResultItem.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay)
            {
                ProjectPreviewOverlayCanvas.MarkItemStale(dialog.ResultItem.Id);
                ProjectPreviewStatusText.Text = "Overlay settings changed — prerender the frame to refresh the composition.";
                QueueProjectPreviewOverlayRefresh();
            }
            if (dialog.ResultItem.Kind == ProjectItemKind.ProgressBar)
            {
                await _viewModel.RememberProgressDefaultsAsync(dialog.ResultItem);
            }
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

    private void MarkRangeStart_Click(object sender, RoutedEventArgs e)
    {
        var playhead = _viewModel.Timeline.Playhead;
        var increment = Math.Max(
            1 / Math.Max(1, _viewModel.Timeline.FramesPerSecond),
            _viewModel.Timeline.SnapIncrement);
        var maximum = Math.Max(
            _viewModel.Timeline.TargetDuration.TotalSeconds,
            _viewModel.Timeline.Duration.TotalSeconds);
        var end = _viewModel.Timeline.HasRangeSelection && _viewModel.Timeline.RangeEnd > playhead
            ? _viewModel.Timeline.RangeEnd
            : TimeSpan.FromSeconds(Math.Min(maximum, playhead.TotalSeconds + increment));
        var start = end > playhead
            ? playhead
            : TimeSpan.FromSeconds(Math.Max(0, end.TotalSeconds - increment));
        _viewModel.Timeline.SetRangeSelection(start, end);
        _viewModel.Timeline.SetPlayhead(start);
        UpdateProjectPreviewForRangeSelection();
    }

    private void MarkRangeEnd_Click(object sender, RoutedEventArgs e)
    {
        var playhead = _viewModel.Timeline.Playhead;
        var increment = Math.Max(
            1 / Math.Max(1, _viewModel.Timeline.FramesPerSecond),
            _viewModel.Timeline.SnapIncrement);
        var start = _viewModel.Timeline.HasRangeSelection && _viewModel.Timeline.RangeStart < playhead
            ? _viewModel.Timeline.RangeStart
            : TimeSpan.FromSeconds(Math.Max(0, playhead.TotalSeconds - increment));
        var end = playhead > start
            ? playhead
            : start + TimeSpan.FromSeconds(increment);
        _viewModel.Timeline.SetRangeSelection(start, end);
        _viewModel.Timeline.SetPlayhead(_viewModel.Timeline.RangeEnd);
        UpdateProjectPreviewForRangeSelection();
    }

    private void TimelineRuler_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border ruler)
        {
            return;
        }

        _timelinePlayheadDragging = true;
        _timelineRangeSelecting = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                                  Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        _timelineRangeClickAnchor = _viewModel.Timeline.Playhead;
        _timelineRangeAnchor = GetRulerPosition(ruler, e);
        var hadRangeSelection = _viewModel.Timeline.HasRangeSelection;
        _viewModel.Timeline.ClearRangeSelection();
        if (hadRangeSelection || _timelineRangeSelecting)
        {
            UpdateProjectPreviewForRangeSelection();
        }
        ruler.CaptureMouse();
        SetPlayheadFromRuler(ruler, e, false);
        e.Handled = true;
    }

    private void TimelineRuler_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_timelinePlayheadDragging && e.LeftButton == MouseButtonState.Pressed && sender is Border ruler)
        {
            SetPlayheadFromRuler(ruler, e, _timelineRangeSelecting);
            e.Handled = true;
        }
    }

    private void TimelineRuler_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border ruler && _timelinePlayheadDragging)
        {
            var position = GetRulerPosition(ruler, e);
            if (_timelineRangeSelecting)
            {
                var frameDuration = 1 / Math.Max(1, _viewModel.Timeline.FramesPerSecond);
                var moved = Math.Abs((position - _timelineRangeAnchor).TotalSeconds) >= frameDuration;
                _viewModel.Timeline.SetRangeSelection(
                    moved ? _timelineRangeAnchor : _timelineRangeClickAnchor,
                    position);
                UpdateProjectPreviewForRangeSelection();
            }

            SetPlayheadFromRuler(ruler, e, false);
            ruler.ReleaseMouseCapture();
            _timelinePlayheadDragging = false;
            _timelineRangeSelecting = false;
            e.Handled = true;
        }
    }

    private void TimelineCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Grid canvas)
        {
            return;
        }

        var position = e.GetPosition(canvas);
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint, PlacementTarget = canvas };
        if (Math.Abs(position.X - _viewModel.Timeline.PlayheadLeft) <= 7)
        {
            menu.Items.Add(CreateContextMenuItem("Play from here", PreviewFromPlayhead_Click));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateContextMenuItem("Mark selection start", MarkRangeStart_Click));
            menu.Items.Add(CreateContextMenuItem("Mark selection end", MarkRangeEnd_Click));
        }
        else if (_viewModel.Timeline.HasRangeSelection &&
                 position.X >= _viewModel.Timeline.RangeLeft &&
                 position.X <= _viewModel.Timeline.RangeEndLeft)
        {
            menu.Items.Add(CreateContextMenuItem("Preview range", PreviewSelectedRange_Click));
        }
        else if (!HasTimelineItemAncestor(e.OriginalSource as DependencyObject))
        {
            AddTimelineLaneEffectItems(menu, position);
        }

        if (menu.Items.Count == 0)
        {
            return;
        }

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void AddTimelineLaneEffectItems(ContextMenu menu, Point position)
    {
        const double rulerHeight = 34;
        if (position.Y < rulerHeight)
        {
            return;
        }

        var laneIndex = (int)((position.Y - rulerHeight) / Math.Max(1, _viewModel.Timeline.TrackHeight));
        if (laneIndex < 0 || laneIndex >= _viewModel.TimelineLanes.Count)
        {
            return;
        }

        var lane = _viewModel.TimelineLanes[laneIndex];
        AddTimelineLaneEffectItems(menu, lane, position.X);
    }

    private void TimelineLane_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled ||
            e.ClickCount != 1 ||
            sender is not Border { DataContext: TimelineLaneViewModel lane } laneBorder ||
            HasTimelineItemAncestor(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var pointer = e.GetPosition(laneBorder);
        SetPlayheadFromTimelineLane(laneBorder, e);
        var menu = new ContextMenu
        {
            Placement = PlacementMode.RelativePoint,
            PlacementTarget = laneBorder,
            HorizontalOffset = pointer.X,
            VerticalOffset = pointer.Y
        };
        AddTimelineLaneEffectItems(menu, lane, pointer.X);
        if (menu.Items.Count == 0)
        {
            e.Handled = true;
            return;
        }

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void AddTimelineLaneEffectItems(ContextMenu menu, TimelineLaneViewModel lane, double pointerX)
    {
        var row = _viewModel.ProjectLayers.FirstOrDefault(candidate =>
            candidate.IsTrackHeader && candidate.Track.Id == lane.TrackId);
        if (row is null)
        {
            return;
        }

        var targetTrack = row.Track;

        var rawStart = TimeSpan.FromSeconds(Math.Max(
            0,
            pointerX / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond)));
        var maximumStart = _viewModel.Timeline.Duration -
                           TimeSpan.FromSeconds(_viewModel.Timeline.SnapIncrement);
        var start = _viewModel.SnapTime(
            maximumStart > TimeSpan.Zero && rawStart > maximumStart ? maximumStart : rawStart,
            targetTrack.Id,
            snapToClipRanges: SnapToClipRangesCheckBox.IsChecked == true);
        if (targetTrack.Kind == ProjectTrackKind.Overlay)
        {
            AddNativeLayerMenuItem(menu, row, targetTrack, start, LayerEditorKind.Image, "Add image / PNG overlay…");
            AddNativeLayerMenuItem(menu, row, targetTrack, start, LayerEditorKind.Text, "Add text overlay…");
        }
        else if (targetTrack.Kind == ProjectTrackKind.Audio)
        {
            AddNativeLayerMenuItem(menu, row, targetTrack, start, LayerEditorKind.Audio, "Add music / audio…");
        }
        else if (targetTrack.Kind == ProjectTrackKind.Progress)
        {
            AddNativeLayerMenuItem(menu, row, targetTrack, start, LayerEditorKind.Progress, "Add progress…");
        }

        var compatible = _viewModel.Plugins.OfType<ICatClipVideoEffectPlugin>()
            .Where(plugin => plugin.Descriptor.CompatibleTracks.Contains(targetTrack.Kind))
            .OrderBy(plugin => plugin.Descriptor.Name)
            .ToList();
        foreach (var plugin in compatible)
        {
            var item = new MenuItem { Header = $"Add {plugin.Descriptor.Name}…" };
            item.Click += (_, _) =>
            {
                _viewModel.SelectedProjectLayer = row;
                var remaining = _viewModel.Timeline.Duration - start;
                var duration = targetTrack.Kind == ProjectTrackKind.Background
                    ? remaining
                    : remaining < TimeSpan.FromSeconds(5) ? remaining : TimeSpan.FromSeconds(5);
                if (duration <= TimeSpan.Zero)
                {
                    duration = TimeSpan.FromSeconds(_viewModel.Timeline.SnapIncrement);
                }

                OpenCompatibleEffectEditor(
                    row,
                    useSelectedItemTiming: false,
                    initialStart: start,
                    initialDuration: duration,
                    initialPluginId: plugin.Descriptor.Id);
            };
            menu.Items.Add(item);
        }
    }

    private void AddNativeLayerMenuItem(
        ContextMenu menu,
        ProjectLayerRowViewModel row,
        ProjectTrack targetTrack,
        TimeSpan start,
        LayerEditorKind kind,
        string header)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            _viewModel.SelectedProjectLayer = row;
            var selectedRange = _viewModel.GetSelectedVideoRange();
            var duration = selectedRange?.Duration ?? TimeSpan.FromSeconds(Math.Min(
                5,
                Math.Max(_viewModel.Timeline.SnapIncrement, (_viewModel.Timeline.Duration - start).TotalSeconds)));
            OpenLayerItemEditor(kind, targetTrack, start, duration);
        };
        menu.Items.Add(item);
    }

    private static MenuItem CreateContextMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
    }

    private static bool HasTimelineItemAncestor(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Border { Tag: TimelineLaneItemViewModel })
            {
                return true;
            }
        }

        return false;
    }

    private TimeSpan GetRulerPosition(Border ruler, MouseEventArgs e)
    {
        var x = Math.Clamp(e.GetPosition(ruler).X, 0, ruler.ActualWidth);
        return TimeSpan.FromSeconds(x / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond));
    }

    private void SetPlayheadFromRuler(Border ruler, MouseEventArgs e, bool updateRange)
    {
        var position = GetRulerPosition(ruler, e);
        if (updateRange)
        {
            _viewModel.Timeline.SetRangeSelection(_timelineRangeAnchor, position);
            UpdateProjectPreviewForRangeSelection();
        }

        PauseAndSeekProjectPreview(position);
    }

    private void SetPlayheadFromTimelineLane(Border lane, MouseEventArgs e)
    {
        var position = TimeSpan.FromSeconds(Math.Max(
            0,
            e.GetPosition(lane).X / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond)));
        PauseAndSeekProjectPreview(position);
    }

    private void TimelineRangeHandle_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is not Thumb thumb || !_viewModel.Timeline.HasRangeSelection)
        {
            return;
        }

        _rangeHandleMovesStart = string.Equals(thumb.Tag as string, "Start", StringComparison.Ordinal);
        _rangeHandleStartAnchor = _viewModel.Timeline.RangeStart;
        _rangeHandleEndAnchor = _viewModel.Timeline.RangeEnd;
        _rangeHandleDragPixels = 0;
        ProjectPreviewPlayer.Pause();
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
    }

    private void TimelineRangeHandle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        _rangeHandleDragPixels += e.HorizontalChange;
        var deltaSeconds = _rangeHandleDragPixels / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond);
        var frameSeconds = 1 / Math.Max(1, _viewModel.Timeline.FramesPerSecond);
        if (_rangeHandleMovesStart)
        {
            var maximumStart = _rangeHandleEndAnchor.TotalSeconds - frameSeconds;
            var rawCandidate = Math.Clamp(
                _rangeHandleStartAnchor.TotalSeconds + deltaSeconds,
                0,
                Math.Max(0, maximumStart));
            var candidate = Math.Clamp(
                _viewModel.SnapTimelineEdge(
                    TimeSpan.FromSeconds(rawCandidate),
                    SnapToClipRangesCheckBox.IsChecked == true).TotalSeconds,
                0,
                Math.Max(0, maximumStart));
            _viewModel.Timeline.SetRangeSelection(TimeSpan.FromSeconds(candidate), _rangeHandleEndAnchor);
            _viewModel.Timeline.SetPlayhead(_viewModel.Timeline.RangeStart);
        }
        else
        {
            var maximum = Math.Max(
                _viewModel.Timeline.TargetDuration.TotalSeconds,
                _viewModel.Timeline.Duration.TotalSeconds);
            var minimumEnd = _rangeHandleStartAnchor.TotalSeconds + frameSeconds;
            var rawCandidate = Math.Clamp(
                _rangeHandleEndAnchor.TotalSeconds + deltaSeconds,
                Math.Min(maximum, minimumEnd),
                maximum);
            var candidate = Math.Clamp(
                _viewModel.SnapTimelineEdge(
                    TimeSpan.FromSeconds(rawCandidate),
                    SnapToClipRangesCheckBox.IsChecked == true).TotalSeconds,
                Math.Min(maximum, minimumEnd),
                maximum);
            _viewModel.Timeline.SetRangeSelection(_rangeHandleStartAnchor, TimeSpan.FromSeconds(candidate));
            _viewModel.Timeline.SetPlayhead(_viewModel.Timeline.RangeEnd);
        }

        UpdateProjectPreviewForRangeSelection();
    }

    private void TimelineRangeHandle_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        ProjectPreviewStatusText.Text = $"Range selected: {_viewModel.Timeline.RangeText}. Render preview to update.";
    }

    private void UpdateProjectPreviewForRangeSelection()
    {
        ProjectPreviewPlayer.Pause();
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
        ProjectPreviewPositionText.Text = _viewModel.Timeline.HasRangeSelection
            ? FormatPreviewTime(_viewModel.Timeline.RangeStart)
            : FormatPreviewTime(_viewModel.Timeline.Playhead);
        ProjectPreviewDurationText.Text = _viewModel.Timeline.HasRangeSelection
            ? FormatPreviewTime(_viewModel.Timeline.RangeEnd)
            : _activeProjectPreviewChunk is null
                ? "0:00"
                : FormatPreviewTime(_activeProjectPreviewChunk.End);
        ProjectPreviewStatusText.Text = _viewModel.Timeline.HasRangeSelection
            ? $"Range selected: {_viewModel.Timeline.RangeText}. Cached prerenders remain available."
            : $"Range cleared. {_projectPreviewChunks.Count} cached prerender chunk(s) remain available.";
    }

    private void ResetProjectPreviewCache()
    {
        ProjectPreviewPlayer.Stop();
        ProjectPreviewPlayer.Source = null;
        _projectPreviewTimer.Stop();
        SetProjectPlaybackState(false);
        _projectPreviewAutoplayPending = false;
        _projectPreviewPendingSeek = null;
        _activeProjectPreviewChunk = null;
        _projectPreviewChunks.Clear();
        _projectPreviewTimelineOffset = TimeSpan.Zero;
        _projectPreviewTimelineEnd = TimeSpan.Zero;
        _projectPreviewPlaybackStart = TimeSpan.Zero;
        _projectPreviewPlaybackEnd = TimeSpan.Zero;
        ProjectPreviewSeekSlider.Value = 0;
        ProjectPreviewSeekSlider.Maximum = 1;
        ProjectPreviewPositionText.Text = "0:00";
        ProjectPreviewDurationText.Text = "0:00";
        QueueProjectPreviewOverlayRefresh();
    }

    private void FitTimelineHorizontally_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.FitTimelineHorizontally(Math.Max(100, TimelineHorizontalScrollViewer.ViewportWidth));
        TimelineHorizontalScrollViewer.ScrollToHorizontalOffset(0);
    }

    private void FitTimelineVertically_Click(object sender, RoutedEventArgs e) =>
        _viewModel.FitTimelineVertically(Math.Max(100, TimelineDropSurface.ActualHeight));

    private void TimelineZoomOut_Click(object sender, RoutedEventArgs e) =>
        _viewModel.Timeline.PixelsPerSecond /= 1.25;

    private void TimelineZoomIn_Click(object sender, RoutedEventArgs e) =>
        _viewModel.Timeline.PixelsPerSecond *= 1.25;

    private void TrackHeightDecrease_Click(object sender, RoutedEventArgs e) =>
        _viewModel.Timeline.TrackHeight -= 8;

    private void TrackHeightIncrease_Click(object sender, RoutedEventArgs e) =>
        _viewModel.Timeline.TrackHeight += 8;

    private void TimelineLaneItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: TimelineLaneItemViewModel item })
        {
            if (FindAncestor<Thumb>(e.OriginalSource as DependencyObject) is not null ||
                FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
            {
                _timelineDragItem = null;
                return;
            }

            // Measure before selection refreshes the lane projection. Coordinates against the detached
            // pre-refresh Border are not stable and previously produced the apparently random drag offset.
            var localOffset = TimeSpan.FromSeconds(
                Math.Clamp(e.GetPosition((Border)sender).X, 0, ((Border)sender).ActualWidth) /
                Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond));
            _viewModel.SelectTimelineItem(item.Id, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            PauseAndSeekProjectPreview(item.Start + localOffset);
            _timelineDragStart = e.GetPosition(this);
            _timelineDragItem = item;
            var selectedStart = _viewModel.TimelineLanes
                .SelectMany(lane => lane.Items)
                .Where(candidate => _viewModel.SelectedTimelineItemIds.Contains(candidate.Id))
                .Select(candidate => candidate.Start)
                .DefaultIfEmpty(item.Start)
                .Min();
            _timelineDragGrabOffset = item.Start - selectedStart + localOffset;
            TimelineDropSurface.Focus();
        }
    }

    private void TimelineLaneItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: TimelineLaneItemViewModel item })
        {
            return;
        }

        _viewModel.SelectTimelineItem(item.Id);
        if (item.Kind == ProjectItemKind.Video &&
            !string.IsNullOrWhiteSpace(item.SourcePath) &&
            File.Exists(item.SourcePath))
        {
            _viewModel.SelectCatalogMedia(item.SourcePath);
            PreviewTabs.SelectedItem = ClipPreviewTab;
            LoadClipPreview(item.SourcePath, AutoplayClipsCheckBox.IsChecked == true);
        }
        else if (item.Kind is ProjectItemKind.Effect or ProjectItemKind.TextOverlay or
                 ProjectItemKind.ImageOverlay or ProjectItemKind.Audio or ProjectItemKind.ProgressBar)
        {
            _viewModel.SelectedProjectLayer = _viewModel.ProjectLayers
                .FirstOrDefault(row => row.Item?.Id == item.Id);
            EditLayer_Click(sender, e);
        }

        _timelineDragItem = null;
        e.Handled = true;
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
        var data = new DataObject(
            typeof(TimelineDragData),
            new TimelineDragData(selected, _timelineDragGrabOffset));
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            HideTimelineDropPreviews();
        }
    }

    private void TimelineItemResize_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is not Thumb { DataContext: TimelineLaneItemViewModel item, Tag: string edge } ||
            !item.CanResize)
        {
            return;
        }

        // Selecting rebuilds TimelineLanes. Do it after the Thumb completes, not while WPF is
        // establishing capture, otherwise the active resize Thumb disappears before DragDelta.
        _timelineResizeItem = item;
        _timelineResizeMovesStart = edge == "Start";
        _timelineResizeOriginalStart = item.Start;
        _timelineResizeOriginalEnd = item.Start + item.Duration;
        _timelineResizePreviewStart = item.Start;
        _timelineResizePreviewDuration = item.Duration;
        _timelineResizePointerStartX = Mouse.GetPosition(TimelineCanvas).X;
        ShowTimelineDropPreview(item.TrackId, item.Start, item.Duration);
        e.Handled = true;
    }

    private void TimelineItemResize_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_timelineResizeItem is null)
        {
            return;
        }

        var dragPixels = Mouse.GetPosition(TimelineCanvas).X - _timelineResizePointerStartX;
        var delta = TimeSpan.FromSeconds(
            dragPixels / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond));
        var minimumDuration = TimeSpan.FromSeconds(_viewModel.Timeline.SnapIncrement);
        var snapToClips = SnapToClipRangesCheckBox.IsChecked == true;
        if (_timelineResizeMovesStart)
        {
            var candidate = _viewModel.SnapTimelineEdge(_timelineResizeOriginalStart + delta, snapToClips);
            var latestStart = _timelineResizeOriginalEnd - minimumDuration;
            _timelineResizePreviewStart = candidate < TimeSpan.Zero
                ? TimeSpan.Zero
                : candidate > latestStart ? latestStart : candidate;
            _timelineResizePreviewDuration = _timelineResizeOriginalEnd - _timelineResizePreviewStart;
        }
        else
        {
            var candidate = _viewModel.SnapTimelineEdge(_timelineResizeOriginalEnd + delta, snapToClips);
            var earliestEnd = _timelineResizeOriginalStart + minimumDuration;
            var end = candidate < earliestEnd ? earliestEnd : candidate;
            _timelineResizePreviewStart = _timelineResizeOriginalStart;
            _timelineResizePreviewDuration = end - _timelineResizeOriginalStart;
        }

        ShowTimelineDropPreview(
            _timelineResizeItem.TrackId,
            _timelineResizePreviewStart,
            _timelineResizePreviewDuration);
        e.Handled = true;
    }

    private void TimelineItemResize_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var item = _timelineResizeItem;
        _timelineResizeItem = null;
        HideTimelineDropPreviews();
        if (item is not null && !e.Canceled)
        {
            _viewModel.ResizeTimelineItem(
                item.Id,
                _timelineResizePreviewStart,
                _timelineResizePreviewDuration);
        }

        e.Handled = true;
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

    private void TimelineTrackHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: TimelineLaneViewModel lane })
        {
            return;
        }

        _viewModel.SelectedProjectLayer = _viewModel.ProjectLayers
            .FirstOrDefault(row => row.IsTrackHeader && row.Track.Id == lane.TrackId);
        _trackDragStart = e.GetPosition(this);
        _trackDragLane = lane;
    }

    private void TimelineTrackHeader_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not Border { DataContext: TimelineLaneViewModel lane } ||
            _trackDragLane?.TrackId != lane.TrackId)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _trackDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _trackDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _trackDragLane = null;
        DragDrop.DoDragDrop(
            (DependencyObject)sender,
            new DataObject(typeof(TrackDragData), new TrackDragData(lane.TrackId)),
            DragDropEffects.Move);
    }

    private void TimelineTrackHeader_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = sender is Border { DataContext: TimelineLaneViewModel target } &&
                    e.Data.GetData(typeof(TrackDragData)) is TrackDragData moving &&
                    moving.TrackId != target.TrackId
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void TimelineTrackHeader_Drop(object sender, DragEventArgs e)
    {
        if (sender is Border { DataContext: TimelineLaneViewModel target } targetBorder &&
            e.Data.GetData(typeof(TrackDragData)) is TrackDragData moving)
        {
            var insertAfter = e.GetPosition(targetBorder).Y >= targetBorder.ActualHeight / 2;
            _viewModel.MoveTrack(moving.TrackId, target.TrackId, insertAfter);
        }

        _trackDragLane = null;
        e.Handled = true;
    }

    private void TimelineTrackHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { DataContext: TimelineLaneViewModel lane })
        {
            return;
        }

        if (lane.TrackKind == ProjectTrackKind.Video)
        {
            if (!_previewsSplit)
            {
                PreviewTabs.SelectedItem = ProjectPreviewTab;
            }
            else
            {
                ProjectPreviewPane.Focus();
            }

            e.Handled = true;
        }
    }

    private void TimelineTrackColor_Click(object sender, RoutedEventArgs e)
    {
        SelectTimelineTrack(sender);
        ColorCode_Click(sender, e);
    }

    private void TimelineTrackAddEffect_Click(object sender, RoutedEventArgs e)
    {
        SelectTimelineTrack(sender);
        OpenCompatibleEffectEditor(_viewModel.SelectedProjectLayer, useSelectedItemTiming: false);
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

    private void TimelineItemToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectTimelineItem(sender);
        if (item is not null)
        {
            _viewModel.SetTimelineItemEnabled(item.Id, !item.IsEnabled);
        }
    }

    private void TimelineItemToggleTransformLock_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectTimelineItem(sender);
        if (item is not null)
        {
            ToggleOverlayTransformLock(item.Id);
        }
    }

    private void LayerItemToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectLayerCommandTarget(sender);
        if (row?.Item is not null)
        {
            _viewModel.SetTimelineItemEnabled(row.Item.Id, !row.Item.IsEnabled);
        }
    }

    private void LayerItemToggleTransformLock_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectLayerCommandTarget(sender);
        if (row?.Item is not null)
        {
            ToggleOverlayTransformLock(row.Item.Id);
        }
    }

    private void LayerTransformLockButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ProjectLayerRowViewModel { Item: { } item } })
        {
            _viewModel.SelectTimelineItem(item.Id);
            ToggleOverlayTransformLock(item.Id);
            e.Handled = true;
        }
    }

    private void TimelineTransformLockButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: TimelineLaneItemViewModel item })
        {
            _viewModel.SelectTimelineItem(item.Id);
            ToggleOverlayTransformLock(item.Id);
            e.Handled = true;
        }
    }

    private void ToggleOverlayTransformLock(Guid itemId)
    {
        var item = _viewModel.ProjectLayers.FirstOrDefault(row => row.Item?.Id == itemId)?.Item;
        if (item is null)
        {
            return;
        }

        if (_overlayTransformEditItemId == itemId)
        {
            ProjectPreviewOverlayCanvas.CompleteEdit(itemId, accepted: false);
            _overlayTransformEditItemId = null;
        }

        _viewModel.SetOverlayTransformLocked(itemId, !item.IsTransformLocked);
        QueueProjectPreviewOverlayRefresh();
    }

    private void TimelineProgressCopyStyle_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectTimelineItem(sender);
        var row = item is null ? null : _viewModel.ProjectLayers.FirstOrDefault(candidate => candidate.Item?.Id == item.Id);
        if (row?.Item is not { Kind: ProjectItemKind.ProgressBar } progress)
        {
            return;
        }

        _copiedProgressStyle = new ProjectTimelineItem
        {
            Kind = ProjectItemKind.ProgressBar,
            ProgressBarStyle = progress.ProgressBarStyle,
            ProgressBarPosition = progress.ProgressBarPosition,
            ProgressColor = progress.ProgressColor,
            ProgressHeight = progress.ProgressHeight
        };
    }

    private async void TimelineProgressPasteStyle_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectTimelineItem(sender);
        var row = item is null ? null : _viewModel.ProjectLayers.FirstOrDefault(candidate => candidate.Item?.Id == item.Id);
        if (row?.Item is not { Kind: ProjectItemKind.ProgressBar } progress || _copiedProgressStyle is null)
        {
            return;
        }

        progress.ProgressBarStyle = _copiedProgressStyle.ProgressBarStyle;
        progress.ProgressBarPosition = _copiedProgressStyle.ProgressBarPosition;
        progress.ProgressColor = _copiedProgressStyle.ProgressColor;
        progress.ProgressHeight = _copiedProgressStyle.ProgressHeight;
        _viewModel.UpdateSelectedLayerItem(progress);
        await _viewModel.RememberProgressDefaultsAsync(progress);
    }

    private void TimelineItemAddEffect_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectTimelineItem(sender);
        if (item is null)
        {
            return;
        }

        var row = _viewModel.ProjectLayers.FirstOrDefault(candidate => candidate.Item?.Id == item.Id);
        _viewModel.SelectedProjectLayer = row;
        OpenCompatibleEffectEditor(row, useSelectedItemTiming: true);
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
            if (e.Data.GetData(typeof(TimelineDragData)) is TimelineDragData timelineData)
            {
                var targetStart = GetTimelineDropStart(e, (Border)sender, timelineData);
                var preview = _viewModel.GetTimelineMovePreview(
                    timelineData.ItemIds,
                    lane.TrackId,
                    targetStart,
                    SnapToClipRangesCheckBox.IsChecked == true);
                e.Effects = preview is null ? DragDropEffects.None : DragDropEffects.Move;
                if (preview is not null)
                {
                    ShowTimelineDropPreview(lane.TrackId, preview.Start, preview.Duration);
                }
                else
                {
                    HideTimelineDropPreviews();
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                HideTimelineDropPreviews();
            }
        }

        e.Handled = true;
    }

    private void TimelineLane_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border { DataContext: TimelineLaneViewModel lane })
        {
            lane.HideDropPreview();
        }
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
            start = GetTimelineDropStart(e, laneBorder, timelineData);
            _viewModel.MoveTimelineItems(
                timelineData.ItemIds,
                lane.TrackId,
                start,
                SnapToClipRangesCheckBox.IsChecked == true);
        }

        HideTimelineDropPreviews();
        e.Handled = true;
    }

    private TimeSpan GetTimelineDropStart(
        DragEventArgs e,
        Border laneBorder,
        TimelineDragData dragData)
    {
        var pointerTime = TimeSpan.FromSeconds(
            e.GetPosition(laneBorder).X / Math.Max(0.1, _viewModel.Timeline.PixelsPerSecond));
        var start = pointerTime - dragData.GrabOffset;
        return start < TimeSpan.Zero ? TimeSpan.Zero : start;
    }

    private void ShowTimelineDropPreview(Guid trackId, TimeSpan start, TimeSpan duration)
    {
        foreach (var lane in _viewModel.TimelineLanes)
        {
            if (lane.TrackId == trackId)
            {
                lane.ShowDropPreview(start, duration, _viewModel.Timeline.PixelsPerSecond);
            }
            else
            {
                lane.HideDropPreview();
            }
        }
    }

    private void HideTimelineDropPreviews()
    {
        foreach (var lane in _viewModel.TimelineLanes)
        {
            lane.HideDropPreview();
        }
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

    private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var focusedElement = Keyboard.FocusedElement as DependencyObject;
        var editingControl = IsEditingControl(focusedElement);
        if (!editingControl && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            (e.Key == Key.Z || e.Key == Key.Y))
        {
            if (e.Key == Key.Y || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                await PerformRedoAsync();
            }
            else
            {
                await PerformUndoAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key != Key.Space)
        {
            return;
        }

        var panel = ResolveFocusedPanel(focusedElement) ?? _focusedPanel;
        if (editingControl && !IsSpaceShortcutButton(focusedElement))
        {
            return;
        }

        if (panel is WorkspacePanelKind.ContentBrowser or WorkspacePanelKind.Layers or WorkspacePanelKind.Timeline)
        {
            TogglePanelExpansion(panel);
            e.Handled = true;
        }
    }

    private async void Undo_Click(object sender, RoutedEventArgs e) =>
        await PerformUndoAsync();

    private async void Redo_Click(object sender, RoutedEventArgs e) =>
        await PerformRedoAsync();

    private async Task PerformUndoAsync()
    {
        if (CancelActiveOverlayTransformEdit())
        {
            ProjectPreviewStatusText.Text = "Overlay transform cancelled.";
            return;
        }

        try
        {
            await _viewModel.UndoAsync();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "The project was restored, but its recovery file could not be updated.", exception);
        }
    }

    private async Task PerformRedoAsync()
    {
        CancelActiveOverlayTransformEdit();
        try
        {
            await _viewModel.RedoAsync();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "The project was restored, but its recovery file could not be updated.", exception);
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
        UpdateExpandedPanelButton();
    }

    private void UpdateExpandedPanelButton()
    {
        var browserExpanded = _expandedPanel == WorkspacePanelKind.ContentBrowser;
        BrowserExpandButton.Content = browserExpanded ? "←" : "→";
        BrowserExpandButton.ToolTip = browserExpanded
            ? "Restore compact content browser"
            : "Expand content browser to full workspace width";
        AutomationProperties.SetName(
            BrowserExpandButton,
            browserExpanded ? "Restore compact content browser" : "Expand content browser");
    }

    private void ApplyPersistedWindowGeometry(ApplicationSettings settings)
    {
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        var hasSavedPosition = settings.WindowLeft != -1 || settings.WindowTop != -1;
        var hasPosition = hasSavedPosition &&
                          settings.WindowLeft >= SystemParameters.VirtualScreenLeft - settings.WindowWidth &&
                          settings.WindowLeft <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                          settings.WindowTop >= SystemParameters.VirtualScreenTop - settings.WindowHeight &&
                          settings.WindowTop <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        if (hasPosition)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
        }

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void ApplyPersistedWorkspaceGeometry(ApplicationSettings settings)
    {
        WorkspaceGrid.ColumnDefinitions[0].Width = new GridLength(settings.WorkspaceLeftWidth);
        WorkspaceGrid.ColumnDefinitions[4].Width = new GridLength(settings.WorkspaceRightWidth);
        WorkspaceGrid.RowDefinitions[2].Height = new GridLength(settings.WorkspaceBottomHeight);
        PreviewSplitGrid.ColumnDefinitions[0].Width = new GridLength(settings.PreviewSplitRatio, GridUnitType.Star);
        PreviewSplitGrid.ColumnDefinitions[2].Width = new GridLength(1 - settings.PreviewSplitRatio, GridUnitType.Star);
    }

    private ApplicationSettings CaptureWorkspaceSettings()
    {
        var settings = _viewModel.Settings.Copy();
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        settings.WindowWidth = Math.Max(MinWidth, bounds.Width);
        settings.WindowHeight = Math.Max(MinHeight, bounds.Height);
        settings.WindowLeft = bounds.Left;
        settings.WindowTop = bounds.Top;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        settings.WorkspaceLeftWidth = WorkspaceGrid.ColumnDefinitions[0].ActualWidth;
        settings.WorkspaceRightWidth = WorkspaceGrid.ColumnDefinitions[4].ActualWidth;
        settings.WorkspaceBottomHeight = WorkspaceGrid.RowDefinitions[2].ActualHeight;
        settings.TimelinePixelsPerSecond = _viewModel.Timeline.PixelsPerSecond;
        settings.TimelineTrackHeight = _viewModel.Timeline.TrackHeight;
        var previewLeft = PreviewSplitGrid.ColumnDefinitions[0];
        var previewRight = PreviewSplitGrid.ColumnDefinitions[2];
        var previewWidth = previewLeft.ActualWidth + previewRight.ActualWidth;
        var previewGridUnits = previewLeft.Width.Value + previewRight.Width.Value;
        settings.PreviewSplitRatio = previewWidth > 0
            ? previewLeft.ActualWidth / previewWidth
            : previewGridUnits > 0 ? previewLeft.Width.Value / previewGridUnits : 0.5;
        settings.PreviewsSplit = _previewsSplit;
        settings.ActivePreviewTab = Math.Clamp(PreviewTabs.SelectedIndex, 0, 1);
        settings.ActiveWorkspacePanel = ToSettingsPanel(_focusedPanel);
        settings.ExpandedWorkspacePanel = _expandedPanel.HasValue
            ? ToSettingsPanel(_expandedPanel.Value)
            : null;
        return settings;
    }

    private static WorkspacePanelKind FromSettingsPanel(WorkspacePanelSelection panel) => panel switch
    {
        WorkspacePanelSelection.Preview => WorkspacePanelKind.Preview,
        WorkspacePanelSelection.Layers => WorkspacePanelKind.Layers,
        WorkspacePanelSelection.Timeline => WorkspacePanelKind.Timeline,
        _ => WorkspacePanelKind.ContentBrowser
    };

    private static WorkspacePanelKind? FromSettingsPanel(WorkspacePanelSelection? panel) =>
        panel.HasValue ? FromSettingsPanel(panel.Value) : null;

    private static WorkspacePanelSelection ToSettingsPanel(WorkspacePanelKind panel) => panel switch
    {
        WorkspacePanelKind.Preview => WorkspacePanelSelection.Preview,
        WorkspacePanelKind.Layers => WorkspacePanelSelection.Layers,
        WorkspacePanelKind.Timeline => WorkspacePanelSelection.Timeline,
        _ => WorkspacePanelSelection.ContentBrowser
    };

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

    private static bool IsSpaceShortcutButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ButtonBase)
            {
                return true;
            }

            if (element is TextBoxBase or ComboBox or Slider)
            {
                return false;
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
