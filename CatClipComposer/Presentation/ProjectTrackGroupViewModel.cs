using System.Collections.ObjectModel;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Presentation;

public sealed class ProjectTrackGroupViewModel : ObservableObject
{
    private readonly Action<Guid, bool> _expansionChanged;
    private bool _isExpanded;

    public ProjectTrackGroupViewModel(
        ProjectTrack track,
        IEnumerable<ProjectLayerRowViewModel> items,
        bool isExpanded,
        Action<Guid, bool> expansionChanged)
    {
        Track = track;
        Header = ProjectLayerRowViewModel.ForTrack(track);
        Items = new ObservableCollection<ProjectLayerRowViewModel>(items);
        _isExpanded = isExpanded;
        _expansionChanged = expansionChanged;
    }

    public ProjectTrack Track { get; }

    public ProjectLayerRowViewModel Header { get; }

    public ObservableCollection<ProjectLayerRowViewModel> Items { get; }

    public string HeaderColor => string.IsNullOrWhiteSpace(Track.Color) ? "#242421" : Track.Color;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                _expansionChanged(Track.Id, value);
            }
        }
    }
}
