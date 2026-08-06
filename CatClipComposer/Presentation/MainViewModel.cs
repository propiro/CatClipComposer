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

    public MainViewModel(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        IMediaCatalog catalog,
        IMediaScanner scanner,
        ICompositionExporter compositionExporter)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _catalog = catalog;
        _scanner = scanner;
        _compositionExporter = compositionExporter;
        Timeline = new TimelineViewModel(settings.TargetDurationMinutes);
        MediaView = CollectionViewSource.GetDefaultView(MediaFiles);
        MediaView.Filter = FilterMedia;
    }

    public ObservableCollection<MediaCardViewModel> MediaFiles { get; } = [];

    public TimelineViewModel Timeline { get; }

    public ICollectionView MediaView { get; }

    public ApplicationSettings Settings => _settings;

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
        var segments = Timeline.CreateRenderSegments();

        try
        {
            var progress = new Progress<RenderProgress>(update =>
            {
                ScanProgress = update.Percent;
                StatusText = update.Message;
            });
            var result = await _compositionExporter.ExportAsync(
                new RenderRequest(
                    segments,
                    outputPath,
                    _settings.Orientation,
                    _settings.ProgressStyle,
                    _settings.OverlayImagePath,
                    _settings.OverlayText,
                    _settings.OverlayFontPath,
                    _settings.OverlayTextSize,
                    _settings.OverlayPosition,
                    _settings.VideoEncoder),
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

        Timeline.AddMedia(SelectedMedia.Media);
        StatusText = $"Added {SelectedMedia.FileName} to timeline";
    }

    public void AddStillImageToTimeline(string imagePath, TimeSpan duration)
    {
        Timeline.AddStillImage(imagePath, duration);
        StatusText = $"Added still screen: {Path.GetFileName(imagePath)}";
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
               media.FullPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

}
