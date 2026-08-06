using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Presentation;

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IProjectStore _projectStore;
    private readonly IMediaCatalog _catalog;
    private readonly IMediaScanner _scanner;
    private readonly ICompositionExporter _compositionExporter;
    private CancellationTokenSource? _operationCancellation;
    private ApplicationSettings _settings;
    private MediaCardViewModel? _selectedMedia;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";
    private double _scanProgress;
    private bool _isBusy;
    private bool _suppressProjectAutosave;
    private EditorProject _project;
    private ProjectLayerRowViewModel? _selectedProjectLayer;

    public MainViewModel(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        IProjectStore projectStore,
        IMediaCatalog catalog,
        IMediaScanner scanner,
        ICompositionExporter compositionExporter)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _projectStore = projectStore;
        _catalog = catalog;
        _scanner = scanner;
        _compositionExporter = compositionExporter;
        Timeline = new TimelineViewModel(settings.TargetDurationMinutes);
        _project = EditorProject.Create("Untitled project", CreateOutputSettings(settings));
        Timeline.Changed += Timeline_Changed;
        MediaView = CollectionViewSource.GetDefaultView(MediaFiles);
        MediaView.Filter = FilterMedia;
        RefreshProjectLayers();
    }

    public ObservableCollection<MediaCardViewModel> MediaFiles { get; } = [];

    public ObservableCollection<ProjectLayerRowViewModel> ProjectLayers { get; } = [];

    public TimelineViewModel Timeline { get; }

    public ICollectionView MediaView { get; }

    public ApplicationSettings Settings => _settings;

    public string ProjectName => _project.Name;

    public string? ProjectFilePath => _project.ProjectFilePath;

    public ProjectOutputSettings OutputSettings => _project.Output;

    public ProjectLayerRowViewModel? SelectedProjectLayer
    {
        get => _selectedProjectLayer;
        set => SetProperty(ref _selectedProjectLayer, value);
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

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await LoadCatalogAsync(cancellationToken);
        var recovery = await _projectStore.LoadRecoveryAsync(cancellationToken);
        if (recovery is not null)
        {
            ApplyProject(recovery);
            StatusText = $"Recovered autosave: {recovery.Name}";
        }
    }

    public async Task NewProjectAsync(CancellationToken cancellationToken = default)
    {
        _suppressProjectAutosave = true;
        try
        {
            Timeline.Clear();
            _project = EditorProject.Create("Untitled project", CreateOutputSettings(_settings));
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(OutputSettings));
            RefreshProjectLayers();
        }
        finally
        {
            _suppressProjectAutosave = false;
        }

        await _projectStore.ClearRecoveryAsync(cancellationToken);
        await SaveRecoveryNowAsync(cancellationToken);
        StatusText = "New project";
    }

    public async Task OpenProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(projectPath, cancellationToken);
        ApplyProject(project);
        await SaveRecoveryNowAsync(cancellationToken);
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
        Timeline.SetTargetDuration(_settings.TargetDurationMinutes);
        StatusText = "Options saved";
    }

    public async Task<ScanResult> ScanAsync()
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
                ScanProgress = update.Total == 0
                    ? 0
                    : update.Processed * 100d / update.Total;
                StatusText = string.IsNullOrWhiteSpace(update.CurrentFile)
                    ? "Finishing catalog update…"
                    : $"Scanning {update.Processed + 1} of {update.Total}: {update.CurrentFile}";
            });
            var result = await _scanner.ScanAsync(
                _settings,
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
            var renderPlan = ProjectRenderMapper.Create(_project);
            var orientation = _project.Output.Height > _project.Output.Width
                ? OutputOrientation.Portrait
                : OutputOrientation.Landscape;
            var progress = new Progress<RenderProgress>(update =>
            {
                ScanProgress = update.Percent;
                StatusText = update.Message;
            });
            var result = await _compositionExporter.ExportAsync(
                new RenderRequest(
                    renderPlan.Segments,
                    outputPath,
                    orientation,
                    _settings.ProgressStyle,
                    _settings.OverlayImagePath,
                    _settings.OverlayText,
                    _settings.OverlayFontPath,
                    _settings.OverlayTextSize,
                    _settings.OverlayPosition,
                    _project.Output.VideoEncoder,
                    _project.Output.FramesPerSecond,
                    ProjectName: _project.Name,
                    ProjectFilePath: _project.ProjectFilePath,
                    OutputWidth: _project.Output.Width,
                    OutputHeight: _project.Output.Height,
                    QualityPercent: _project.Output.QualityPercent,
                    VideoBitrateKbps: _project.Output.VideoBitrateKbps,
                    AudioBitrateKbps: _project.Output.AudioBitrateKbps,
                    TimedOverlays: renderPlan.TimedOverlays,
                    AudioLayers: renderPlan.AudioLayers),
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

    public void UpdateSelectedClipEffects(
        VideoFitMode fitMode,
        double fadeInSeconds,
        double fadeOutSeconds,
        double volume)
    {
        Timeline.UpdateSelectedEffects(fitMode, fadeInSeconds, fadeOutSeconds, volume);
        StatusText = "Clip effects updated";
    }

    public void AddLayerItem(ProjectTrackKind trackKind, ProjectTimelineItem item)
    {
        EnsureProjectTracks(_project);
        var track = _project.Tracks.Single(candidate => candidate.Kind == trackKind);
        item.StartTicks = Math.Max(0, item.StartTicks);
        item.DurationTicks = Math.Max(TimeSpan.FromMilliseconds(100).Ticks, item.DurationTicks);
        track.Items.Add(item);
        _project.ModifiedUtc = DateTime.UtcNow;
        RefreshProjectLayers(item.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Added {item.Name} to {track.Name}";
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
            }
            return;
        }

        row.Track.Items.RemoveAll(item => item.Id == row.Item.Id);
        _project.ModifiedUtc = DateTime.UtcNow;
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
        _project.ModifiedUtc = DateTime.UtcNow;
        RefreshProjectLayers(updatedItem.Id);
        _ = SaveRecoverySafelyAsync();
        StatusText = "Layer item updated";
    }

    public void ApplyOutputSettings(ProjectOutputSettings settings)
    {
        _project.Output = settings;
        _project.ModifiedUtc = DateTime.UtcNow;
        OnPropertyChanged(nameof(OutputSettings));
        _ = SaveRecoverySafelyAsync();
        StatusText = $"Output preset: {settings.PresetName}";
    }

    public void MoveSelectedTimelineClip(int offset)
    {
        Timeline.MoveSelected(offset);
    }

    public void RemoveSelectedTimelineClip()
    {
        Timeline.RemoveSelected();
    }

    public void ClearTimeline()
    {
        Timeline.Clear();
        StatusText = "Timeline cleared";
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
            SynchronizeProjectFromTimeline();
            RefreshProjectLayers();
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
            .Single(track => track.Kind == ProjectTrackKind.Video)
            .Items;

        _suppressProjectAutosave = true;
        try
        {
            _project = project;
            Timeline.ReplaceProjectItems(videoItems, mediaById, mediaByPath);
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(OutputSettings));
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
        var videoTrack = _project.Tracks.Single(track => track.Kind == ProjectTrackKind.Video);
        videoTrack.Items = Timeline.CreateProjectItems().ToList();
        _project.ModifiedUtc = DateTime.UtcNow;
    }

    private void RefreshProjectLayers(Guid? selectedItemId = null)
    {
        EnsureProjectTracks(_project);
        selectedItemId ??= SelectedProjectLayer?.Item?.Id;
        ProjectLayers.Clear();
        foreach (var track in _project.Tracks.OrderBy(track => track.Order))
        {
            ProjectLayers.Add(ProjectLayerRowViewModel.ForTrack(track));
            foreach (var item in track.Items.OrderBy(item => item.StartTicks))
            {
                ProjectLayers.Add(ProjectLayerRowViewModel.ForItem(track, item));
            }
        }

        SelectedProjectLayer = selectedItemId.HasValue
            ? ProjectLayers.FirstOrDefault(row => row.Item?.Id == selectedItemId.Value)
            : ProjectLayers.FirstOrDefault();
    }

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

    private static void EnsureProjectTracks(EditorProject project)
    {
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

    private static ProjectOutputSettings CreateOutputSettings(ApplicationSettings settings)
    {
        var (width, height) = settings.Orientation == OutputOrientation.Portrait
            ? (1080, 1920)
            : (1920, 1080);
        return new ProjectOutputSettings
        {
            Width = width,
            Height = height,
            VideoEncoder = settings.VideoEncoder
        };
    }

}
