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
    private readonly ProjectUndoHistory _projectHistory;
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
    private readonly ProjectPreviewCoverageCatalog _projectPreviewCoverage = new();
    private readonly Dictionary<string, List<(TimeSpan Start, TimeSpan End)>> _renderedCoverageByFingerprint =
        new(StringComparer.Ordinal);
    private OverlayTransformDraft? _overlayTransformDraft;
    private readonly SemaphoreSlim _catalogReferenceGate = new(1, 1);

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
        _projectHistory = new ProjectUndoHistory(settings.UndoHistoryCapacity);
        Timeline = new TimelineViewModel(15);
        Timeline.PixelsPerSecond = settings.TimelinePixelsPerSecond;
        Timeline.TrackHeight = settings.TimelineTrackHeight;
        _project = EditorProject.Create("Untitled project", CreateOutputSettings());
        _projectHistory.Reset(_project, isSaved: true);
        Timeline.Changed += Timeline_Changed;
        Timeline.DisplaySettingsChanged += Timeline_DisplaySettingsChanged;
        Timeline.SelectionChanged += Timeline_SelectionChanged;
        Timeline.SetFrameRate(_project.Output.FramesPerSecond);
        Timeline.SetDisplaySettings(_project.TimelineRulerMode, _project.TimelineSnapMode);
        MediaView = CollectionViewSource.GetDefaultView(MediaFiles);
        MediaView.Filter = FilterMedia;
        ApplyMediaSort();
        RefreshProjectLayers();
    }

    public ObservableCollection<MediaCardViewModel> MediaFiles { get; } = [];

    public ObservableCollection<ProjectLayerRowViewModel> ProjectLayers { get; } = [];

    public ObservableCollection<ProjectTrackGroupViewModel> ProjectLayerGroups { get; } = [];

    public ObservableCollection<TimelineLaneViewModel> TimelineLanes { get; } = [];

    public ObservableCollection<ProjectPreviewCoverageSegmentViewModel> ProjectPreviewCoverageSegments { get; } = [];

    public ObservableCollection<ActionHistoryEntryViewModel> ActionHistory { get; } = [];

    public TimelineViewModel Timeline { get; }

    public ICollectionView MediaView { get; }

    public ApplicationSettings Settings => _settings;

    public string ApplicationVersion => ProductInfo.DisplayVersion;

    public string WindowTitle => $"{ProductInfo.WindowTitle} — {ProjectDisplayName}";

    public string ProjectName => _project.Name;

    public string ProjectDisplayName => $"{_project.Name}{(IsDirty ? " *" : string.Empty)}";

    public string? ProjectFilePath => _project.ProjectFilePath;

    public ProjectOutputSettings OutputSettings => _project.Output;

    public IReadOnlyList<ICatClipPlugin> Plugins => _plugins.Plugins;

    public IReadOnlyList<string> RecentProjectPaths => _settings.RecentProjectPaths;

    public IReadOnlyList<TextOverlayPreset> TextOverlayPresets => _settings.TextOverlayPresets;

    public IReadOnlyList<ContentBrowserSortChoice> BrowserSortChoices { get; } =
    [
        new(ContentBrowserSortMode.Name, "Name"),
        new(ContentBrowserSortMode.Date, "Date (newest)"),
        new(ContentBrowserSortMode.Length, "Length"),
        new(ContentBrowserSortMode.Tag, "Custom (tag)")
    ];

    public ContentBrowserSortMode BrowserSortMode
    {
        get => _settings.BrowserSortMode;
        set
        {
            if (_settings.BrowserSortMode == value)
            {
                return;
            }

            _settings.BrowserSortMode = value;
            OnPropertyChanged();
            ApplyMediaSort();
            _ = SaveBrowserSortModeSafelyAsync();
        }
    }

    public IReadOnlyCollection<Guid> SelectedTimelineItemIds => _selectedTimelineItemIds;

    public IReadOnlyList<ProjectTimelineItem> GetActivePositionableOverlayItems(TimeSpan position) =>
        _project.Tracks
            .Where(track => track.IsEnabled && track.Kind == ProjectTrackKind.Overlay)
            .OrderByDescending(track => track.Order)
            .SelectMany(track => track.Items)
            .Where(item => item.IsEnabled &&
                           item.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay or ProjectItemKind.VideoOverlay &&
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

    public string GetSelectedVideoProgressName()
    {
        var selected = _project.Tracks
            .SelectMany(track => track.Items)
            .Where(item => _selectedTimelineItemIds.Contains(item.Id) &&
                           item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
            .OrderBy(item => item.StartTicks)
            .ToList();
        return selected.Count switch
        {
            1 => $"PROGRESS {selected[0].Name}",
            > 1 => $"PROGRESS {selected.Count} CLIPS",
            _ => "PROGRESS"
        };
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                NotifyProjectIdentityChanged();
            }
        }
    }

    public bool CanUndo => _projectHistory.CanUndo;

    public bool CanRedo => _projectHistory.CanRedo;

    internal IReadOnlyList<ProjectHistoryNavigationEntry> UndoHistoryChoices =>
        _projectHistory.GetUndoChoices();

    internal IReadOnlyList<ProjectHistoryNavigationEntry> RedoHistoryChoices =>
        _projectHistory.GetRedoChoices();

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

    public void ReportStatus(string message) => StatusText = message;

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
        ContentBrowserViewMode.ExtraLargeGrid => "View: extra large",
        _ => "View: small"
    };

    public bool IsBrowserListView => _settings.BrowserViewMode == ContentBrowserViewMode.List;

    public bool IsBrowserGridView => !IsBrowserListView;

    public double BrowserItemWidth => _settings.BrowserViewMode switch
    {
        ContentBrowserViewMode.LargeGrid => _settings.LargeThumbnailSize + 8,
        ContentBrowserViewMode.ExtraLargeGrid => _settings.ExtraLargeThumbnailSize + 8,
        _ => _settings.SmallThumbnailSize + 8
    };

    public double BrowserItemHeight => _settings.BrowserViewMode switch
    {
        ContentBrowserViewMode.List => 76,
        ContentBrowserViewMode.LargeGrid => Math.Round(_settings.LargeThumbnailSize * 0.68) + 58,
        ContentBrowserViewMode.ExtraLargeGrid => Math.Round(_settings.ExtraLargeThumbnailSize * 0.68) + 58,
        _ => Math.Round(_settings.SmallThumbnailSize * 0.65) + 58
    };

    public double BrowserCardWidth => IsBrowserListView
        ? double.NaN
        : _settings.BrowserViewMode switch
        {
            ContentBrowserViewMode.LargeGrid => _settings.LargeThumbnailSize + 2,
            ContentBrowserViewMode.ExtraLargeGrid => _settings.ExtraLargeThumbnailSize + 2,
            _ => _settings.SmallThumbnailSize + 2
        };

    public double BrowserCardHeight => BrowserItemHeight - 6;

    public double BrowserThumbnailHeight => _settings.BrowserViewMode switch
    {
        ContentBrowserViewMode.LargeGrid => Math.Round(_settings.LargeThumbnailSize * 0.68),
        ContentBrowserViewMode.ExtraLargeGrid => Math.Round(_settings.ExtraLargeThumbnailSize * 0.68),
        _ => Math.Round(_settings.SmallThumbnailSize * 0.65)
    };

    public async Task CycleBrowserViewModeAsync(CancellationToken cancellationToken = default)
    {
        _settings.BrowserViewMode = _settings.BrowserViewMode switch
        {
            ContentBrowserViewMode.List => ContentBrowserViewMode.SmallGrid,
            ContentBrowserViewMode.SmallGrid => ContentBrowserViewMode.LargeGrid,
            ContentBrowserViewMode.LargeGrid => ContentBrowserViewMode.ExtraLargeGrid,
            _ => ContentBrowserViewMode.List
        };
        NotifyBrowserLayoutChanged();
        await _settingsStore.SaveAsync(_settings, cancellationToken);
        StatusText = $"Content Browser {_settings.BrowserViewMode} view";
    }

    private void ApplyMediaSort()
    {
        if (!MediaView.CanSort)
        {
            return;
        }

        using (MediaView.DeferRefresh())
        {
            MediaView.SortDescriptions.Clear();
            switch (_settings.BrowserSortMode)
            {
                case ContentBrowserSortMode.Date:
                    MediaView.SortDescriptions.Add(new SortDescription(
                        nameof(MediaCardViewModel.LastWriteUtc),
                        ListSortDirection.Descending));
                    break;
                case ContentBrowserSortMode.Length:
                    MediaView.SortDescriptions.Add(new SortDescription(
                        nameof(MediaCardViewModel.DurationTicks),
                        ListSortDirection.Ascending));
                    break;
                case ContentBrowserSortMode.Tag:
                    MediaView.SortDescriptions.Add(new SortDescription(
                        nameof(MediaCardViewModel.SortTags),
                        ListSortDirection.Ascending));
                    break;
            }

            MediaView.SortDescriptions.Add(new SortDescription(
                nameof(MediaCardViewModel.FileName),
                ListSortDirection.Ascending));
        }
    }

    private async Task SaveBrowserSortModeSafelyAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settings);
            StatusText = $"Content Browser sorted by {_settings.BrowserSortMode}";
        }
        catch (Exception exception)
        {
            StatusText = $"Could not save Content Browser sorting: {exception.Message}";
        }
    }

    public async Task InitializeAsync(
        IProgress<StartupProgress>? startupProgress = null,
        CancellationToken cancellationToken = default)
    {
        startupProgress?.Report(new StartupProgress(
            28,
            $"Loaded {_plugins.Plugins.Count} effect/source module(s) from the portable plugins folder.",
            "PLUGIN MODULES"));
        foreach (var diagnostic in _plugins.Diagnostics.Where(message =>
                     message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("not found", StringComparison.OrdinalIgnoreCase)))
        {
            startupProgress?.Report(new StartupProgress(29, diagnostic, "PLUGIN / WARNING"));
        }

        startupProgress?.Report(new StartupProgress(
            32,
            "Loading cached clip metadata and preview references…",
            "LIBRARY CATALOG"));
        await LoadCatalogAsync(cancellationToken);
        startupProgress?.Report(new StartupProgress(
            40,
            $"Catalog loaded: {MediaFiles.Count} clip record(s) available to the browser.",
            "LIBRARY CATALOG"));
        if (_settings.RescanLibraryOnStartup && _settings.SourceFolders.Count > 0)
        {
            startupProgress?.Report(new StartupProgress(
                42,
                $"Startup scan enabled: enumerating {_settings.SourceFolders.Count} configured source folder(s)…",
                "LIBRARY SCAN"));
            var scanProgress = new Progress<ScanProgress>(update =>
            {
                var scanPercent = update.Total == 0
                    ? 0
                    : Math.Clamp(update.Processed * 100d / update.Total, 0, 100);
                var percent = update.Total == 0 ? 80 : 44 + scanPercent * 0.36;
                var message = string.IsNullOrWhiteSpace(update.CurrentFile)
                    ? $"Finalizing library scan: {update.Added} added, {update.Updated} refreshed, " +
                      $"{update.Failed} failed."
                    : $"Scanning clip {update.Processed + 1:N0} of {update.Total:N0} " +
                      $"({scanPercent:0.0}%): {update.CurrentFile}";
                startupProgress?.Report(new StartupProgress(percent, message, "LIBRARY SCAN"));
            });
            var scanResult = await ScanAsync(false, scanProgress);
            startupProgress?.Report(new StartupProgress(
                81,
                $"Library scan complete: {scanResult.Discovered} discovered, {scanResult.Added} added, " +
                $"{scanResult.Updated} refreshed, {scanResult.Failed} failed.",
                "LIBRARY SCAN"));
        }
        else
        {
            var scanMessage = _settings.RescanLibraryOnStartup
                ? "Startup scan has no configured source folders; catalog scan skipped."
                : "Startup library scan is disabled in preferences; cached catalog retained.";
            startupProgress?.Report(new StartupProgress(45, scanMessage, "LIBRARY SCAN / SKIP"));
        }

        startupProgress?.Report(new StartupProgress(
            84,
            "Locating the startup project file and crash-recovery snapshot…",
            "PROJECT FILE"));
        var recovery = await _projectStore.LoadRecoveryAsync(cancellationToken);
        if (recovery is not null)
        {
            var projectLabel = string.IsNullOrWhiteSpace(recovery.ProjectFilePath)
                ? recovery.Name
                : Path.GetFileName(recovery.ProjectFilePath);
            startupProgress?.Report(new StartupProgress(
                87,
                $"Reading project state: {projectLabel}…",
                "PROJECT FILE"));
            if (await RestoreCleanRecoveryAsync(recovery, cancellationToken))
            {
                startupProgress?.Report(new StartupProgress(
                    92,
                    $"Project loaded cleanly: {ProjectName}.",
                    "PROJECT FILE"));
            }
            else
            {
                ApplyProject(recovery);
                _projectHistory.Reset(recovery, isSaved: false);
                NotifyHistoryStateChanged();
                IsDirty = true;
                StatusText = $"Recovered autosave: {recovery.Name}";
                startupProgress?.Report(new StartupProgress(
                    92,
                    $"Recovered unsaved project state: {recovery.Name}.",
                    "PROJECT RECOVERY"));
            }
        }
        else
        {
            startupProgress?.Report(new StartupProgress(
                92,
                "No startup project file is queued; prepared a clean Untitled project.",
                "PROJECT FILE / NEW"));
        }

        await UpdateProjectMediaReferencesAsync(cancellationToken);

        startupProgress?.Report(new StartupProgress(
            97,
            "Synchronizing timeline lanes, preview surfaces, and editor commands…",
            "EDITOR WORKSPACE"));
        startupProgress?.Report(new StartupProgress(100, "Editor ready.", "STARTUP COMPLETE"));
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
            NotifyProjectIdentityChanged();
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(OutputSettings));
            OnPropertyChanged(nameof(TargetDurationMinutes));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(ProjectCreatedUtc));
            OnPropertyChanged(nameof(ProjectSettingsSummary));
            _projectPreviewCoverage.Clear();
            RestoreCurrentFingerprintCoverage();
            RefreshProjectPreviewCoverageSegments();
            RefreshProjectLayers();
        }
        finally
        {
            _suppressProjectAutosave = false;
        }

        await _projectStore.ClearRecoveryAsync(cancellationToken);
        await SaveRecoveryNowAsync(cancellationToken);
        _projectHistory.Reset(_project, isSaved: true);
        NotifyHistoryStateChanged();
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
        _projectHistory.Reset(_project, isSaved: true);
        NotifyHistoryStateChanged();
        IsDirty = false;
        await RecordRecentProjectAsync(projectPath, cancellationToken);
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
        await UpdateProjectMediaReferencesAsync(cancellationToken);
        NotifyProjectIdentityChanged();
        OnPropertyChanged(nameof(ProjectFilePath));
        OnPropertyChanged(nameof(OutputSettings));
        OnPropertyChanged(nameof(ProjectSettingsSummary));
        _projectHistory.MarkSaved(_project);
        NotifyHistoryStateChanged();
        IsDirty = false;
        await RecordRecentProjectAsync(fullPath, cancellationToken);
        StatusText = $"Saved project: {_project.Name}";
    }

    private async Task RecordRecentProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(projectPath);
        _settings.RecentProjectPaths.RemoveAll(path =>
            path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        _settings.RecentProjectPaths.Insert(0, fullPath);
        if (_settings.RecentProjectPaths.Count > 10)
        {
            _settings.RecentProjectPaths.RemoveRange(10, _settings.RecentProjectPaths.Count - 10);
        }

        await _settingsStore.SaveAsync(_settings, cancellationToken);
        OnPropertyChanged(nameof(RecentProjectPaths));
    }

    public async Task SaveRecoveryNowAsync(CancellationToken cancellationToken = default)
    {
        SynchronizeProjectFromTimeline();
        await _projectStore.SaveRecoveryAsync(_project, cancellationToken);
        await UpdateProjectMediaReferencesAsync(cancellationToken);
    }

    private async Task UpdateProjectMediaReferencesAsync(CancellationToken cancellationToken)
    {
        var projectId = _project.Id;
        var mediaFileIds = _project.Tracks
            .SelectMany(track => track.Items)
            .Where(item => item.MediaFileId.HasValue)
            .Select(item => item.MediaFileId!.Value)
            .Distinct()
            .ToArray();
        await _catalogReferenceGate.WaitAsync(cancellationToken);
        try
        {
            await _catalog.ReplaceProjectMediaReferencesAsync(projectId, mediaFileIds, cancellationToken);
            var referenceCounts = await _catalog.GetProjectReferenceCountsAsync(cancellationToken);
            RefreshMediaUsageBadges(referenceCounts);
        }
        finally
        {
            _catalogReferenceGate.Release();
        }
    }

    private void RefreshMediaUsageBadges(IReadOnlyDictionary<long, int>? referenceCounts = null)
    {
        var currentMediaIds = _project.Tracks
            .SelectMany(track => track.Items)
            .Where(item => item.MediaFileId.HasValue)
            .Select(item => item.MediaFileId!.Value)
            .ToHashSet();
        foreach (var media in MediaFiles)
        {
            var count = referenceCounts?.GetValueOrDefault(media.Media.Id) ?? media.ProjectReferenceCount;
            media.UpdateProjectUsage(currentMediaIds.Contains(media.Media.Id), count);
        }
    }

    public async Task MarkMediaSeenAsync(
        IEnumerable<MediaCardViewModel> mediaFiles,
        CancellationToken cancellationToken = default)
    {
        foreach (var media in mediaFiles.Where(media => !media.IsSeen).DistinctBy(media => media.Media.Id))
        {
            await _catalog.MarkSeenAsync(media.Media.Id, cancellationToken);
            media.MarkSeen();
        }
    }

    public Task CompleteCleanSessionAsync(CancellationToken cancellationToken = default) =>
        _projectStore.ClearRecoveryAsync(cancellationToken);

    private async Task<bool> RestoreCleanRecoveryAsync(
        EditorProject recovery,
        CancellationToken cancellationToken)
    {
        EditorProject? savedProject = null;
        if (!string.IsNullOrWhiteSpace(recovery.ProjectFilePath) &&
            File.Exists(recovery.ProjectFilePath))
        {
            try
            {
                savedProject = await _projectStore.LoadAsync(recovery.ProjectFilePath, cancellationToken);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var cleanProject = savedProject is not null &&
                           ProjectContentComparer.EqualsIgnoringPersistenceMetadata(recovery, savedProject)
            ? savedProject
            : IsPristineUntitledProject(recovery) ? recovery : null;
        if (cleanProject is null)
        {
            return false;
        }

        ApplyProject(cleanProject);
        _projectHistory.Reset(cleanProject, isSaved: true);
        NotifyHistoryStateChanged();
        IsDirty = false;
        await _projectStore.ClearRecoveryAsync(cancellationToken);
        StatusText = savedProject is null
            ? "New project"
            : $"Restored last project: {savedProject.Name}";
        return true;
    }

    private static bool IsPristineUntitledProject(EditorProject project)
    {
        var expectedTracks = new[]
        {
            ("Overlays 1", ProjectTrackKind.Overlay, 0),
            ("Video 1", ProjectTrackKind.Video, 1),
            ("Progress 1", ProjectTrackKind.Progress, 2),
            ("Background", ProjectTrackKind.Background, 3),
            ("Audio 1", ProjectTrackKind.Audio, 4)
        };
        var output = project.Output;
        return project.Name.Equals("Untitled project", StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrWhiteSpace(project.ProjectFilePath) &&
               project.BackgroundColor == "#101010" &&
               Math.Abs(project.TargetDurationMinutes - 15) < 0.000001 &&
               project.TimelineRulerMode == TimelineRulerMode.TimeAndFrames &&
               project.TimelineSnapMode == TimelineSnapMode.TenthSecond &&
               output.PresetName == "YouTube 1080p" &&
               output.Width == 1920 && output.Height == 1080 &&
               Math.Abs(output.FramesPerSecond - 30) < 0.000001 &&
               output.VideoEncoder == VideoEncoderPreset.NativeMpeg4 &&
               output.QualityPercent == 80 && output.VideoBitrateKbps == 8000 &&
               output.AudioBitrateKbps == 192 &&
               project.Tracks.OrderBy(track => track.Order)
                   .Select(track => (track.Name, track.Kind, track.Order))
                   .SequenceEqual(expectedTracks) &&
               project.Tracks.All(track => track.Items.Count == 0 && track.IsEnabled &&
                                           !track.IsLocked && string.IsNullOrWhiteSpace(track.Color));
    }

    public async Task<bool> UndoAsync(
        int steps = 1,
        CancellationToken cancellationToken = default) =>
        await RestoreHistoryAsync(undo: true, steps, cancellationToken);

    public async Task<bool> RedoAsync(
        int steps = 1,
        CancellationToken cancellationToken = default) =>
        await RestoreHistoryAsync(undo: false, steps, cancellationToken);

    private async Task<bool> RestoreHistoryAsync(
        bool undo,
        int steps,
        CancellationToken cancellationToken)
    {
        CancelOverlayTransformEdit();
        var restored = undo ? _projectHistory.Undo(steps) : _projectHistory.Redo(steps);
        if (restored is null)
        {
            return false;
        }

        restored.ProjectFilePath = _project.ProjectFilePath;
        ApplyProject(restored);
        IsDirty = !_projectHistory.IsAtSavePoint;
        NotifyHistoryStateChanged();
        await SaveRecoveryNowAsync(cancellationToken);
        var stepDescription = steps == 1
            ? _projectHistory.LastActionDescription
            : $"{steps} actions through {_projectHistory.LastActionDescription}";
        RecordAction($"{(undo ? "Undo" : "Redo")}: {stepDescription}");
        StatusText = $"{(undo ? "Undid" : "Redid")} {stepDescription}";
        return true;
    }

    public async Task ApplySettingsAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _settingsStore.SaveAsync(settings, cancellationToken);
        _settings = settings.Copy();
        _projectHistory.SetMaximumEntries(_settings.UndoHistoryCapacity);
        while (ActionHistory.Count > _settings.UndoHistoryCapacity)
        {
            ActionHistory.RemoveAt(ActionHistory.Count - 1);
        }
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
        TimeSpan? rangeEnd = null,
        bool highQuality = false,
        string? operationDescription = null)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is already running.");
        }

        StatusText = $"Prerender: parsing project elements for {operationDescription ?? "the requested interval"}…";
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
        var previewDuration = outputRangeDuration ?? compositionDuration;
        var fingerprint = ProjectContentComparer.CreateContentFingerprint(_project);
        var previewQuality = highQuality ? 100 : _settings.PreviewQualityPercent;
        var preserveSelectedObjectQuality = !highQuality && _settings.PreserveSelectedPreviewObjectQuality;
        var selectedObjectId = preserveSelectedObjectQuality ? SelectedProjectLayer?.Item?.Id : null;
        var outputPath = Path.Combine(
            previewFolder,
            $"{_project.Id:N}-{fingerprint[..16]}-{(outputRangeStart ?? TimeSpan.Zero).Ticks}-" +
            $"{previewDuration.Ticks}-q{previewQuality}-{DateTime.UtcNow.Ticks}.mp4");
        var orientation = _project.Output.Height > _project.Output.Width
            ? OutputOrientation.Portrait
            : OutputOrientation.Landscape;
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        ScanProgress = 0;
        StatusText = $"Prerender: preparing video engine for {operationDescription ?? "the requested interval"}…";
        try
        {
            await _projectStore.SaveRecoveryAsync(_project, _operationCancellation.Token);
            var progress = new Progress<RenderProgress>(update =>
            {
                ScanProgress = update.Percent;
                var frame = Math.Max(0, (int)Math.Floor(
                    update.ProcessedDuration.TotalSeconds * _project.Output.FramesPerSecond));
                var totalFrames = Math.Max(1, (int)Math.Ceiling(
                    update.TotalDuration.TotalSeconds * _project.Output.FramesPerSecond));
                StatusText = $"Prerender: {update.Message} — frame {Math.Min(frame, totalFrames):N0}/{totalFrames:N0} " +
                             $"for {operationDescription ?? "the requested interval"}";
            });
            StatusText = $"Prerender: starting FFmpeg for {operationDescription ?? "the requested interval"}…";
            var result = await _videoRenderer.RenderAsync(
                CreateRenderRequest(
                    renderPlan,
                    outputPath,
                    orientation,
                    VideoEncoderPreset.WindowsMediaFoundationH264,
                    outputRangeStart,
                    outputRangeDuration,
                    previewScale: previewQuality / 100d,
                    preserveSelectedObjectQuality: preserveSelectedObjectQuality,
                    selectedObjectId: selectedObjectId),
                _settings.FfmpegPath,
                progress,
                _operationCancellation.Token);
            SynchronizeProjectFromTimeline();
            if (!fingerprint.Equals(
                    ProjectContentComparer.CreateContentFingerprint(_project),
                    StringComparison.Ordinal))
            {
                try
                {
                    File.Delete(outputPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                throw new InvalidOperationException(
                    "The timeline changed while this prerender was running. The outdated result was discarded.");
            }

            await SaveProjectPreviewCacheEntryAsync(
                previewFolder,
                outputPath,
                outputRangeStart ?? TimeSpan.Zero,
                result.Duration,
                previewQuality,
                preserveSelectedObjectQuality,
                selectedObjectId,
                fingerprint,
                _operationCancellation.Token);
            TrimProjectPreviewCache(previewFolder);
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

    internal async Task<IReadOnlyList<ProjectPreviewCacheEntry>> LoadProjectPreviewCacheEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        SynchronizeProjectFromTimeline();
        var previewFolder = Path.Combine(_settings.MetadataFolder, "project-previews");
        if (!Directory.Exists(previewFolder))
        {
            return [];
        }

        var fingerprint = ProjectContentComparer.CreateContentFingerprint(_project);
        var filePrefix = $"{_project.Id:N}-{fingerprint[..16]}-";
        ProjectPreviewCacheEntry? latestMetadata = null;
        try
        {
            var metadataPath = GetProjectPreviewMetadataPath(previewFolder);
            if (File.Exists(metadataPath))
            {
                await using var stream = File.OpenRead(metadataPath);
                latestMetadata = await JsonSerializer.DeserializeAsync<ProjectPreviewCacheEntry>(
                    stream,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            latestMetadata = null;
        }

        var entries = new List<ProjectPreviewCacheEntry>();
        foreach (var outputPath in Directory.EnumerateFiles(previewFolder, $"{filePrefix}*.mp4"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = ParseProjectPreviewCacheEntry(outputPath, filePrefix, fingerprint);
            if (entry is null)
            {
                continue;
            }

            if (latestMetadata is not null &&
                outputPath.Equals(latestMetadata.OutputPath, StringComparison.OrdinalIgnoreCase) &&
                latestMetadata.ProjectFingerprint.Equals(fingerprint, StringComparison.Ordinal))
            {
                entry.PreserveSelectedObjectQuality = latestMetadata.PreserveSelectedObjectQuality;
                entry.SelectedObjectId = latestMetadata.SelectedObjectId;
                entry.RenderedUtc = latestMetadata.RenderedUtc;
            }

            entries.Add(entry);
        }

        return entries
            .OrderBy(entry => entry.RangeStartTicks)
            .ThenBy(entry => entry.RenderedUtc)
            .ToList();
    }

    private async Task SaveProjectPreviewCacheEntryAsync(
        string previewFolder,
        string outputPath,
        TimeSpan rangeStart,
        TimeSpan duration,
        int previewQualityPercent,
        bool preserveSelectedObjectQuality,
        Guid? selectedObjectId,
        string projectFingerprint,
        CancellationToken cancellationToken)
    {
        var entry = new ProjectPreviewCacheEntry
        {
            ProjectFingerprint = projectFingerprint,
            OutputPath = outputPath,
            RangeStartTicks = rangeStart.Ticks,
            DurationTicks = duration.Ticks,
            PreviewQualityPercent = previewQualityPercent,
            PreserveSelectedObjectQuality = preserveSelectedObjectQuality,
            SelectedObjectId = selectedObjectId,
            RenderedUtc = DateTime.UtcNow
        };
        var metadataPath = GetProjectPreviewMetadataPath(previewFolder);
        var temporaryPath = $"{metadataPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, entry, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, metadataPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetProjectPreviewMetadataPath(string previewFolder) =>
        Path.Combine(previewFolder, $"{_project.Id:N}-preview.json");

    private static ProjectPreviewCacheEntry? ParseProjectPreviewCacheEntry(
        string outputPath,
        string filePrefix,
        string fingerprint)
    {
        var fileName = Path.GetFileNameWithoutExtension(outputPath);
        if (!fileName.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = fileName[filePrefix.Length..].Split('-');
        if (parts.Length != 4 ||
            !long.TryParse(parts[0], out var rangeStartTicks) ||
            !long.TryParse(parts[1], out var durationTicks) ||
            durationTicks <= 0 ||
            !parts[2].StartsWith('q') ||
            !int.TryParse(parts[2][1..], out var qualityPercent) ||
            !long.TryParse(parts[3], out var renderedTicks) ||
            renderedTicks < DateTime.MinValue.Ticks ||
            renderedTicks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        return new ProjectPreviewCacheEntry
        {
            ProjectFingerprint = fingerprint,
            OutputPath = outputPath,
            RangeStartTicks = rangeStartTicks,
            DurationTicks = durationTicks,
            PreviewQualityPercent = Math.Clamp(qualityPercent, 10, 100),
            RenderedUtc = new DateTime(renderedTicks, DateTimeKind.Utc)
        };
    }

    private void TrimProjectPreviewCache(string previewFolder)
    {
        const int maximumCachedChunksPerProject = 80;
        foreach (var oldPreview in Directory.EnumerateFiles(previewFolder, $"{_project.Id:N}-*.mp4")
                     .Select(path => new FileInfo(path))
                     .OrderByDescending(file => file.LastWriteTimeUtc)
                     .Skip(maximumCachedChunksPerProject))
        {
            try
            {
                oldPreview.Delete();
            }
            catch (IOException)
            {
                // MediaElement can retain an older chunk briefly; a later render will retry cleanup.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public async Task<RenderResult> RenderEffectFramePreviewAsync(
        Guid trackId,
        ProjectTimelineItem previewItem,
        TimeSpan frame,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is already running.");
        }

        SynchronizeProjectFromTimeline();
        progress?.Report(new RenderProgress(8, TimeSpan.Zero, TimeSpan.Zero, "Cloning project layers"));
        var previewProject = JsonSerializer.Deserialize<EditorProject>(JsonSerializer.Serialize(_project)) ??
                             throw new InvalidOperationException("The project could not be cloned for preview.");
        var track = previewProject.Tracks.FirstOrDefault(candidate => candidate.Id == trackId) ??
                    throw new InvalidOperationException("The effect timeline no longer exists.");
        var previewCandidate = JsonSerializer.Deserialize<ProjectTimelineItem>(JsonSerializer.Serialize(previewItem)) ??
                               throw new InvalidOperationException("The edited effect could not be cloned for preview.");
        if (previewCandidate.Kind == ProjectItemKind.TextOverlay)
        {
            // Effect-editor frame previews are for judging text appearance. A long fade can make the
            // selected frame technically accurate but uselessly invisible, so only this temporary
            // candidate bypasses its fades; timeline and final/project prerenders retain them.
            previewCandidate.FadeInSeconds = 0;
            previewCandidate.FadeOutSeconds = 0;
        }

        var existingIndex = track.Items.FindIndex(item => item.Id == previewItem.Id);
        if (existingIndex >= 0)
        {
            track.Items[existingIndex] = previewCandidate;
        }
        else
        {
            track.Items.Add(previewCandidate);
        }

        progress?.Report(new RenderProgress(14, TimeSpan.Zero, TimeSpan.Zero, "Mapping active effects and overlays"));
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
        progress?.Report(new RenderProgress(20, TimeSpan.Zero, duration, "Preparing frame render"));
        var outputPath = Path.Combine(previewFolder, $"{previewProject.Id:N}-{DateTime.UtcNow.Ticks}.mp4");
        var orientation = previewProject.Output.Height > previewProject.Output.Width
            ? OutputOrientation.Portrait
            : OutputOrientation.Landscape;
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsBusy = true;
        StatusText = $"Rendering effect frame at {DurationFormatter.Format(start)}…";
        try
        {
            progress?.Report(new RenderProgress(24, TimeSpan.Zero, duration, "Starting FFmpeg"));
            var result = await _videoRenderer.RenderAsync(
                CreateRenderRequest(
                    renderPlan,
                    outputPath,
                    orientation,
                    VideoEncoderPreset.WindowsMediaFoundationH264,
                    start,
                    duration,
                    previewProject,
                    previewScale: _settings.PreviewQualityPercent / 100d,
                    preserveSelectedObjectQuality: _settings.PreserveSelectedPreviewObjectQuality,
                    selectedObjectId: previewItem.Id),
                _settings.FfmpegPath,
                progress,
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
        MarkProjectPreviewRangesRendered(
            [(rangeStart ?? TimeSpan.Zero, rangeEnd ?? Timeline.Duration)]);
    }

    public void MarkProjectPreviewRangesRendered(IEnumerable<(TimeSpan Start, TimeSpan End)> ranges)
    {
        var renderedRanges = ranges.Where(range => range.End > range.Start).ToList();
        foreach (var (start, end) in renderedRanges)
        {
            _projectPreviewCoverage.MarkRendered(start, end);
        }

        SynchronizeProjectFromTimeline();
        var fingerprint = ProjectContentComparer.CreateContentFingerprint(_project);
        if (!_renderedCoverageByFingerprint.TryGetValue(fingerprint, out var rememberedRanges))
        {
            if (_renderedCoverageByFingerprint.Count >= 80)
            {
                _renderedCoverageByFingerprint.Remove(_renderedCoverageByFingerprint.Keys.First());
            }

            rememberedRanges = [];
            _renderedCoverageByFingerprint[fingerprint] = rememberedRanges;
        }

        foreach (var range in renderedRanges)
        {
            if (!rememberedRanges.Contains(range))
            {
                rememberedRanges.Add(range);
            }
        }

        RefreshProjectPreviewCoverageSegments();
        RefreshTimelineLanes();
    }

    private void InvalidateProjectPreviewRange(TimeSpan start, TimeSpan end)
    {
        if (end <= start)
        {
            return;
        }

        _projectPreviewCoverage.MarkStale(start, end);
        RestoreCurrentFingerprintCoverage();
        RefreshProjectPreviewCoverageSegments();
        RefreshTimelineLanes();
    }

    private void RestoreCurrentFingerprintCoverage()
    {
        SynchronizeProjectFromTimeline();
        var fingerprint = ProjectContentComparer.CreateContentFingerprint(_project);
        if (!_renderedCoverageByFingerprint.TryGetValue(fingerprint, out var ranges))
        {
            return;
        }

        foreach (var (start, end) in ranges)
        {
            _projectPreviewCoverage.MarkRendered(start, end);
        }
    }

    private void RefreshProjectPreviewCoverageSegments()
    {
        ProjectPreviewCoverageSegments.Clear();
        foreach (var coverage in _projectPreviewCoverage.Ranges)
        {
            ProjectPreviewCoverageSegments.Add(new ProjectPreviewCoverageSegmentViewModel(
                Math.Max(0, coverage.Start.TotalSeconds * Timeline.PixelsPerSecond),
                Math.Max(1, (coverage.End - coverage.Start).TotalSeconds * Timeline.PixelsPerSecond),
                coverage.NeedsRender));
        }
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
        MarkProjectDirty(item.Start, item.Start + item.Duration, actionDescription: $"Add {item.Name}");
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
        MarkProjectDirty(item.Start, item.Start + item.Duration, actionDescription: $"Edit {item.Name}");
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
        MarkProjectDirty(item.Start, item.Start + item.Duration, actionDescription: $"Add {item.Name}");
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
        MarkProjectDirty(item.Start, item.Start + item.Duration, actionDescription: $"Add {item.Name}");
        _selectedTimelineItemIds.Clear();
        _selectedTimelineItemIds.Add(item.Id);
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Added {item.Name} to {track.Name}";
    }

    public ProjectTimelineItem CreateProgressItem(TimeSpan start, TimeSpan duration, string name) => new()
    {
        Kind = ProjectItemKind.ProgressBar,
        Name = name,
        StartTicks = Math.Max(0, start.Ticks),
        DurationTicks = Math.Max(TimeSpan.FromSeconds(Timeline.SnapIncrement).Ticks, duration.Ticks),
        ProgressTimeMode = ProgressTimeMode.SourceSegment,
        ProgressBarStyle = _settings.DefaultProgressBarStyle,
        ProgressBarPosition = _settings.DefaultProgressBarPosition,
        ProgressColor = _settings.DefaultProgressColor,
        ProgressHeight = _settings.DefaultProgressHeight
    };

    public async Task RememberProgressDefaultsAsync(ProjectTimelineItem item)
    {
        if (item.Kind != ProjectItemKind.ProgressBar)
        {
            return;
        }

        _settings.DefaultProgressBarStyle = item.ProgressBarStyle;
        _settings.DefaultProgressBarPosition = item.ProgressBarPosition;
        _settings.DefaultProgressColor = item.ProgressColor;
        _settings.DefaultProgressHeight = item.ProgressHeight;
        await _settingsStore.SaveAsync(_settings);
    }

    public async Task SaveTextOverlayPresetAsync(
        TextOverlayPreset preset,
        CancellationToken cancellationToken = default)
    {
        var existingIndex = _settings.TextOverlayPresets.FindIndex(candidate =>
            candidate.Id == preset.Id || candidate.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            preset.Id = _settings.TextOverlayPresets[existingIndex].Id;
            _settings.TextOverlayPresets[existingIndex] = preset;
        }
        else
        {
            _settings.TextOverlayPresets.Add(preset);
        }

        await _settingsStore.SaveAsync(_settings, cancellationToken);
        StatusText = $"Saved text preset {preset.Name}";
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
        MarkProjectDirty(previewAffected: false, actionDescription: $"Add timeline {track.Name}");
        RefreshProjectLayers(trackId: track.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Added timeline: {track.Name}";
    }

    public bool RemoveSelectedTrack()
    {
        var row = SelectedProjectLayer;
        if (row is null || !row.IsTrackHeader || row.Track.Items.Count > 0 ||
            (row.Track.Kind != ProjectTrackKind.Effects &&
             _project.Tracks.Count(track => track.Kind == row.Track.Kind) <= 1))
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

        var primaryChanged = GetPrimaryVideoTrack().Id != previousPrimaryId;
        MarkProjectDirty(previewAffected: primaryChanged, actionDescription: $"Remove timeline {row.Track.Name}");
        if (primaryChanged)
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
        MarkProjectDirty(actionDescription: $"Move timeline {moving.Name}");
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
        MarkProjectDirty(previewAffected: false, actionDescription: $"Set color for {track.Name}");
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
        MarkProjectDirty(previewAffected: false, actionDescription: $"Set color for {entry.Item.Name}");
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

        var affectedStart = row.Item.Start;
        var affectedEnd = row.Item.Start + row.Item.Duration;
        row.Track.Items.RemoveAll(item => item.Id == row.Item.Id);
        MarkProjectDirty(affectedStart, affectedEnd, actionDescription: $"Remove {row.Item.Name}");
        RefreshProjectLayers();
        _ = SaveRecoverySafelyAsync();
        StatusText = "Layer item removed";
    }

    public void UpdateSelectedLayerItem(ProjectTimelineItem updatedItem)
    {
        var row = SelectedProjectLayer;
        if (row?.Item is null || row.Item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
        {
            return;
        }

        var index = row.Track.Items.FindIndex(item => item.Id == row.Item.Id);
        if (index < 0)
        {
            return;
        }

        var affectedStart = row.Item.Start < updatedItem.Start ? row.Item.Start : updatedItem.Start;
        var previousEnd = row.Item.Start + row.Item.Duration;
        var updatedEnd = updatedItem.Start + updatedItem.Duration;
        var affectedEnd = previousEnd > updatedEnd ? previousEnd : updatedEnd;
        updatedItem.Id = row.Item.Id;
        row.Track.Items[index] = updatedItem;
        MarkProjectDirty(affectedStart, affectedEnd, actionDescription: $"Edit {updatedItem.Name}");
        RefreshProjectLayers(updatedItem.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = "Layer item updated";
    }

    public bool SetTimelineItemEnabled(Guid itemId, bool enabled)
    {
        var entry = _project.Tracks
            .SelectMany(track => track.Items.Select(item => (Track: track, Item: item)))
            .FirstOrDefault(candidate => candidate.Item.Id == itemId);
        if (entry.Item is null || entry.Item.IsEnabled == enabled)
        {
            return false;
        }

        if (entry.Track.Id == GetPrimaryVideoTrack().Id &&
            entry.Item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
        {
            if (!Timeline.SetEnabled(itemId, enabled))
            {
                return false;
            }
        }
        else
        {
            entry.Item.IsEnabled = enabled;
            MarkProjectDirty(
                entry.Item.Start,
                entry.Item.Start + entry.Item.Duration,
                actionDescription: $"{(enabled ? "Enable" : "Disable")} {entry.Item.Name}");
            RefreshProjectLayers(itemId);
            _ = SaveRecoverySafelyAsync();
        }

        StatusText = $"{(enabled ? "Enabled" : "Disabled")} {entry.Item.Name}";
        return true;
    }

    public bool SetOverlayTransformLocked(Guid itemId, bool locked)
    {
        var item = FindPositionableOverlay(itemId);
        if (item is null || item.IsTransformLocked == locked)
        {
            return false;
        }

        if (_overlayTransformDraft?.ItemId == itemId)
        {
            CancelOverlayTransformEdit();
        }

        item.IsTransformLocked = locked;
        MarkProjectDirty(
            previewAffected: false,
            actionDescription: $"{(locked ? "Lock" : "Unlock")} transform for {item.Name}");
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"{(locked ? "Locked" : "Unlocked")} transform for {item.Name}";
        return true;
    }

    public bool BeginOverlayTransformEdit(Guid itemId)
    {
        if (_overlayTransformDraft?.ItemId == itemId)
        {
            return true;
        }

        CancelOverlayTransformEdit();
        var item = FindPositionableOverlay(itemId);
        if (item is null || item.IsTransformLocked)
        {
            return false;
        }

        _overlayTransformDraft = new OverlayTransformDraft(
            item.Id,
            item.Position,
            item.HasCustomOverlayTransform,
            item.OverlayX,
            item.OverlayY,
            item.OverlayScale,
            item.OverlayRotationDegrees);
        StatusText = $"Editing preview transform for {item.Name}";
        return true;
    }

    public bool PreviewOverlayTransform(
        Guid itemId,
        double x,
        double y,
        double scale,
        double rotationDegrees)
    {
        if (_overlayTransformDraft?.ItemId != itemId && !BeginOverlayTransformEdit(itemId))
        {
            return false;
        }

        var item = FindPositionableOverlay(itemId);
        if (item is null)
        {
            return false;
        }

        item.HasCustomOverlayTransform = true;
        item.OverlayX = OverlayTransformValues.NormalizeCoordinate(x);
        item.OverlayY = OverlayTransformValues.NormalizeCoordinate(y);
        item.OverlayScale = OverlayTransformValues.NormalizeScale(scale);
        item.OverlayRotationDegrees = OverlayTransformValues.NormalizeRotation(rotationDegrees);
        return true;
    }

    public bool CommitOverlayTransformEdit(Guid itemId)
    {
        var item = FindPositionableOverlay(itemId);
        var draft = _overlayTransformDraft;
        if (item is null || draft?.ItemId != itemId)
        {
            return false;
        }

        _overlayTransformDraft = null;
        var changed = item.HasCustomOverlayTransform != draft.HasCustomTransform ||
                      item.OverlayX != draft.X || item.OverlayY != draft.Y ||
                      item.OverlayScale != draft.Scale ||
                      item.OverlayRotationDegrees != draft.RotationDegrees;
        if (!changed)
        {
            StatusText = $"Preview transform unchanged for {item.Name}";
            return true;
        }

        MarkProjectDirty(item.Start, item.Start + item.Duration, actionDescription: $"Transform {item.Name}");
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Applied preview transform for {item.Name}";
        return true;
    }

    public bool CancelOverlayTransformEdit()
    {
        var draft = _overlayTransformDraft;
        _overlayTransformDraft = null;
        if (draft is null)
        {
            return false;
        }

        var item = FindPositionableOverlay(draft.ItemId);
        if (item is null)
        {
            return false;
        }

        item.HasCustomOverlayTransform = draft.HasCustomTransform;
        item.Position = draft.Position;
        item.OverlayX = draft.X;
        item.OverlayY = draft.Y;
        item.OverlayScale = draft.Scale;
        item.OverlayRotationDegrees = draft.RotationDegrees;
        RefreshProjectLayers(item.Id);
        StatusText = $"Cancelled preview transform for {item.Name}";
        return true;
    }

    private ProjectTimelineItem? FindPositionableOverlay(Guid itemId) => _project.Tracks
        .Where(track => track.Kind == ProjectTrackKind.Overlay)
        .SelectMany(track => track.Items)
        .FirstOrDefault(candidate => candidate.Id == itemId &&
                                     candidate.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay or
                                         ProjectItemKind.VideoOverlay);

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
        MarkProjectDirty(actionDescription: "Edit project settings");
        Timeline.SetTargetDuration(targetDurationMinutes);
        Timeline.SetFrameRate(settings.FramesPerSecond);
        NotifyProjectIdentityChanged();
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

        var selectedItems = _project.Tracks.SelectMany(track => track.Items)
            .Where(item => selected.Contains(item.Id))
            .ToList();
        if (selectedItems.Count == 0)
        {
            return;
        }

        var affectedStart = selectedItems.Min(item => item.Start);
        var affectedEnd = selectedItems.Any(item =>
            item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
            ? TimeSpan.MaxValue
            : selectedItems.Max(item => item.Start + item.Duration);

        var primary = _project.Tracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .OrderByDescending(track => track.Order)
            .First();
        var primarySourceIds = selected.Where(id => primary.Items.Any(item =>
            item.Id == id && item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)).ToHashSet();
        var primaryNonSourceIds = selected.Where(id => primary.Items.Any(item =>
            item.Id == id && item.Kind is not (ProjectItemKind.Video or ProjectItemKind.StillImage))).ToHashSet();
        _suppressProjectAutosave = true;
        try
        {
            if (primarySourceIds.Count > 0)
            {
                Timeline.Remove(primarySourceIds);
            }

            primary.Items.RemoveAll(item => primaryNonSourceIds.Contains(item.Id));

            foreach (var track in _project.Tracks.Where(track => track.Id != primary.Id))
            {
                track.Items.RemoveAll(item => selected.Contains(item.Id));
            }

            SynchronizeProjectFromTimeline();
        }
        finally
        {
            _suppressProjectAutosave = false;
        }

        _selectedTimelineItemIds.Clear();
        MarkProjectDirty(
            affectedStart,
            affectedEnd,
            actionDescription: selected.Count == 1 ? $"Remove {selectedItems[0].Name}" : $"Remove {selected.Count} items");
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
        var gridCandidate = Math.Round(seconds / increment) * increment;
        var candidates = new List<double> { gridCandidate };
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
            var clipCandidates = new List<double>();
            foreach (var boundary in GetPrimaryVideoTrack().Items
                         .Where(item => item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
                         .SelectMany(item => new[] { item.Start, item.Start + item.Duration })
                         .Append(TimeSpan.Zero)
                         .Distinct())
            {
                clipCandidates.Add(boundary.TotalSeconds);
                if (movingDuration > TimeSpan.Zero)
                {
                    clipCandidates.Add(boundary.TotalSeconds - movingDuration.Value.TotalSeconds);
                }
            }

            var clipThreshold = Math.Min(2, 12 / Math.Max(0.1, Timeline.PixelsPerSecond));
            var nearestClip = clipCandidates.Where(value => value >= 0)
                .OrderBy(value => Math.Abs(value - seconds))
                .FirstOrDefault(double.NaN);
            if (double.IsFinite(nearestClip) && Math.Abs(nearestClip - seconds) <= clipThreshold)
            {
                return TimeSpan.FromSeconds(nearestClip);
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
        var gridCandidate = Math.Round(seconds / increment) * increment;
        if (snapToClipRanges)
        {
            var clipCandidates = GetPrimaryVideoTrack().Items
                .Where(item => item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
                .SelectMany(item => new[] { item.Start.TotalSeconds, (item.Start + item.Duration).TotalSeconds })
                .Append(0)
                .Distinct()
                .ToList();
            var clipThreshold = Math.Min(2, 12 / Math.Max(0.1, Timeline.PixelsPerSecond));
            var nearestClip = clipCandidates.OrderBy(value => Math.Abs(value - seconds)).First();
            if (Math.Abs(nearestClip - seconds) <= clipThreshold)
            {
                return TimeSpan.FromSeconds(nearestClip);
            }
        }

        var threshold = Math.Max(increment / 2, 8 / Math.Max(0.1, Timeline.PixelsPerSecond));
        return TimeSpan.FromSeconds(Math.Abs(gridCandidate - seconds) <= threshold ? gridCandidate : seconds);
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
        if (target is null || sourceTracks.Count != 1)
        {
            return null;
        }

        var source = sourceTracks[0];
        var moving = source.Items.Where(item => itemIds.Contains(item.Id)).OrderBy(item => item.StartTicks).ToList();
        if (moving.Count == 0 || moving.Any(item => !IsTimelineItemCompatibleWithTrack(item, target)))
        {
            return null;
        }

        var selectionStart = moving[0].Start;
        var selectionEnd = moving.Max(item => item.Start + item.Duration);
        var selectionDuration = selectionEnd - selectionStart;
        if (source.Id == target.Id && target.Id == GetPrimaryVideoTrack().Id &&
            moving.All(item => item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage))
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
        if (sourceTracks.Count != 1)
        {
            return false;
        }

        var source = sourceTracks[0];
        var moving = source.Items.Where(item => itemIds.Contains(item.Id)).OrderBy(item => item.StartTicks).ToList();
        if (moving.Count == 0 || moving.Any(item => !IsTimelineItemCompatibleWithTrack(item, target)))
        {
            StatusText = $"Those items are not compatible with {target.Name}.";
            return false;
        }

        var primary = _project.Tracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .OrderByDescending(track => track.Order)
            .First();
        if (source.Id == target.Id && target.Id == primary.Id &&
            moving.All(item => item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage))
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

        var originalStart = moving[0].Start;
        var originalEnd = moving.Max(item => item.Start + item.Duration);
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

        if (source.Id != target.Id)
        {
            source.Items.RemoveAll(item => itemIds.Contains(item.Id));
            target.Items.AddRange(moving);
        }

        var movedStart = moving.Min(item => item.Start);
        var movedEnd = moving.Max(item => item.Start + item.Duration);
        MarkProjectDirty(
            originalStart < movedStart ? originalStart : movedStart,
            originalEnd > movedEnd ? originalEnd : movedEnd,
            actionDescription: source.Id == target.Id
                ? moving.Count == 1 ? $"Move {moving[0].Name}" : $"Move {moving.Count} items"
                : moving.Count == 1
                    ? $"Move {moving[0].Name} to {target.Name}"
                    : $"Move {moving.Count} items to {target.Name}");
        RefreshProjectLayers(moving[0].Id);
        _selectedTimelineItemIds.Clear();
        _selectedTimelineItemIds.UnionWith(itemIds);
        RefreshTimelineLanes();
        _ = SaveRecoverySafelyAsync();
        StatusText = source.Id == target.Id
            ? moving.Count == 1 ? "Timeline item moved" : $"Moved {moving.Count} timeline items"
            : moving.Count == 1
                ? $"Moved {moving[0].Name} to {target.Name}"
                : $"Moved {moving.Count} timeline items to {target.Name}";
        return true;
    }

    private bool IsTimelineItemCompatibleWithTrack(ProjectTimelineItem item, ProjectTrack track)
    {
        if (item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
        {
            return track.Kind == ProjectTrackKind.Video && track.Id == GetPrimaryVideoTrack().Id;
        }

        return item.Kind switch
        {
            ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay or ProjectItemKind.VideoOverlay =>
                track.Kind == ProjectTrackKind.Overlay,
            ProjectItemKind.Audio => track.Kind == ProjectTrackKind.Audio,
            ProjectItemKind.ProgressBar => track.Kind == ProjectTrackKind.Progress,
            ProjectItemKind.Effect => _plugins.Plugins.OfType<ICatClipVideoEffectPlugin>().Any(plugin =>
                plugin.Descriptor.Id.Equals(item.PluginId, StringComparison.OrdinalIgnoreCase) &&
                plugin.Descriptor.CompatibleTracks.Contains(track.Kind)),
            _ => false
        };
    }

    public bool ResizeTimelineItem(Guid itemId, TimeSpan start, TimeSpan duration)
    {
        var track = _project.Tracks.FirstOrDefault(candidate => candidate.Items.Any(item => item.Id == itemId));
        var item = track?.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (track is null || item is null ||
            item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
        {
            return false;
        }

        var previousStart = item.Start;
        var previousEnd = item.Start + item.Duration;
        var minimumDuration = TimeSpan.FromSeconds(Timeline.SnapIncrement);
        item.StartTicks = Math.Max(0, start.Ticks);
        item.DurationTicks = Math.Max(minimumDuration.Ticks, duration.Ticks);
        var currentEnd = item.Start + item.Duration;
        MarkProjectDirty(
            previousStart < item.Start ? previousStart : item.Start,
            previousEnd > currentEnd ? previousEnd : currentEnd,
            actionDescription: $"Resize {item.Name}");
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
        RefreshMediaUsageBadges();
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
            var previousSourceItems = GetPrimaryVideoTrack().Items
                .Where(item => item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
                .ToList();
            var updatedSourceItems = Timeline.CreateProjectItems().ToList();
            var affectedRange = GetChangedSourceRange(previousSourceItems, updatedSourceItems);
            SynchronizeProjectFromTimeline();
            MarkProjectDirty(
                affectedRange?.Start,
                affectedRange?.End,
                previewAffected: affectedRange.HasValue,
                actionDescription: "Edit video timeline");
            RefreshProjectLayers();
            await SaveRecoveryNowAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Recovery autosave failed: {exception.Message}";
        }
    }

    private static (TimeSpan Start, TimeSpan End)? GetChangedSourceRange(
        IReadOnlyList<ProjectTimelineItem> previous,
        IReadOnlyList<ProjectTimelineItem> updated)
    {
        var previousById = previous.ToDictionary(item => item.Id);
        var updatedById = updated.ToDictionary(item => item.Id);
        var changedItems = previous.Concat(updated)
            .Where(item =>
                !previousById.TryGetValue(item.Id, out var previousItem) ||
                !updatedById.TryGetValue(item.Id, out var updatedItem) ||
                !JsonSerializer.Serialize(previousItem).Equals(
                    JsonSerializer.Serialize(updatedItem),
                    StringComparison.Ordinal))
            .DistinctBy(item => (item.Id, item.StartTicks, item.DurationTicks))
            .ToList();
        if (changedItems.Count == 0)
        {
            return null;
        }

        return (
            changedItems.Min(item => item.Start),
            changedItems.Max(item => item.Start + item.Duration));
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
            MarkProjectDirty(previewAffected: false, actionDescription: "Change timeline display settings");
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
            NotifyProjectIdentityChanged();
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(OutputSettings));
            OnPropertyChanged(nameof(TargetDurationMinutes));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(ProjectCreatedUtc));
            OnPropertyChanged(nameof(ProjectSettingsSummary));
            _projectPreviewCoverage.Clear();
            RefreshProjectPreviewCoverageSegments();
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
        var nonSourceItems = videoTrack.Items
            .Where(item => item.Kind is not (ProjectItemKind.Video or ProjectItemKind.StillImage))
            .ToList();
        videoTrack.Items = Timeline.CreateProjectItems().Concat(nonSourceItems).ToList();
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
        RefreshMediaUsageBadges();
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
                .Select((item, stackingOrder) => new { Item = item, StackingOrder = stackingOrder })
                .OrderBy(entry => entry.Item.StartTicks)
                .Select(entry => new TimelineLaneItemViewModel(
                    track,
                    entry.Item,
                    clips.GetValueOrDefault(entry.Item.Id),
                    Timeline.PixelsPerSecond,
                    Timeline.TrackHeight,
                    _selectedTimelineItemIds.Contains(entry.Item.Id),
                    NeedsProjectPreview(entry.Item),
                    track.Id != primaryVideoTrackId,
                    entry.StackingOrder));
            TimelineLanes.Add(new TimelineLaneViewModel(track, kindOrdinals[track.Kind], items));
        }

        RefreshProjectPreviewCoverageSegments();
    }

    private bool NeedsProjectPreview(ProjectTimelineItem item) =>
        item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage &&
        !_projectPreviewCoverage.IsCurrent(item.Start, item.Start + item.Duration);

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

    private void MarkProjectDirty(
        TimeSpan? affectedStart = null,
        TimeSpan? affectedEnd = null,
        bool previewAffected = true,
        string actionDescription = "Project edit")
    {
        _project.ModifiedUtc = DateTime.UtcNow;
        if (_projectHistory.Capture(_project, actionDescription))
        {
            RecordAction(actionDescription);
        }
        NotifyHistoryStateChanged();
        IsDirty = !_projectHistory.IsAtSavePoint;
        if (previewAffected)
        {
            InvalidateProjectPreviewRange(
                affectedStart ?? TimeSpan.Zero,
                affectedEnd ?? TimeSpan.MaxValue);
        }
    }

    private void RecordAction(string description)
    {
        ActionHistory.Insert(0, new ActionHistoryEntryViewModel(DateTime.UtcNow, description));
        while (ActionHistory.Count > _settings.UndoHistoryCapacity)
        {
            ActionHistory.RemoveAt(ActionHistory.Count - 1);
        }
    }

    private void NotifyHistoryStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void NotifyProjectIdentityChanged()
    {
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectDisplayName));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private RenderRequest CreateRenderRequest(
        ProjectRenderPlan renderPlan,
        string outputPath,
        OutputOrientation orientation,
        VideoEncoderPreset? videoEncoderOverride = null,
        TimeSpan? outputRangeStart = null,
        TimeSpan? outputRangeDuration = null,
        EditorProject? sourceProject = null,
        double previewScale = 1,
        bool preserveSelectedObjectQuality = false,
        Guid? selectedObjectId = null)
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
            OutputRangeDuration: outputRangeDuration,
            PreviewScale: previewScale,
            PreserveSelectedObjectQuality: preserveSelectedObjectQuality,
            SelectedObjectId: selectedObjectId);
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

        foreach (var kind in Enum.GetValues<ProjectTrackKind>().Where(kind => kind != ProjectTrackKind.Effects))
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

    private sealed record OverlayTransformDraft(
        Guid ItemId,
        OverlayPosition Position,
        bool HasCustomTransform,
        double X,
        double Y,
        double Scale,
        double RotationDegrees);

}
