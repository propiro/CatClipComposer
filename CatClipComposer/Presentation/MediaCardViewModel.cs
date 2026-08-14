using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class MediaCardViewModel : ObservableObject
{
    private int _index;
    private bool _showFileName;
    private bool _isSeen;
    private bool _isUsedInCurrentProject;
    private int _projectReferenceCount;

    public MediaCardViewModel(MediaFile media, int index, bool showFileName)
    {
        Media = media;
        _index = index;
        _showFileName = showFileName;
        _isSeen = media.IsSeen;
        _projectReferenceCount = media.ProjectReferenceCount;
    }

    public MediaFile Media { get; }

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public bool ShowFileName
    {
        get => _showFileName;
        set => SetProperty(ref _showFileName, value);
    }

    public string FileName => Media.FileName;

    public long DurationTicks => Media.DurationTicks;

    public DateTime LastWriteUtc => Media.LastWriteUtc;

    public string SortTags => string.IsNullOrWhiteSpace(Media.Tags) ? "\uffff" : Media.Tags;

    public string FullPath => Media.FullPath;

    public string DurationText => DurationFormatter.Format(Media.Duration);

    public string DimensionsText => Media.Width > 0 && Media.Height > 0
        ? $"{Media.Width} × {Media.Height}"
        : "Unknown size";

    public string FileSizeText => Media.FileSize switch
    {
        >= 1_073_741_824 => $"{Media.FileSize / 1_073_741_824d:0.##} GB",
        >= 1_048_576 => $"{Media.FileSize / 1_048_576d:0.##} MB",
        >= 1024 => $"{Media.FileSize / 1024d:0.##} KB",
        _ => $"{Media.FileSize:N0} bytes"
    };

    public string TechnicalSummary =>
        $"{DurationText}  |  {DimensionsText}  |  {FileSizeText}  |  " +
        $"{(Media.HasAudio ? "audio present" : "no audio")}  |  {Media.Extension}";

    public string CatalogSummary =>
        $"Discovered {Media.DiscoveredUtc.ToLocalTime():g}  |  last scanned {Media.LastScannedUtc.ToLocalTime():g}  |  " +
        $"{ProjectReferenceCount} saved/recovered project reference(s)  |  {UsageText}";

    public string UsageText => Media.UseCount switch
    {
        0 => "Not used yet",
        1 => "Used once",
        _ => $"Used {Media.UseCount} times"
    };

    public string? ThumbnailPath => Media.ThumbnailPath;

    public string? PreviewSheetPath => Media.PreviewSheetPath;

    public string TagsText => string.IsNullOrWhiteSpace(Media.Tags)
        ? "No tags"
        : Media.Tags;

    public bool HasCornerBadge => IsUsedInCurrentProject || ProjectReferenceCount > 0 || !IsSeen;

    public string CornerBadgeColor => IsUsedInCurrentProject
        ? "#52C878"
        : ProjectReferenceCount > 0
            ? "#E2BD43"
            : "#4B98E8";

    public string CornerBadgeToolTip => IsUsedInCurrentProject
        ? "Used in the current project"
        : ProjectReferenceCount > 0
            ? $"Used in {ProjectReferenceCount} saved or recovered project(s)"
            : "New clip — preview it or choose Mark as seen";

    public bool IsSeen
    {
        get => _isSeen;
        private set
        {
            if (SetProperty(ref _isSeen, value))
            {
                NotifyBadgeChanged();
            }
        }
    }

    public bool IsUsedInCurrentProject
    {
        get => _isUsedInCurrentProject;
        private set
        {
            if (SetProperty(ref _isUsedInCurrentProject, value))
            {
                NotifyBadgeChanged();
            }
        }
    }

    public int ProjectReferenceCount
    {
        get => _projectReferenceCount;
        private set
        {
            if (SetProperty(ref _projectReferenceCount, Math.Max(0, value)))
            {
                NotifyBadgeChanged();
            }
        }
    }

    public void MarkSeen() => IsSeen = true;

    public void UpdateProjectUsage(bool isUsedInCurrentProject, int projectReferenceCount)
    {
        IsUsedInCurrentProject = isUsedInCurrentProject;
        ProjectReferenceCount = projectReferenceCount;
    }

    private void NotifyBadgeChanged()
    {
        OnPropertyChanged(nameof(HasCornerBadge));
        OnPropertyChanged(nameof(CornerBadgeColor));
        OnPropertyChanged(nameof(CornerBadgeToolTip));
    }
}
