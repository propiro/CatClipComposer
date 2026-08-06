using System.Collections.ObjectModel;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class TimelineViewModel : ObservableObject
{
    private readonly ObservableCollection<TimelineClipViewModel> _clips = [];
    private TimelineClipViewModel? _selectedClip;
    private TimeSpan _targetDuration;

    public TimelineViewModel(double targetDurationMinutes)
    {
        Clips = new ReadOnlyObservableCollection<TimelineClipViewModel>(_clips);
        _targetDuration = TimeSpan.FromMinutes(targetDurationMinutes);
        _clips.CollectionChanged += (_, _) => RefreshSummary();
    }

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

    public IReadOnlyList<RenderSegment> CreateRenderSegments() =>
        _clips.Select(item => item.ToRenderSegment()).ToList();

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
