using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class MediaCardViewModel : ObservableObject
{
    private int _index;
    private bool _showFileName;

    public MediaCardViewModel(MediaFile media, int index, bool showFileName)
    {
        Media = media;
        _index = index;
        _showFileName = showFileName;
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

    public string FullPath => Media.FullPath;

    public string DurationText => DurationFormatter.Format(Media.Duration);

    public string DimensionsText => Media.Width > 0 && Media.Height > 0
        ? $"{Media.Width} × {Media.Height}"
        : "Unknown size";

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
}
