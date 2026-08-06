using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IMediaCatalog _catalog;
    private readonly IMediaScanner _scanner;
    private readonly IVideoRenderer _videoRenderer;
    private CancellationTokenSource? _operationCancellation;
    private ApplicationSettings _settings;
    private MediaCardViewModel? _selectedMedia;
    private TimelineClipViewModel? _selectedTimelineClip;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";
    private double _scanProgress;
    private bool _isBusy;

    public MainViewModel(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        IMediaCatalog catalog,
        IMediaScanner scanner,
        IVideoRenderer videoRenderer)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _catalog = catalog;
        _scanner = scanner;
        _videoRenderer = videoRenderer;
        MediaView = CollectionViewSource.GetDefaultView(MediaFiles);
        MediaView.Filter = FilterMedia;
        Timeline.CollectionChanged += (_, _) => RefreshTimelineSummary();
    }

    public ObservableCollection<MediaCardViewModel> MediaFiles { get; } = [];

    public ObservableCollection<TimelineClipViewModel> Timeline { get; } = [];

    public ICollectionView MediaView { get; }

    public ApplicationSettings Settings => _settings;

    public MediaCardViewModel? SelectedMedia
    {
        get => _selectedMedia;
        set => SetProperty(ref _selectedMedia, value);
    }

    public TimelineClipViewModel? SelectedTimelineClip
    {
        get => _selectedTimelineClip;
        set => SetProperty(ref _selectedTimelineClip, value);
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

    public TimeSpan TimelineDuration => TimeSpan.FromTicks(Timeline.Sum(clip => clip.Duration.Ticks));

    public TimeSpan TargetDuration => TimeSpan.FromMinutes(_settings.TargetDurationMinutes);

    public string TimelineTotalText => $"{DurationFormatter.Format(TimelineDuration)} total";

    public string TargetDurationText => $"Target {DurationFormatter.Format(TargetDuration)}";

    public string TimelineRemainingText
    {
        get
        {
            var remaining = TargetDuration - TimelineDuration;
            return remaining >= TimeSpan.Zero
                ? $"{DurationFormatter.Format(remaining)} remaining"
                : $"{DurationFormatter.Format(remaining.Duration())} over target";
        }
    }

    public double TimelineProgress => TargetDuration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(TimelineDuration.TotalMilliseconds / TargetDuration.TotalMilliseconds * 100, 0, 100);

    public string AxisStartText => "0:00";

    public string AxisQuarterText => DurationFormatter.Format(TimeSpan.FromTicks(TargetDuration.Ticks / 4));

    public string AxisHalfText => DurationFormatter.Format(TimeSpan.FromTicks(TargetDuration.Ticks / 2));

    public string AxisThreeQuarterText => DurationFormatter.Format(TimeSpan.FromTicks(TargetDuration.Ticks * 3 / 4));

    public string AxisEndText => DurationFormatter.Format(TargetDuration);

    public async Task InitializeAsync(CancellationToken cancellationToken = default) =>
        await LoadCatalogAsync(cancellationToken);

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
        RefreshTimelineSummary();
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

        if (Timeline.Count == 0)
        {
            throw new InvalidOperationException("Add at least one clip to the timeline before exporting.");
        }

        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        ScanProgress = 0;
        StatusText = "Preparing compilation…";
        var segments = Timeline.Select(item => item.ToRenderSegment()).ToList();

        try
        {
            var progress = new Progress<RenderProgress>(update =>
            {
                ScanProgress = update.Percent;
                StatusText = update.Message;
            });
            var result = await _videoRenderer.RenderAsync(
                new RenderRequest(
                    segments,
                    outputPath,
                    _settings.Orientation,
                    _settings.ProgressStyle,
                    _settings.OverlayImagePath,
                    _settings.OverlayText,
                    _settings.OverlayFontPath,
                    _settings.OverlayTextSize,
                    _settings.OverlayPosition),
                _settings.FfmpegPath,
                progress,
                _operationCancellation.Token);
            await _catalog.RecordExportAsync(
                result.OutputPath,
                result.Duration,
                segments
                    .Where(segment => segment.MediaFileId.HasValue)
                    .Select(segment => segment.MediaFileId!.Value)
                    .ToList(),
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

        var clip = TimelineClipViewModel.FromMedia(SelectedMedia.Media, Timeline.Count + 1);
        Timeline.Add(clip);
        SelectedTimelineClip = clip;
        StatusText = $"Added {SelectedMedia.FileName} to timeline";
    }

    public void AddStillImageToTimeline(string imagePath, TimeSpan duration)
    {
        var insertIndex = SelectedTimelineClip is null
            ? Timeline.Count
            : Timeline.IndexOf(SelectedTimelineClip) + 1;
        var screen = TimelineClipViewModel.FromStillImage(imagePath, duration, insertIndex + 1);
        Timeline.Insert(insertIndex, screen);
        SelectedTimelineClip = screen;
        ReindexTimeline();
        StatusText = $"Added still screen: {Path.GetFileName(imagePath)}";
    }

    public void MoveSelectedTimelineClip(int offset)
    {
        if (SelectedTimelineClip is null)
        {
            return;
        }

        var oldIndex = Timeline.IndexOf(SelectedTimelineClip);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Timeline.Count)
        {
            return;
        }

        Timeline.Move(oldIndex, newIndex);
        ReindexTimeline();
    }

    public void RemoveSelectedTimelineClip()
    {
        if (SelectedTimelineClip is null)
        {
            return;
        }

        var oldIndex = Timeline.IndexOf(SelectedTimelineClip);
        Timeline.Remove(SelectedTimelineClip);
        SelectedTimelineClip = Timeline.Count == 0
            ? null
            : Timeline[Math.Clamp(oldIndex, 0, Timeline.Count - 1)];
        ReindexTimeline();
    }

    public void ClearTimeline()
    {
        Timeline.Clear();
        SelectedTimelineClip = null;
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
               media.FullPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void ReindexTimeline()
    {
        for (var index = 0; index < Timeline.Count; index++)
        {
            Timeline[index].Order = index + 1;
        }

        RefreshTimelineSummary();
    }

    private void RefreshTimelineSummary()
    {
        OnPropertyChanged(nameof(TimelineDuration));
        OnPropertyChanged(nameof(TargetDuration));
        OnPropertyChanged(nameof(TimelineTotalText));
        OnPropertyChanged(nameof(TargetDurationText));
        OnPropertyChanged(nameof(TimelineRemainingText));
        OnPropertyChanged(nameof(TimelineProgress));
        OnPropertyChanged(nameof(AxisQuarterText));
        OnPropertyChanged(nameof(AxisHalfText));
        OnPropertyChanged(nameof(AxisThreeQuarterText));
        OnPropertyChanged(nameof(AxisEndText));
    }
}
