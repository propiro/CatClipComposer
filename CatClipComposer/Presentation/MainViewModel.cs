using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using CatClipComposer.Core;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Core.Plugins;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IProjectStore _projectStore;
    private readonly IMediaCatalog _catalog;
    private readonly IMediaScanner _scanner;
    private readonly IVideoRenderer _videoRenderer;
    private readonly ICompositionExporter _compositionExporter;
    private readonly IPluginCatalog _plugins;
    private CancellationTokenSource? _operationCancellation;
    private ApplicationSettings _settings;
    private MediaCardViewModel? _selectedMedia;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";
    private double _scanProgress;
    private bool _isBusy;
    private bool _suppressProjectAutosave;
    private bool _suppressTimelineSelectionSync;
    private EditorProject _project;
    private ProjectLayerRowViewModel? _selectedProjectLayer;
    private readonly HashSet<Guid> _selectedTimelineItemIds = [];
    private readonly HashSet<Guid> _collapsedTrackIds = [];
    private bool _isDirty;
    private bool _projectPreviewCurrent;
    private TimeSpan _projectPreviewCoverageStart;
    private TimeSpan _projectPreviewCoverageEnd;

    public MainViewModel(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        IProjectStore projectStore,
        IMediaCatalog catalog,
        IMediaScanner scanner,
        IVideoRenderer videoRenderer,
        ICompositionExporter compositionExporter,
        IPluginCatalog plugins)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _projectStore = projectStore;
        _catalog = catalog;
        _scanner = scanner;
        _videoRenderer = videoRenderer;
        _compositionExporter = compositionExporter;
        _plugins = plugins;
        Timeline = new TimelineViewModel(15);
        _project = EditorProject.Create("Untitled project", CreateOutputSettings());
        Timeline.Changed += Timeline_Changed;
        Timeline.DisplaySettingsChanged += Timeline_DisplaySettingsChanged;
        Timeline.SelectionChanged += Timeline_SelectionChanged;
        Timeline.SetFrameRate(_project.Output.FramesPerSecond);
        Timeline.SetDisplaySettings(_project.TimelineRulerMode, _project.TimelineSnapMode);
        MediaView = CollectionViewSource.GetDefaultView(MediaFiles);
        MediaView.Filter = FilterMedia;
        RefreshProjectLayers();
    }

    public ObservableCollection<MediaCardViewModel> MediaFiles { get; } = [];

    public ObservableCollection<ProjectLayerRowViewModel> ProjectLayers { get; } = [];

    public ObservableCollection<ProjectTrackGroupViewModel> ProjectLayerGroups { get; } = [];

    public ObservableCollection<TimelineLaneViewModel> TimelineLanes { get; } = [];

    public TimelineViewModel Timeline { get; }

    public ICollectionView MediaView { get; }

    public ApplicationSettings Settings => _settings;

    public string ApplicationVersion => ProductInfo.DisplayVersion;

    public string WindowTitle => ProductInfo.WindowTitle;

    public string ProjectName => _project.Name;

    public string? ProjectFilePath => _project.ProjectFilePath;

    public ProjectOutputSettings OutputSettings => _project.Output;

    public IReadOnlyList<ICatClipPlugin> Plugins => _plugins.Plugins;

    public IReadOnlyCollection<Guid> SelectedTimelineItemIds => _selectedTimelineItemIds;

    public IReadOnlyList<ProjectTimelineItem> GetActivePositionableOverlayItems(TimeSpan position) =>
        _project.Tracks
            .Where(track => track.IsEnabled && track.Kind == ProjectTrackKind.Overlay)
            .OrderByDescending(track => track.Order)
            .SelectMany(track => track.Items)
            .Where(item => item.IsEnabled &&
                           item.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay &&
                           position >= item.Start &&
                           position < item.Start + item.Duration)
            .ToList();

    public (TimeSpan Start, TimeSpan Duration)? GetSelectedVideoRange()
    {
        var selected = _project.Tracks
            .SelectMany(track => track.Items)
            .Where(item => _selectedTimelineItemIds.Contains(item.Id) &&
                           item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
            .ToList();
        if (selected.Count == 0)
        {
            return null;
        }

        var start = selected.Min(item => item.Start);
        var end = selected.Max(item => item.Start + item.Duration);
        return (start, end - start);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public double TargetDurationMinutes => _project.TargetDurationMinutes;

    public string BackgroundColor => _project.BackgroundColor;

    public DateTime ProjectCreatedUtc => _project.CreatedUtc;

    public string ProjectSettingsSummary =>
        $"{_project.Output.Width}x{_project.Output.Height} · {_project.Output.FramesPerSecond:0.###} fps · " +
        $"{_project.Output.VideoEncoder} · {_project.TargetDurationMinutes:0.##} min target · bg {_project.BackgroundColor}";

    public ProjectLayerRowViewModel? SelectedProjectLayer
    {
        get => _selectedProjectLayer;
        set
        {
            if (ReferenceEquals(_selectedProjectLayer, value))
            {
                return;
            }

            if (_selectedProjectLayer is not null)
            {
                _selectedProjectLayer.IsSelected = false;
            }

            if (SetProperty(ref _selectedProjectLayer, value) && value is not null)
            {
                value.IsSelected = true;
            }
        }
    }

    public MediaCardViewModel? SelectedMedia
    {
        get => _selectedMedia;
        set => SetProperty(ref _selectedMedia, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                MediaView.Refresh();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public double ScanProgress
    {
        get => _scanProgress;
        private set => SetProperty(ref _scanProgress, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string SourceSummary => _settings.SourceFolders.Count switch
    {
        0 => "No source folders configured",
        1 => "1 source folder",
        _ => $"{_settings.SourceFolders.Count} source folders"
    };

    public string CatalogSummary => MediaFiles.Count switch
    {
        0 => "No clips in catalog",
        1 => "1 clip in catalog",
        _ => $"{MediaFiles.Count} clips in catalog"
    };

    public string BrowserViewModeText => _settings.BrowserViewMode switch
    {
        ContentBrowserViewMode.List => "View: list",
        ContentBrowserViewMode.LargeGrid => "View: large",
        _ => "View: small"
    };

    public bool IsBrowserListView => _settings.BrowserViewMode == ContentBrowserViewMode.List;

    public bool IsBrowserGridView => !IsBrowserListView;

    public double BrowserItemWidth => _settings.BrowserViewMode switch
    {
        ContentBrowserViewMode.LargeGrid => _settings.LargeThumbnailSize + 8,
        _ => _settings.SmallThumbnailSize + 8
    };

    public double BrowserItemHeight => _settings.BrowserViewMode switch
    {
        ContentBrowserViewMode.List => 76,
        ContentBrowserViewMode.LargeGrid => Math.Round(_settings.LargeThumbnailSize * 0.68) + 58,
        _ => Math.Round(_settings.SmallThumbnailSize * 0.65) + 58
    };

    public double BrowserCardWidth => IsBrowserListView
        ? double.NaN
        : _settings.BrowserViewMode == ContentBrowserViewMode.LargeGrid
            ? _settings.LargeThumbnailSize + 2
            : _settings.SmallThumbnailSize + 2;

    public double BrowserCardHeight => BrowserItemHeight - 6;

    public double BrowserThumbnailHeight => _settings.BrowserViewMode == ContentBrowserViewMode.LargeGrid
        ? Math.Round(_settings.LargeThumbnailSize * 0.68)
        : Math.Round(_settings.SmallThumbnailSize * 0.65);

    public async Task CycleBrowserViewModeAsync(CancellationToken cancellationToken = default)
    {
        _settings.BrowserViewMode = _settings.BrowserViewMode switch
        {
            ContentBrowserViewMode.List => ContentBrowserViewMode.SmallGrid,
            ContentBrowserViewMode.SmallGrid => ContentBrowserViewMode.LargeGrid,
            _ => ContentBrowserViewMode.List
        };
        NotifyBrowserLayoutChanged();
        await _settingsStore.SaveAsync(_settings, cancellationToken);
        StatusText = $"Content Browser {_settings.BrowserViewMode} view";
    }

    public async Task InitializeAsync(
        IProgress<StartupProgress>? startupProgress = null,
        CancellationToken cancellationToken = default)
    {
        startupProgress?.Report(new StartupProgress(
            7,
            $"Loaded {_plugins.Plugins.Count} effect/source module(s) from the portable plugins folder."));
        foreach (var diagnostic in _plugins.Diagnostics.Where(message =>
                     message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("not found", StringComparison.OrdinalIgnoreCase)))
        {
            startupProgress?.Report(new StartupProgress(7, diagnostic));
        }

        startupProgress?.Report(new StartupProgress(8, "Loading the existing clip catalog…"));
        await LoadCatalogAsync(cancellationToken);
        if (_settings.RescanLibraryOnStartup && _settings.SourceFolders.Count > 0)
        {
            startupProgress?.Report(new StartupProgress(18, "Rescanning configured video folders…"));
            var scanProgress = new Progress<ScanProgress>(update =>
            {
                var percent = update.Total == 0 ? 18 : 18 + update.Processed * 72d / update.Total;
                var message = string.IsNullOrWhiteSpace(update.CurrentFile)
                    ? "Finalizing the library catalog…"
                    : $"Scanning {update.Processed + 1} of {update.Total}: {update.CurrentFile}";
                startupProgress?.Report(new StartupProgress(percent, message));
            });
            await ScanAsync(false, scanProgress);
        }

        startupProgress?.Report(new StartupProgress(94, "Checking crash recovery data…"));
        var recovery = await _projectStore.LoadRecoveryAsync(cancellationToken);
        if (recovery is not null)
        {
            ApplyProject(recovery);
            IsDirty = true;
            StatusText = $"Recovered autosave: {recovery.Name}";
        }

        startupProgress?.Report(new StartupProgress(100, "Editor ready."));
    }

    public async Task NewProjectAsync(CancellationToken cancellationToken = default)
    {
        _suppressProjectAutosave = true;
        try
        {
            Timeline.Clear();
            _project = EditorProject.Create("Untitled project", CreateOutputSettings());
            Timeline.SetTargetDuration(_project.TargetDurationMinutes);
            Timeline.SetFrameRate(_project.Output.FramesPerSecond);
            Timeline.SetDisplaySettings(_project.TimelineRulerMode, _project.TimelineSnapMode);
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(OutputSettings));
            OnPropertyChanged(nameof(TargetDurationMinutes));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(ProjectCreatedUtc));
            OnPropertyChanged(nameof(ProjectSettingsSummary));
            RefreshProjectLayers();
        }
        finally
        {
            _suppressProjectAutosave = false;
        }

        await _projectStore.ClearRecoveryAsync(cancellationToken);
        await SaveRecoveryNowAsync(cancellationToken);
        IsDirty = false;
        StatusText = "New project";
    }

    public async Task OpenProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(projectPath, cancellationToken);
        ApplyProject(project);
        await SaveRecoveryNowAsync(cancellationToken);
        IsDirty = false;
        StatusText = $"Opened project: {project.Name}";
    }

    public async Task SaveProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        SynchronizeProjectFromTimeline();
        var fullPath = Path.GetFullPath(projectPath);
        _project.ProjectFilePath = fullPath;
        if (_project.Name.Equals("Untitled project", StringComparison.OrdinalIgnoreCase))
        {
            _project.Name = Path.GetFileNameWithoutExtension(fullPath);
        }

        await _projectStore.SaveAsync(_project, fullPath, cancellationToken);
        await _projectStore.SaveRecoveryAsync(_project, cancellationToken);
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectFilePath));
        OnPropertyChanged(nameof(OutputSettings));
        OnPropertyChanged(nameof(ProjectSettingsSummary));
        IsDirty = false;
        StatusText = $"Saved project: {_project.Name}";
    }

    public async Task SaveRecoveryNowAsync(CancellationToken cancellationToken = default)
    {
        SynchronizeProjectFromTimeline();
        await _projectStore.SaveRecoveryAsync(_project, cancellationToken);
    }

    public async Task ApplySettingsAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _settingsStore.SaveAsync(settings, cancellationToken);
        _settings = settings.Copy();
        foreach (var media in MediaFiles)
        {
            media.ShowFileName = _settings.ShowFileNames;
        }

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(SourceSummary));
        NotifyBrowserLayoutChanged();
        StatusText = "Preferences saved";
    }

    private void NotifyBrowserLayoutChanged()
    {
        OnPropertyChanged(nameof(BrowserViewModeText));
        OnPropertyChanged(nameof(IsBrowserListView));
        OnPropertyChanged(nameof(IsBrowserGridView));
        OnPropertyChanged(nameof(BrowserItemWidth));
        OnPropertyChanged(nameof(BrowserItemHeight));
        OnPropertyChanged(nameof(BrowserCardWidth));
        OnPropertyChanged(nameof(BrowserCardHeight));
        OnPropertyChanged(nameof(BrowserThumbnailHeight));
    }

    public async Task<ScanResult> ScanAsync(
        bool regeneratePreviews,
        IProgress<ScanProgress>? externalProgress = null)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("A scan is already running.");
        }

        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        ScanProgress = 0;
        StatusText = "Discovering video files…";

        try
        {
            var progress = new Progress<ScanProgress>(update =>
            {
                externalProgress?.Report(update);
                ScanProgress = update.Total == 0
                    ? 0
                    : update.Processed * 100d / update.Total;
                StatusText = string.IsNullOrWhiteSpace(update.CurrentFile)
                    ? "Finishing catalog update…"
                    : $"Scanning {update.Processed + 1} of {update.Total}: {update.CurrentFile}";
            });
            var result = await _scanner.ScanAsync(
                _settings,
                new ScanOptions(regeneratePreviews),
                progress,
                _operationCancellation.Token);
            await LoadCatalogAsync(_operationCancellation.Token);
            ScanProgress = 100;
            StatusText = result.Failed == 0
                ? $"Scan complete: {result.Added} added, {result.Updated} refreshed"
                : $"Scan complete with {result.Failed} failed file(s)";
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled";
            throw;
        }
        finally
        {
            IsBusy = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    public async Task<RenderResult> ExportAsync(string outputPath)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is already running.");
        }

        if (Timeline.Clips.Count == 0)
        {
            throw new InvalidOperationException("Add at least one clip to the timeline before exporting.");
        }

        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        ScanProgress = 0;
        StatusText = "Preparing compilation…";
        try
        {
            SynchronizeProjectFromTimeline();
            await _projectStore.SaveRecoveryAsync(_project, _operationCancellation.Token);
            var renderPlan = ProjectRenderMapper.Create(_project, _plugins);
            var orientation = _project.Output.Height > _project.Output.Width
                ? OutputOrientation.Portrait
                : OutputOrientation.Landscape;
            var progress = new Progress<RenderProgress>(update =>
            {
                ScanProgress = update.Percent;
                StatusText = update.Message;
            });
            var result = await _compositionExporter.ExportAsync(
                CreateRenderRequest(renderPlan, outputPath, orientation),
                _settings.FfmpegPath,
                progress,
                _operationCancellation.Token);
            await LoadCatalogAsync(_operationCancellation.Token);
            ScanProgress = 100;
            StatusText = $"Saved compilation: {Path.GetFileName(result.OutputPath)}";
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Export cancelled";
            throw;
        }
        finally
        {
            IsBusy = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    public async Task<RenderResult> RenderProjectPreviewAsync(
        TimeSpan? rangeStart = null,
        TimeSpan? rangeEnd = null)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is already running.");
        }

        SynchronizeProjectFromTimeline();
        var renderPlan = ProjectRenderMapper.Create(_project, _plugins);
        if (renderPlan.Segments.Count == 0)
        {
            throw new InvalidOperationException("Add at least one clip to a video timeline before previewing the project.");
        }

        var compositionDuration = TimeSpan.FromTicks(renderPlan.Segments.Sum(segment => segment.Duration.Ticks));
        TimeSpan? outputRangeStart = null;
        TimeSpan? outputRangeDuration = null;
        if (rangeStart.HasValue || rangeEnd.HasValue)
        {
            if (!rangeStart.HasValue || !rangeEnd.HasValue)
            {
                throw new ArgumentException("A preview range requires both start and end times.");
            }

            var start = rangeStart.Value < TimeSpan.Zero ? TimeSpan.Zero : rangeStart.Value;
            var end = rangeEnd.Value > compositionDuration ? compositionDuration : rangeEnd.Value;
            if (end <= start)
            {
                throw new InvalidOperationException("The selected range does not contain rendered project content.");
            }

            outputRangeStart = start;
            outputRangeDuration = end - start;
        }

        var previewFolder = Path.Combine(_settings.MetadataFolder, "project-previews");
        Directory.CreateDirectory(previewFolder);
        var outputPath = Path.Combine(previewFolder, $"{_project.Id:N}-{DateTime.UtcNow.Ticks}.mp4");
        var orientation = _project.Output.Height > _project.Output.Width
            ? OutputOrientation.Portrait
            : OutputOrientation.Landscape;
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        ScanProgress = 0;
        StatusText = "Rendering project preview…";
        try
        {
            await _projectStore.SaveRecoveryAsync(_project, _operationCancellation.Token);
            var progress = new Progress<RenderProgress>(update =>
            {
                ScanProgress = update.Percent;
                StatusText = $"Preview: {update.Message}";
            });
            var result = await _videoRenderer.RenderAsync(
                CreateRenderRequest(
                    renderPlan,
                    outputPath,
                    orientation,
                    VideoEncoderPreset.WindowsMediaFoundationH264,
                    outputRangeStart,
                    outputRangeDuration),
                _settings.FfmpegPath,
                progress,
                _operationCancellation.Token);
            foreach (var oldPreview in Directory.EnumerateFiles(previewFolder, $"{_project.Id:N}-*.mp4")
                         .Where(path => !path.Equals(outputPath, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    File.Delete(oldPreview);
                }
                catch (IOException)
                {
                    // Windows MediaElement may briefly retain the preceding preview file.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            ScanProgress = 100;
            StatusText = "Project preview ready";
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Preview cancelled";
            throw;
        }
        finally
        {
            IsBusy = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    public async Task<RenderResult> RenderEffectFramePreviewAsync(
        Guid trackId,
        ProjectTimelineItem previewItem,
        TimeSpan frame,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is already running.");
        }

        SynchronizeProjectFromTimeline();
        var previewProject = JsonSerializer.Deserialize<EditorProject>(JsonSerializer.Serialize(_project)) ??
                             throw new InvalidOperationException("The project could not be cloned for preview.");
        var track = previewProject.Tracks.FirstOrDefault(candidate => candidate.Id == trackId) ??
                    throw new InvalidOperationException("The effect timeline no longer exists.");
        var existingIndex = track.Items.FindIndex(item => item.Id == previewItem.Id);
        if (existingIndex >= 0)
        {
            track.Items[existingIndex] = previewItem;
        }
        else
        {
            track.Items.Add(previewItem);
        }

        var renderPlan = ProjectRenderMapper.Create(previewProject, _plugins);
        if (renderPlan.Segments.Count == 0)
        {
            throw new InvalidOperationException("Add at least one clip before previewing an effect frame.");
        }

        var compositionDuration = TimeSpan.FromTicks(renderPlan.Segments.Sum(segment => segment.Duration.Ticks));
        var frameDuration = TimeSpan.FromSeconds(Math.Max(0.1, 1 / Math.Clamp(previewProject.Output.FramesPerSecond, 1, 240)));
        var start = frame < TimeSpan.Zero ? TimeSpan.Zero : frame;
        if (start >= compositionDuration)
        {
            start = compositionDuration > frameDuration ? compositionDuration - frameDuration : TimeSpan.Zero;
        }

        var duration = compositionDuration - start < frameDuration
            ? compositionDuration - start
            : frameDuration;
        if (duration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The selected frame is outside rendered project content.");
        }

        var previewFolder = Path.Combine(_settings.MetadataFolder, "effect-frame-previews");
        Directory.CreateDirectory(previewFolder);
        var outputPath = Path.Combine(previewFolder, $"{previewProject.Id:N}-{DateTime.UtcNow.Ticks}.mp4");
        var orientation = previewProject.Output.Height > previewProject.Output.Width
            ? OutputOrientation.Portrait
            : OutputOrientation.Landscape;
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsBusy = true;
        StatusText = $"Rendering effect frame at {DurationFormatter.Format(start)}…";
        try
        {
            var result = await _videoRenderer.RenderAsync(
                CreateRenderRequest(
                    renderPlan,
                    outputPath,
                    orientation,
                    VideoEncoderPreset.WindowsMediaFoundationH264,
                    start,
                    duration,
                    previewProject),
                _settings.FfmpegPath,
                progress: null,
                _operationCancellation.Token);
            foreach (var oldPreview in Directory.EnumerateFiles(previewFolder, $"{previewProject.Id:N}-*.mp4")
                         .Where(path => !path.Equals(outputPath, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    File.Delete(oldPreview);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            StatusText = "Effect frame preview ready";
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Effect frame preview cancelled";
            throw;
        }
        finally
        {
            IsBusy = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    public void MarkProjectPreviewRendered(TimeSpan? rangeStart = null, TimeSpan? rangeEnd = null)
    {
        _projectPreviewCurrent = true;
        _projectPreviewCoverageStart = rangeStart ?? TimeSpan.Zero;
        _projectPreviewCoverageEnd = rangeEnd ?? Timeline.Duration;
        RefreshTimelineLanes();
    }

    public void CancelOperation() => _operationCancellation?.Cancel();

    public void AddSelectedToTimeline()
    {
        if (SelectedMedia is null)
        {
            return;
        }

        AddMediaToTimeline(SelectedMedia);
    }

    public void AddMediaToTimeline(MediaCardViewModel media)
    {
        Timeline.AddMedia(media.Media);
        StatusText = $"Added {media.FileName} to timeline";
    }

    public void AddMediaToTimeline(IEnumerable<MediaCardViewModel> mediaFiles)
    {
        var files = mediaFiles.DistinctBy(media => media.Media.Id).ToList();
        foreach (var media in files)
        {
            Timeline.AddMedia(media.Media);
        }

        if (files.Count > 0)
        {
            StatusText = files.Count == 1
                ? $"Added {files[0].FileName} to timeline"
                : $"Added {files.Count} clips to timeline";
        }
    }

    public void AddMediaToTrack(MediaCardViewModel media, Guid trackId, TimeSpan start)
    {
        EnsureProjectTracks(_project);
        var target = _project.Tracks.FirstOrDefault(track => track.Id == trackId);
        if (target is null || target.Kind != ProjectTrackKind.Video)
        {
            return;
        }

        var primary = _project.Tracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .OrderByDescending(track => track.Order)
            .First();
        if (target.Id == primary.Id)
        {
            Timeline.AddMedia(media.Media, start);
            return;
        }

        var item = new ProjectTimelineItem
        {
            Kind = ProjectItemKind.Video,
            Name = media.FileName,
            SourcePath = media.Media.FullPath,
            MediaFileId = media.Media.Id,
            StartTicks = SnapTime(start, target.Id).Ticks,
            DurationTicks = media.Media.Duration.Ticks,
            HasAudio = media.Media.HasAudio
        };
        target.Items.Add(item);
        MarkProjectDirty();
        _selectedTimelineItemIds.Clear();
        _selectedTimelineItemIds.Add(item.Id);
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Added {media.FileName} to {target.Name}";
    }

    public void AddStillImageToTimeline(string imagePath, TimeSpan duration)
    {
        Timeline.AddStillImage(imagePath, duration);
        StatusText = $"Added still screen: {Path.GetFileName(imagePath)}";
    }

    public async Task UpdateSelectedTagsAsync(
        string tags,
        CancellationToken cancellationToken = default)
    {
        if (SelectedMedia is null)
        {
            return;
        }

        await _catalog.UpdateTagsAsync(SelectedMedia.Media.Id, tags, cancellationToken);
        await LoadCatalogAsync(cancellationToken);
        StatusText = "Clip tags saved";
    }

    public async Task UpdateTagsAsync(
        IEnumerable<MediaCardViewModel> mediaFiles,
        string tags,
        CancellationToken cancellationToken = default)
    {
        var files = mediaFiles.DistinctBy(media => media.Media.Id).ToList();
        foreach (var media in files)
        {
            await _catalog.UpdateTagsAsync(media.Media.Id, tags, cancellationToken);
        }

        await LoadCatalogAsync(cancellationToken);
        StatusText = files.Count == 1 ? "Clip tags saved" : $"Tags saved for {files.Count} clips";
    }

    public void UpdateSelectedClipEffects(
        VideoFitMode fitMode,
        double fadeInSeconds,
        double fadeOutSeconds,
        double volume)
    {
        Timeline.UpdateSelectedEffects(fitMode, fadeInSeconds, fadeOutSeconds, volume);
        StatusText = "Clip effects updated";
    }

    public void UpdateSelectedLayerClipEffects(
        VideoFitMode fitMode,
        double fadeInSeconds,
        double fadeOutSeconds,
        double volume)
    {
        var item = SelectedProjectLayer?.Item;
        if (item is null || item.Kind is not (ProjectItemKind.Video or ProjectItemKind.StillImage))
        {
            return;
        }

        item.FitMode = fitMode;
        item.FadeInSeconds = fadeInSeconds;
        item.FadeOutSeconds = fadeOutSeconds;
        item.Volume = volume;
        MarkProjectDirty();
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = "Layer clip effects updated";
    }

    public void AddLayerItem(ProjectTrackKind trackKind, ProjectTimelineItem item)
    {
        EnsureProjectTracks(_project);
        var track = SelectedProjectLayer?.Track.Kind == trackKind
            ? SelectedProjectLayer.Track
            : _project.Tracks.OrderBy(candidate => candidate.Order)
                .First(candidate => candidate.Kind == trackKind);
        item.StartTicks = Math.Max(0, item.StartTicks);
        item.DurationTicks = Math.Max(TimeSpan.FromMilliseconds(100).Ticks, item.DurationTicks);
        track.Items.Add(item);
        MarkProjectDirty();
        _selectedTimelineItemIds.Clear();
        _selectedTimelineItemIds.Add(item.Id);
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Added {item.Name} to {track.Name}";
    }

    public void AddLayerItem(Guid trackId, ProjectTimelineItem item)
    {
        var track = _project.Tracks.FirstOrDefault(candidate => candidate.Id == trackId) ??
                    throw new InvalidOperationException("The selected timeline no longer exists.");
        item.StartTicks = Math.Max(0, item.StartTicks);
        item.DurationTicks = Math.Max(TimeSpan.FromMilliseconds(100).Ticks, item.DurationTicks);
        track.Items.Add(item);
        MarkProjectDirty();
        _selectedTimelineItemIds.Clear();
        _selectedTimelineItemIds.Add(item.Id);
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Added {item.Name} to {track.Name}";
    }

    public void AddTrack(ProjectTrackKind kind, string name)
    {
        foreach (var existing in _project.Tracks)
        {
            existing.Order++;
        }

        var track = new ProjectTrack
        {
            Name = name,
            Kind = kind,
            Order = 0
        };
        _project.Tracks.Add(track);
        MarkProjectDirty();
        RefreshProjectLayers(trackId: track.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Added timeline: {track.Name}";
    }

    public bool RemoveSelectedTrack()
    {
        var row = SelectedProjectLayer;
        if (row is null || !row.IsTrackHeader || row.Track.Items.Count > 0 ||
            _project.Tracks.Count(track => track.Kind == row.Track.Kind) <= 1)
        {
            return false;
        }

        SynchronizeProjectFromTimeline();
        var previousPrimaryId = GetPrimaryVideoTrack().Id;
        _project.Tracks.Remove(row.Track);
        for (var index = 0; index < _project.Tracks.Count; index++)
        {
            _project.Tracks[index].Order = index;
        }

        MarkProjectDirty();
        if (GetPrimaryVideoTrack().Id != previousPrimaryId)
        {
            LoadPrimaryTimeline();
        }
        RefreshProjectLayers();
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Removed timeline: {row.Track.Name}";
        return true;
    }

    public bool MoveSelectedTrack(int offset)
    {
        var track = SelectedProjectLayer?.Track;
        if (track is null || offset == 0)
        {
            return false;
        }

        var ordered = _project.Tracks.OrderBy(candidate => candidate.Order).ToList();
        var oldIndex = ordered.FindIndex(candidate => candidate.Id == track.Id);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= ordered.Count)
        {
            return false;
        }

        return MoveTrack(track.Id, ordered[newIndex].Id, offset > 0);
    }

    public bool MoveTrack(Guid trackId, Guid targetTrackId, bool insertAfter)
    {
        if (trackId == targetTrackId)
        {
            return false;
        }

        SynchronizeProjectFromTimeline();
        var previousPrimaryId = GetPrimaryVideoTrack().Id;
        var ordered = _project.Tracks.OrderBy(candidate => candidate.Order).ToList();
        var moving = ordered.FirstOrDefault(candidate => candidate.Id == trackId);
        var target = ordered.FirstOrDefault(candidate => candidate.Id == targetTrackId);
        if (moving is null || target is null)
        {
            return false;
        }

        ordered.Remove(moving);
        var targetIndex = ordered.IndexOf(target) + (insertAfter ? 1 : 0);
        ordered.Insert(Math.Clamp(targetIndex, 0, ordered.Count), moving);
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Order = index;
        }

        _project.Tracks = ordered;
        MarkProjectDirty();
        if (GetPrimaryVideoTrack().Id != previousPrimaryId)
        {
            LoadPrimaryTimeline();
        }
        RefreshProjectLayers(trackId: moving.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Moved timeline {moving.Name}";
        return true;
    }

    public void SetTrackColor(Guid trackId, string color)
    {
        var track = _project.Tracks.FirstOrDefault(candidate => candidate.Id == trackId);
        if (track is null)
        {
            return;
        }

        track.Color = color;
        MarkProjectDirty();
        RefreshProjectLayers(trackId: track.Id);
        _ = SaveRecoverySafelyAsync();
    }

    public void SetItemColor(Guid itemId, string color)
    {
        var entry = _project.Tracks
            .SelectMany(track => track.Items.Select(item => (Track: track, Item: item)))
            .FirstOrDefault(candidate => candidate.Item.Id == itemId);
        if (entry.Item is null)
        {
            return;
        }

        entry.Item.Color = color;
        MarkProjectDirty();
        RefreshProjectLayers(itemId);
        _ = SaveRecoverySafelyAsync();
    }

    public void RemoveSelectedLayerItem()
    {
        var row = SelectedProjectLayer;
        if (row?.Item is null)
        {
            return;
        }

        if (row.Track.Kind == ProjectTrackKind.Video)
        {
            if (Timeline.Select(row.Item.Id))
            {
                Timeline.RemoveSelected();
                return;
            }
        }

        row.Track.Items.RemoveAll(item => item.Id == row.Item.Id);
        MarkProjectDirty();
        RefreshProjectLayers();
        _ = SaveRecoverySafelyAsync();
        StatusText = "Layer item removed";
    }

    public void UpdateSelectedLayerItem(ProjectTimelineItem updatedItem)
    {
        var row = SelectedProjectLayer;
        if (row?.Item is null || row.Track.Kind == ProjectTrackKind.Video)
        {
            return;
        }

        var index = row.Track.Items.FindIndex(item => item.Id == row.Item.Id);
        if (index < 0)
        {
            return;
        }

        updatedItem.Id = row.Item.Id;
        row.Track.Items[index] = updatedItem;
        MarkProjectDirty();
        RefreshProjectLayers(updatedItem.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = "Layer item updated";
    }

    public bool UpdateOverlayTransform(
        Guid itemId,
        double x,
        double y,
        double scale,
        double rotationDegrees,
        bool commit)
    {
        var item = _project.Tracks
            .Where(track => track.Kind == ProjectTrackKind.Overlay)
            .SelectMany(track => track.Items)
            .FirstOrDefault(candidate => candidate.Id == itemId &&
                                         candidate.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay);
        if (item is null)
        {
            return false;
        }

        item.HasCustomOverlayTransform = true;
        item.OverlayX = OverlayTransformValues.NormalizeCoordinate(x);
        item.OverlayY = OverlayTransformValues.NormalizeCoordinate(y);
        item.OverlayScale = OverlayTransformValues.NormalizeScale(scale);
        item.OverlayRotationDegrees = OverlayTransformValues.NormalizeRotation(rotationDegrees);
        MarkProjectDirty();
        if (commit)
        {
            RefreshProjectLayers(item.Id);
            _ = SaveRecoverySafelyAsync();
            StatusText = $"Updated preview transform for {item.Name}";
        }

        return true;
    }

    public void ApplyProjectSettings(
        string projectName,
        double targetDurationMinutes,
        string backgroundColor,
        ProjectOutputSettings settings)
    {
        _project.Name = projectName;
        _project.TargetDurationMinutes = targetDurationMinutes;
        _project.BackgroundColor = backgroundColor;
        _project.Output = settings;
        MarkProjectDirty();
        Timeline.SetTargetDuration(targetDurationMinutes);
        Timeline.SetFrameRate(settings.FramesPerSecond);
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(OutputSettings));
        OnPropertyChanged(nameof(TargetDurationMinutes));
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ProjectSettingsSummary));
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Project settings updated: {settings.PresetName}";
    }

    public void MoveSelectedTimelineClip(int offset)
    {
        Timeline.MoveSelected(offset);
    }

    public void RemoveSelectedTimelineClip() => RemoveSelectedTimelineItems();

    public void RemoveSelectedTimelineItems()
    {
        var selected = _selectedTimelineItemIds.Count > 0
            ? _selectedTimelineItemIds.ToHashSet()
            : Timeline.SelectedClip is null
                ? []
                : new HashSet<Guid> { Timeline.SelectedClip.InstanceId };
        if (selected.Count == 0)
        {
            return;
        }

        var primary = _project.Tracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .OrderByDescending(track => track.Order)
            .First();
        var primaryIds = selected.Where(id => primary.Items.Any(item => item.Id == id)).ToHashSet();
        if (primaryIds.Count > 0)
        {
            Timeline.Remove(primaryIds);
        }

        foreach (var track in _project.Tracks.Where(track => track.Id != primary.Id))
        {
            track.Items.RemoveAll(item => selected.Contains(item.Id));
        }

        _selectedTimelineItemIds.Clear();
        MarkProjectDirty();
        RefreshProjectLayers();
        _ = SaveRecoverySafelyAsync();
        StatusText = selected.Count == 1 ? "Timeline item removed" : $"Removed {selected.Count} timeline items";
    }

    public void ClearTimeline()
    {
        Timeline.Clear();
        StatusText = "Timeline cleared";
    }

    public void SelectTimelineItem(Guid itemId, bool additive = false)
    {
        if (additive && _selectedTimelineItemIds.Contains(itemId))
        {
            _selectedTimelineItemIds.Remove(itemId);
            RefreshTimelineLanes();
            return;
        }

        if (!additive)
        {
            _selectedTimelineItemIds.Clear();
        }

        _selectedTimelineItemIds.Add(itemId);
        _suppressTimelineSelectionSync = true;
        try
        {
            if (!Timeline.Select(itemId))
            {
                Timeline.SelectedClip = null;
            }

            SelectedProjectLayer = ProjectLayers.FirstOrDefault(row => row.Item?.Id == itemId);
            var selectedGroup = ProjectLayerGroups.FirstOrDefault(group =>
                group.Items.Any(row => row.Item?.Id == itemId));
            if (selectedGroup is not null)
            {
                selectedGroup.IsExpanded = true;
            }
        }
        finally
        {
            _suppressTimelineSelectionSync = false;
        }

        RefreshTimelineLanes();
    }

    public TimeSpan SnapTime(
        TimeSpan candidate,
        Guid trackId,
        IReadOnlyCollection<Guid>? excludedIds = null,
        TimeSpan? movingDuration = null,
        bool snapToClipRanges = false)
    {
        var increment = Timeline.SnapIncrement;
        var seconds = Math.Max(0, candidate.TotalSeconds);
        var candidates = new List<double> { Math.Round(seconds / increment) * increment };
        var track = _project.Tracks.FirstOrDefault(item => item.Id == trackId);
        if (track is not null)
        {
            foreach (var item in track.Items.Where(item => excludedIds?.Contains(item.Id) != true))
            {
                candidates.Add(item.Start.TotalSeconds);
                candidates.Add((item.Start + item.Duration).TotalSeconds);
            }
        }

        if (snapToClipRanges)
        {
            foreach (var boundary in GetPrimaryVideoTrack().Items
                         .SelectMany(item => new[] { item.Start, item.Start + item.Duration })
                         .Append(TimeSpan.Zero)
                         .Distinct())
            {
                candidates.Add(boundary.TotalSeconds);
                if (movingDuration > TimeSpan.Zero)
                {
                    candidates.Add(boundary.TotalSeconds - movingDuration.Value.TotalSeconds);
                }
            }
        }

        var threshold = Math.Max(increment / 2, 8 / Math.Max(0.1, Timeline.PixelsPerSecond));
        var nearest = candidates.Where(value => value >= 0).OrderBy(value => Math.Abs(value - seconds)).First();
        return TimeSpan.FromSeconds(Math.Abs(nearest - seconds) <= threshold ? nearest : seconds);
    }

    public TimeSpan SnapTimelineEdge(TimeSpan candidate, bool snapToClipRanges)
    {
        var increment = Timeline.SnapIncrement;
        var seconds = Math.Max(0, candidate.TotalSeconds);
        var candidates = new List<double> { Math.Round(seconds / increment) * increment };
        if (snapToClipRanges)
        {
            candidates.AddRange(GetPrimaryVideoTrack().Items
                .SelectMany(item => new[] { item.Start.TotalSeconds, (item.Start + item.Duration).TotalSeconds }));
        }

        var threshold = Math.Max(increment / 2, 8 / Math.Max(0.1, Timeline.PixelsPerSecond));
        var nearest = candidates.OrderBy(value => Math.Abs(value - seconds)).First();
        return TimeSpan.FromSeconds(Math.Abs(nearest - seconds) <= threshold ? nearest : seconds);
    }

    public TimelineItemMovePreview? GetTimelineMovePreview(
        IReadOnlyCollection<Guid> itemIds,
        Guid targetTrackId,
        TimeSpan targetStart,
        bool snapToClipRanges)
    {
        if (itemIds.Count == 0)
        {
            return null;
        }

        var target = _project.Tracks.FirstOrDefault(track => track.Id == targetTrackId);
        var sourceTracks = _project.Tracks
            .Where(track => track.Items.Any(item => itemIds.Contains(item.Id)))
            .ToList();
        if (target is null || sourceTracks.Count != 1 || sourceTracks[0].Id != target.Id)
        {
            return null;
        }

        var moving = target.Items.Where(item => itemIds.Contains(item.Id)).OrderBy(item => item.StartTicks).ToList();
        if (moving.Count == 0)
        {
            return null;
        }

        var selectionStart = moving[0].Start;
        var selectionEnd = moving.Max(item => item.Start + item.Duration);
        var selectionDuration = selectionEnd - selectionStart;
        if (target.Id == GetPrimaryVideoTrack().Id)
        {
            selectionDuration = TimeSpan.FromTicks(moving.Sum(item => item.Duration.Ticks));
            var remaining = target.Items.Where(item => !itemIds.Contains(item.Id)).OrderBy(item => item.StartTicks).ToList();
            var insertionIndex = 0;
            var position = TimeSpan.Zero;
            while (insertionIndex < remaining.Count &&
                   position + TimeSpan.FromTicks(remaining[insertionIndex].Duration.Ticks / 2) <= targetStart)
            {
                position += remaining[insertionIndex].Duration;
                insertionIndex++;
            }

            return new TimelineItemMovePreview(position, selectionDuration);
        }

        var snappedStart = SnapTime(
            targetStart,
            target.Id,
            itemIds,
            selectionDuration,
            snapToClipRanges);
        return new TimelineItemMovePreview(snappedStart, selectionDuration);
    }

    public bool MoveTimelineItems(
        IReadOnlyCollection<Guid> itemIds,
        Guid targetTrackId,
        TimeSpan targetStart,
        bool snapToClipRanges = false)
    {
        if (itemIds.Count == 0)
        {
            return false;
        }

        var target = _project.Tracks.FirstOrDefault(track => track.Id == targetTrackId);
        if (target is null)
        {
            return false;
        }

        var sourceTracks = _project.Tracks
            .Where(track => track.Items.Any(item => itemIds.Contains(item.Id)))
            .ToList();
        if (sourceTracks.Count != 1 || sourceTracks[0].Id != target.Id)
        {
            StatusText = "Move items within the same timeline; copy media to another timeline from the browser.";
            return false;
        }

        var primary = _project.Tracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .OrderByDescending(track => track.Order)
            .First();
        if (target.Id == primary.Id)
        {
            var moved = Timeline.MoveSelection(itemIds, targetStart);
            if (moved)
            {
                _selectedTimelineItemIds.Clear();
                _selectedTimelineItemIds.UnionWith(itemIds);
                RefreshTimelineLanes();
            }

            return moved;
        }

        var moving = target.Items.Where(item => itemIds.Contains(item.Id)).OrderBy(item => item.StartTicks).ToList();
        if (moving.Count == 0)
        {
            return false;
        }

        var originalStart = moving[0].Start;
        var preview = GetTimelineMovePreview(itemIds, targetTrackId, targetStart, snapToClipRanges);
        if (preview is null)
        {
            return false;
        }

        var snappedStart = preview.Start;
        var offset = snappedStart - originalStart;
        if (moving.Any(item => item.Start + offset < TimeSpan.Zero))
        {
            offset = -originalStart;
        }

        foreach (var item in moving)
        {
            item.StartTicks = (item.Start + offset).Ticks;
        }

        MarkProjectDirty();
        RefreshProjectLayers(moving[0].Id);
        _selectedTimelineItemIds.Clear();
        _selectedTimelineItemIds.UnionWith(itemIds);
        RefreshTimelineLanes();
        _ = SaveRecoverySafelyAsync();
        StatusText = moving.Count == 1 ? "Timeline item moved" : $"Moved {moving.Count} timeline items";
        return true;
    }

    public bool ResizeTimelineItem(Guid itemId, TimeSpan start, TimeSpan duration)
    {
        var track = _project.Tracks.FirstOrDefault(candidate => candidate.Items.Any(item => item.Id == itemId));
        var item = track?.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (track is null || item is null || track.Id == GetPrimaryVideoTrack().Id)
        {
            return false;
        }

        var minimumDuration = TimeSpan.FromSeconds(Timeline.SnapIncrement);
        item.StartTicks = Math.Max(0, start.Ticks);
        item.DurationTicks = Math.Max(minimumDuration.Ticks, duration.Ticks);
        MarkProjectDirty();
        _selectedTimelineItemIds.Clear();
        _selectedTimelineItemIds.Add(item.Id);
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Resized {item.Name} to {DurationFormatter.Format(item.Start)} – " +
                     DurationFormatter.Format(item.Start + item.Duration);
        return true;
    }

    public void FitTimelineHorizontally(double availableWidth)
    {
        var duration = Math.Max(1, Math.Max(Timeline.TargetDuration.TotalSeconds, Timeline.Duration.TotalSeconds));
        Timeline.PixelsPerSecond = Math.Max(0.1, (availableWidth - 2) / duration);
    }

    public void FitTimelineVertically(double availableHeight)
    {
        Timeline.TrackHeight = Math.Max(28, (availableHeight - 34) / Math.Max(1, TimelineLanes.Count));
    }

    public void CycleTimelineRulerMode() => Timeline.CycleRulerMode();

    public void CycleTimelineSnapMode() => Timeline.CycleSnapMode();

    public MediaCardViewModel? SelectCatalogMedia(string sourcePath)
    {
        var media = MediaFiles.FirstOrDefault(item =>
            item.FullPath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase));
        if (media is not null)
        {
            SelectedMedia = media;
        }

        return media;
    }

    private async Task LoadCatalogAsync(CancellationToken cancellationToken)
    {
        var files = await _catalog.GetAllAsync(cancellationToken: cancellationToken);
        var selectedId = SelectedMedia?.Media.Id;
        MediaFiles.Clear();
        for (var index = 0; index < files.Count; index++)
        {
            MediaFiles.Add(new MediaCardViewModel(
                files[index],
                index + 1,
                _settings.ShowFileNames));
        }

        SelectedMedia = selectedId.HasValue
            ? MediaFiles.FirstOrDefault(item => item.Media.Id == selectedId.Value)
            : MediaFiles.FirstOrDefault();
        MediaView.Refresh();
        OnPropertyChanged(nameof(CatalogSummary));
    }

    private bool FilterMedia(object item)
    {
        if (item is not MediaCardViewModel media || string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return media.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               media.FullPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               media.Media.Tags.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private async void Timeline_Changed(object? sender, EventArgs e)
    {
        if (_suppressProjectAutosave)
        {
            return;
        }

        try
        {
            MarkProjectDirty();
            SynchronizeProjectFromTimeline();
            RefreshProjectLayers();
            await SaveRecoveryNowAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Recovery autosave failed: {exception.Message}";
        }
    }

    private void Timeline_SelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressTimelineSelectionSync)
        {
            return;
        }

        _selectedTimelineItemIds.Clear();
        if (Timeline.SelectedClip is not null)
        {
            _selectedTimelineItemIds.Add(Timeline.SelectedClip.InstanceId);
        }

        RefreshTimelineLanes();
    }

    private async void Timeline_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        RefreshTimelineLanes();
        if (_suppressProjectAutosave)
        {
            return;
        }

        var projectSettingsChanged = _project.TimelineRulerMode != Timeline.RulerMode ||
                                     _project.TimelineSnapMode != Timeline.SnapMode;
        _project.TimelineRulerMode = Timeline.RulerMode;
        _project.TimelineSnapMode = Timeline.SnapMode;
        if (!projectSettingsChanged)
        {
            return;
        }

        try
        {
            MarkProjectDirty();
            await SaveRecoveryNowAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Recovery autosave failed: {exception.Message}";
        }
    }

    private void ApplyProject(EditorProject project)
    {
        EnsureProjectTracks(project);
        var media = MediaFiles.Select(card => card.Media).ToList();
        var mediaById = media.ToDictionary(item => item.Id);
        var mediaByPath = media.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);
        var videoItems = project.Tracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .OrderByDescending(track => track.Order)
            .First()
            .Items;

        _suppressProjectAutosave = true;
        try
        {
            _project = project;
            Timeline.SetTargetDuration(project.TargetDurationMinutes);
            Timeline.SetFrameRate(project.Output.FramesPerSecond);
            Timeline.SetDisplaySettings(project.TimelineRulerMode, project.TimelineSnapMode);
            Timeline.ReplaceProjectItems(videoItems, mediaById, mediaByPath);
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(OutputSettings));
            OnPropertyChanged(nameof(TargetDurationMinutes));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(ProjectCreatedUtc));
            OnPropertyChanged(nameof(ProjectSettingsSummary));
            RefreshProjectLayers();
        }
        finally
        {
            _suppressProjectAutosave = false;
        }
    }

    private void SynchronizeProjectFromTimeline()
    {
        EnsureProjectTracks(_project);
        var videoTrack = GetPrimaryVideoTrack();
        videoTrack.Items = Timeline.CreateProjectItems().ToList();
        _project.ModifiedUtc = DateTime.UtcNow;
    }

    private ProjectTrack GetPrimaryVideoTrack() => _project.Tracks
        .Where(track => track.Kind == ProjectTrackKind.Video)
        .OrderByDescending(track => track.Order)
        .First();

    private void LoadPrimaryTimeline()
    {
        var media = MediaFiles.Select(card => card.Media).ToList();
        var mediaById = media.ToDictionary(item => item.Id);
        var mediaByPath = media.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);
        _suppressProjectAutosave = true;
        try
        {
            Timeline.ReplaceProjectItems(GetPrimaryVideoTrack().Items, mediaById, mediaByPath);
        }
        finally
        {
            _suppressProjectAutosave = false;
        }
    }

    private void RefreshProjectLayers(Guid? selectedItemId = null, Guid? trackId = null)
    {
        EnsureProjectTracks(_project);
        selectedItemId ??= SelectedProjectLayer?.Item?.Id;
        ProjectLayers.Clear();
        ProjectLayerGroups.Clear();
        foreach (var track in _project.Tracks.OrderBy(track => track.Order))
        {
            var header = ProjectLayerRowViewModel.ForTrack(track);
            ProjectLayers.Add(header);
            var rows = track.Items
                .OrderBy(item => item.StartTicks)
                .Select(item => ProjectLayerRowViewModel.ForItem(track, item))
                .ToList();
            foreach (var row in rows)
            {
                ProjectLayers.Add(row);
            }

            ProjectLayerGroups.Add(new ProjectTrackGroupViewModel(
                track,
                rows,
                !_collapsedTrackIds.Contains(track.Id),
                (id, expanded) =>
                {
                    if (expanded)
                    {
                        _collapsedTrackIds.Remove(id);
                    }
                    else
                    {
                        _collapsedTrackIds.Add(id);
                    }
                }));
        }

        SelectedProjectLayer = selectedItemId.HasValue
            ? ProjectLayers.FirstOrDefault(row => row.Item?.Id == selectedItemId.Value)
            : trackId.HasValue
                ? ProjectLayers.FirstOrDefault(row => row.IsTrackHeader && row.Track.Id == trackId.Value)
                : ProjectLayers.FirstOrDefault();
        RefreshTimelineLanes();
    }

    private void RefreshTimelineLanes()
    {
        EnsureProjectTracks(_project);
        var clips = Timeline.Clips.ToDictionary(clip => clip.InstanceId);
        var kindOrdinals = new Dictionary<ProjectTrackKind, int>();
        TimelineLanes.Clear();
        var primaryVideoTrackId = GetPrimaryVideoTrack().Id;
        foreach (var track in _project.Tracks.OrderBy(track => track.Order))
        {
            kindOrdinals[track.Kind] = kindOrdinals.GetValueOrDefault(track.Kind) + 1;
            var items = track.Items
                .OrderBy(item => item.StartTicks)
                .Select(item => new TimelineLaneItemViewModel(
                    track,
                    item,
                    clips.GetValueOrDefault(item.Id),
                    Timeline.PixelsPerSecond,
                    Timeline.TrackHeight,
                    _selectedTimelineItemIds.Contains(item.Id),
                    NeedsProjectPreview(item),
                    track.Id != primaryVideoTrackId));
            TimelineLanes.Add(new TimelineLaneViewModel(track, kindOrdinals[track.Kind], items));
        }
    }

    private bool NeedsProjectPreview(ProjectTimelineItem item) =>
        item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage &&
        (!_projectPreviewCurrent || item.Start < _projectPreviewCoverageStart ||
         item.Start + item.Duration > _projectPreviewCoverageEnd);

    private async Task SaveRecoverySafelyAsync()
    {
        try
        {
            await SaveRecoveryNowAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Recovery autosave failed: {exception.Message}";
        }
    }

    private void MarkProjectDirty()
    {
        _project.ModifiedUtc = DateTime.UtcNow;
        IsDirty = true;
        _projectPreviewCurrent = false;
    }

    private RenderRequest CreateRenderRequest(
        ProjectRenderPlan renderPlan,
        string outputPath,
        OutputOrientation orientation,
        VideoEncoderPreset? videoEncoderOverride = null,
        TimeSpan? outputRangeStart = null,
        TimeSpan? outputRangeDuration = null,
        EditorProject? sourceProject = null)
    {
        sourceProject ??= _project;
        return new RenderRequest(
            renderPlan.Segments,
            outputPath,
            orientation,
            videoEncoderOverride ?? sourceProject.Output.VideoEncoder,
            sourceProject.Output.FramesPerSecond,
            ProjectName: sourceProject.Name,
            ProjectFilePath: sourceProject.ProjectFilePath,
            OutputWidth: sourceProject.Output.Width,
            OutputHeight: sourceProject.Output.Height,
            QualityPercent: sourceProject.Output.QualityPercent,
            VideoBitrateKbps: sourceProject.Output.VideoBitrateKbps,
            AudioBitrateKbps: sourceProject.Output.AudioBitrateKbps,
            BackgroundColor: sourceProject.BackgroundColor,
            TimedOverlays: renderPlan.TimedOverlays,
            AudioLayers: renderPlan.AudioLayers,
            PluginEffects: renderPlan.PluginEffects,
            OutputRangeStart: outputRangeStart,
            OutputRangeDuration: outputRangeDuration);
    }

    private static void EnsureProjectTracks(EditorProject project)
    {
        if (project.Tracks.All(track => track.Kind != ProjectTrackKind.Background))
        {
            foreach (var track in project.Tracks)
            {
                track.Order++;
            }

            project.Tracks.Add(new ProjectTrack
            {
                Name = "Background",
                Kind = ProjectTrackKind.Background,
                Order = 0
            });
        }

        foreach (var kind in Enum.GetValues<ProjectTrackKind>())
        {
            if (project.Tracks.All(track => track.Kind != kind))
            {
                project.Tracks.Add(new ProjectTrack
                {
                    Name = kind.ToString(),
                    Kind = kind,
                    Order = project.Tracks.Count
                });
            }
        }

        project.Tracks = project.Tracks.OrderBy(track => track.Order).ToList();
    }

    private static ProjectOutputSettings CreateOutputSettings() => new();

}
