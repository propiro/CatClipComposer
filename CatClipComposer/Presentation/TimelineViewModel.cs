using System.Collections.ObjectModel;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class TimelineViewModel : ObservableObject
{
    private readonly ObservableCollection<TimelineClipViewModel> _clips = [];
    private TimelineClipViewModel? _selectedClip;
    private TimeSpan _targetDuration;
    private bool _suppressChanged;

    public TimelineViewModel(double targetDurationMinutes)
    {
        Clips = new ReadOnlyObservableCollection<TimelineClipViewModel>(_clips);
        _targetDuration = TimeSpan.FromMinutes(targetDurationMinutes);
        _clips.CollectionChanged += (_, _) =>
        {
            RefreshSummary();
            if (!_suppressChanged)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? Changed;

    public ReadOnlyObservableCollection<TimelineClipViewModel> Clips { get; }

    public TimelineClipViewModel? SelectedClip
    {
        get => _selectedClip;
        set => SetProperty(ref _selectedClip, value);
    }

    public TimeSpan Duration => TimeSpan.FromTicks(_clips.Sum(clip => clip.Duration.Ticks));

    public TimeSpan TargetDuration => _targetDuration;

    public string TotalText => $"{DurationFormatter.Format(Duration)} total";

    public string TargetText => $"Target {DurationFormatter.Format(TargetDuration)}";

    public string RemainingText
    {
        get
        {
            var remaining = TargetDuration - Duration;
            return remaining >= TimeSpan.Zero
                ? $"{DurationFormatter.Format(remaining)} remaining"
                : $"{DurationFormatter.Format(remaining.Duration())} over target";
        }
    }

    public double Progress => TargetDuration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(Duration.TotalMilliseconds / TargetDuration.TotalMilliseconds * 100, 0, 100);

    public string AxisStartText => "0:00";

    public string AxisQuarterText => DurationFormatter.Format(TimeSpan.FromTicks(TargetDuration.Ticks / 4));

    public string AxisHalfText => DurationFormatter.Format(TimeSpan.FromTicks(TargetDuration.Ticks / 2));

    public string AxisThreeQuarterText => DurationFormatter.Format(TimeSpan.FromTicks(TargetDuration.Ticks * 3 / 4));

    public string AxisEndText => DurationFormatter.Format(TargetDuration);

    public TimelineClipViewModel AddMedia(MediaFile media)
    {
        var clip = TimelineClipViewModel.FromMedia(media, _clips.Count + 1);
        _clips.Add(clip);
        SelectedClip = clip;
        return clip;
    }

    public TimelineClipViewModel AddStillImage(string imagePath, TimeSpan duration)
    {
        var insertIndex = SelectedClip is null
            ? _clips.Count
            : _clips.IndexOf(SelectedClip) + 1;
        var screen = TimelineClipViewModel.FromStillImage(imagePath, duration, insertIndex + 1);
        _clips.Insert(insertIndex, screen);
        SelectedClip = screen;
        Reindex();
        return screen;
    }

    public bool MoveSelected(int offset)
    {
        if (SelectedClip is null)
        {
            return false;
        }

        var oldIndex = _clips.IndexOf(SelectedClip);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= _clips.Count)
        {
            return false;
        }

        _clips.Move(oldIndex, newIndex);
        Reindex();
        return true;
    }

    public bool RemoveSelected()
    {
        if (SelectedClip is null)
        {
            return false;
        }

        var oldIndex = _clips.IndexOf(SelectedClip);
        _clips.Remove(SelectedClip);
        SelectedClip = _clips.Count == 0
            ? null
            : _clips[Math.Clamp(oldIndex, 0, _clips.Count - 1)];
        Reindex();
        return true;
    }

    public bool Select(Guid instanceId)
    {
        var clip = _clips.FirstOrDefault(item => item.InstanceId == instanceId);
        if (clip is null)
        {
            return false;
        }

        SelectedClip = clip;
        return true;
    }

    public void Clear()
    {
        _clips.Clear();
        SelectedClip = null;
    }

    public void SetTargetDuration(double minutes)
    {
        _targetDuration = TimeSpan.FromMinutes(minutes);
        RefreshSummary();
    }

    public void UpdateSelectedEffects(
        VideoFitMode fitMode,
        double fadeInSeconds,
        double fadeOutSeconds,
        double volume)
    {
        if (SelectedClip is null)
        {
            return;
        }

        SelectedClip.UpdateEffects(fitMode, fadeInSeconds, fadeOutSeconds, volume);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<RenderSegment> CreateRenderSegments() =>
        _clips.Select(item => item.ToRenderSegment()).ToList();

    public IReadOnlyList<ProjectTimelineItem> CreateProjectItems()
    {
        var start = TimeSpan.Zero;
        var items = new List<ProjectTimelineItem>(_clips.Count);
        foreach (var clip in _clips)
        {
            items.Add(clip.ToProjectItem(start));
            start += clip.Duration;
        }

        return items;
    }

    public void ReplaceProjectItems(
        IEnumerable<ProjectTimelineItem> items,
        IReadOnlyDictionary<long, MediaFile> mediaById,
        IReadOnlyDictionary<string, MediaFile> mediaByPath)
    {
        _suppressChanged = true;
        try
        {
            _clips.Clear();
            foreach (var item in items
                         .Where(item => item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
                         .OrderBy(item => item.StartTicks))
            {
                MediaFile? media = null;
                if (item.MediaFileId.HasValue)
                {
                    mediaById.TryGetValue(item.MediaFileId.Value, out media);
                }

                if (media is null && !string.IsNullOrWhiteSpace(item.SourcePath))
                {
                    mediaByPath.TryGetValue(item.SourcePath, out media);
                }

                _clips.Add(TimelineClipViewModel.FromProjectItem(
                    item,
                    media,
                    _clips.Count + 1));
            }

            SelectedClip = _clips.FirstOrDefault();
            Reindex();
        }
        finally
        {
            _suppressChanged = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Reindex()
    {
        for (var index = 0; index < _clips.Count; index++)
        {
            _clips[index].Order = index + 1;
        }

        RefreshSummary();
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(TargetDuration));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(TargetText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(AxisQuarterText));
        OnPropertyChanged(nameof(AxisHalfText));
        OnPropertyChanged(nameof(AxisThreeQuarterText));
        OnPropertyChanged(nameof(AxisEndText));
    }
}
