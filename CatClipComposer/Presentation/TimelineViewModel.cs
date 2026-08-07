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
    private double _pixelsPerSecond = 8;
    private double _trackHeight = 64;
    private double _framesPerSecond = 30;
    private TimelineRulerMode _rulerMode = TimelineRulerMode.TimeAndFrames;
    private TimelineSnapMode _snapMode = TimelineSnapMode.TenthSecond;
    private TimeSpan _playhead;
    private TimeSpan _rangeStart;
    private TimeSpan _rangeEnd;

    public TimelineViewModel(double targetDurationMinutes)
    {
        Clips = new ReadOnlyObservableCollection<TimelineClipViewModel>(_clips);
        RulerTicks = [];
        _targetDuration = TimeSpan.FromMinutes(targetDurationMinutes);
        _clips.CollectionChanged += (_, _) =>
        {
            RefreshSummary();
            if (!_suppressChanged)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
        RefreshRuler();
    }

    public event EventHandler? Changed;

    public event EventHandler? DisplaySettingsChanged;

    public event EventHandler? SelectionChanged;

    public ReadOnlyObservableCollection<TimelineClipViewModel> Clips { get; }

    public TimelineClipViewModel? SelectedClip
    {
        get => _selectedClip;
        set
        {
            if (SetProperty(ref _selectedClip, value))
            {
                OnPropertyChanged(nameof(SelectedClipStart));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public ObservableCollection<TimelineTickViewModel> RulerTicks { get; }

    public double PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set
        {
            var normalized = Math.Clamp(value, 0.1, 40);
            if (SetProperty(ref _pixelsPerSecond, normalized))
            {
                RefreshRuler();
                NotifyRangeDisplayChanged();
                DisplaySettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double TrackHeight
    {
        get => _trackHeight;
        set
        {
            var normalized = Math.Clamp(value, 28, 110);
            if (SetProperty(ref _trackHeight, normalized))
            {
                DisplaySettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double FramesPerSecond => _framesPerSecond;

    public TimeSpan Playhead => _playhead;

    public double PlayheadLeft => _playhead.TotalSeconds * PixelsPerSecond;

    public string PlayheadText => $"{DurationFormatter.Format(_playhead)} | frame {Math.Round(_playhead.TotalSeconds * _framesPerSecond):0}";

    public bool HasRangeSelection => _rangeEnd > _rangeStart;

    public TimeSpan RangeStart => _rangeStart;

    public TimeSpan RangeEnd => _rangeEnd;

    public double RangeLeft => _rangeStart.TotalSeconds * PixelsPerSecond;

    public double RangeEndLeft => _rangeEnd.TotalSeconds * PixelsPerSecond;

    public double RangeWidth => Math.Max(0, (_rangeEnd - _rangeStart).TotalSeconds * PixelsPerSecond);

    public string RangeText => HasRangeSelection
        ? $"{DurationFormatter.Format(_rangeStart)} - {DurationFormatter.Format(_rangeEnd)} ({DurationFormatter.Format(_rangeEnd - _rangeStart)})"
        : string.Empty;

    public double SnapIncrement => _snapMode switch
    {
        TimelineSnapMode.Frame => 1 / _framesPerSecond,
        TimelineSnapMode.TenthSecond => 0.1,
        TimelineSnapMode.HalfSecond => 0.5,
        _ => 1
    };

    public TimelineRulerMode RulerMode => _rulerMode;

    public TimelineSnapMode SnapMode => _snapMode;

    public string RulerModeButtonText => _rulerMode switch
    {
        TimelineRulerMode.Time => "Ruler: time",
        TimelineRulerMode.Frames => "Ruler: frames",
        _ => "Ruler: time + frames"
    };

    public string SnapModeButtonText => _snapMode switch
    {
        TimelineSnapMode.Frame => "Snap: 1 frame",
        TimelineSnapMode.TenthSecond => "Snap: 0.1 sec",
        TimelineSnapMode.HalfSecond => "Snap: 0.5 sec",
        _ => "Snap: 1 sec"
    };

    public double TimelineWidth => Math.Max(
        720,
        Math.Max(TargetDuration.TotalSeconds, Duration.TotalSeconds) * PixelsPerSecond + 1);

    public TimeSpan Duration => TimeSpan.FromTicks(_clips.Sum(clip => clip.Duration.Ticks));

    public TimeSpan? SelectedClipStart
    {
        get
        {
            if (SelectedClip is null)
            {
                return null;
            }

            var index = _clips.IndexOf(SelectedClip);
            return index < 0
                ? null
                : TimeSpan.FromTicks(_clips.Take(index).Sum(clip => clip.Duration.Ticks));
        }
    }

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

    public TimelineClipViewModel AddMedia(MediaFile media, TimeSpan targetStart)
    {
        var insertionIndex = 0;
        var position = TimeSpan.Zero;
        while (insertionIndex < _clips.Count &&
               position + TimeSpan.FromTicks(_clips[insertionIndex].Duration.Ticks / 2) <= targetStart)
        {
            position += _clips[insertionIndex].Duration;
            insertionIndex++;
        }

        var clip = TimelineClipViewModel.FromMedia(media, insertionIndex + 1);
        _clips.Insert(insertionIndex, clip);
        SelectedClip = clip;
        Reindex();
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

    public bool Remove(IReadOnlyCollection<Guid> instanceIds)
    {
        var remove = _clips.Where(clip => instanceIds.Contains(clip.InstanceId)).ToList();
        if (remove.Count == 0)
        {
            return false;
        }

        _suppressChanged = true;
        try
        {
            foreach (var clip in remove)
            {
                _clips.Remove(clip);
            }

            SelectedClip = _clips.FirstOrDefault();
            Reindex();
        }
        finally
        {
            _suppressChanged = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool MoveSelection(IReadOnlyCollection<Guid> instanceIds, TimeSpan targetStart)
    {
        var moving = _clips.Where(clip => instanceIds.Contains(clip.InstanceId)).ToList();
        if (moving.Count == 0)
        {
            return false;
        }

        var remaining = _clips.Where(clip => !instanceIds.Contains(clip.InstanceId)).ToList();
        var insertionIndex = 0;
        var position = TimeSpan.Zero;
        while (insertionIndex < remaining.Count &&
               position + TimeSpan.FromTicks(remaining[insertionIndex].Duration.Ticks / 2) <= targetStart)
        {
            position += remaining[insertionIndex].Duration;
            insertionIndex++;
        }

        var reordered = remaining.Take(insertionIndex)
            .Concat(moving)
            .Concat(remaining.Skip(insertionIndex))
            .ToList();
        if (reordered.Select(item => item.InstanceId).SequenceEqual(_clips.Select(item => item.InstanceId)))
        {
            return false;
        }

        _suppressChanged = true;
        try
        {
            _clips.Clear();
            foreach (var clip in reordered)
            {
                _clips.Add(clip);
            }

            SelectedClip = moving[0];
            Reindex();
        }
        finally
        {
            _suppressChanged = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
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
        ClearRangeSelection();
    }

    public void SetTargetDuration(double minutes)
    {
        _targetDuration = TimeSpan.FromMinutes(minutes);
        RefreshSummary();
    }

    public void SetFrameRate(double framesPerSecond)
    {
        _framesPerSecond = Math.Clamp(framesPerSecond, 1, 240);
        SetPlayhead(_playhead);
        if (HasRangeSelection)
        {
            SetRangeSelection(_rangeStart, _rangeEnd);
        }
        OnPropertyChanged(nameof(PlayheadText));
        RefreshRuler();
    }

    public void SetPlayhead(TimeSpan value)
    {
        var normalized = NormalizeFrameTime(value);
        if (_playhead == normalized)
        {
            return;
        }

        _playhead = normalized;
        OnPropertyChanged(nameof(Playhead));
        OnPropertyChanged(nameof(PlayheadLeft));
        OnPropertyChanged(nameof(PlayheadText));
    }

    public void StepFrame(int direction) =>
        SetPlayhead(_playhead + TimeSpan.FromSeconds(direction / _framesPerSecond));

    public void SetRangeSelection(TimeSpan first, TimeSpan second)
    {
        var normalizedFirst = NormalizeFrameTime(first);
        var normalizedSecond = NormalizeFrameTime(second);
        var start = normalizedFirst <= normalizedSecond ? normalizedFirst : normalizedSecond;
        var end = normalizedFirst <= normalizedSecond ? normalizedSecond : normalizedFirst;
        if (_rangeStart == start && _rangeEnd == end)
        {
            return;
        }

        _rangeStart = start;
        _rangeEnd = end;
        NotifyRangeDisplayChanged();
    }

    public void ClearRangeSelection()
    {
        if (_rangeStart == TimeSpan.Zero && _rangeEnd == TimeSpan.Zero)
        {
            return;
        }

        _rangeStart = TimeSpan.Zero;
        _rangeEnd = TimeSpan.Zero;
        NotifyRangeDisplayChanged();
    }

    public void SetDisplaySettings(TimelineRulerMode rulerMode, TimelineSnapMode snapMode)
    {
        _rulerMode = rulerMode;
        _snapMode = snapMode;
        OnPropertyChanged(nameof(RulerMode));
        OnPropertyChanged(nameof(SnapMode));
        OnPropertyChanged(nameof(RulerModeButtonText));
        OnPropertyChanged(nameof(SnapModeButtonText));
        RefreshRuler();
    }

    public void CycleRulerMode()
    {
        _rulerMode = _rulerMode switch
        {
            TimelineRulerMode.Time => TimelineRulerMode.Frames,
            TimelineRulerMode.Frames => TimelineRulerMode.TimeAndFrames,
            _ => TimelineRulerMode.Time
        };
        OnPropertyChanged(nameof(RulerMode));
        OnPropertyChanged(nameof(RulerModeButtonText));
        RefreshRuler();
        DisplaySettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CycleSnapMode()
    {
        _snapMode = _snapMode switch
        {
            TimelineSnapMode.Frame => TimelineSnapMode.TenthSecond,
            TimelineSnapMode.TenthSecond => TimelineSnapMode.HalfSecond,
            TimelineSnapMode.HalfSecond => TimelineSnapMode.Second,
            _ => TimelineSnapMode.Frame
        };
        OnPropertyChanged(nameof(SnapMode));
        OnPropertyChanged(nameof(SnapModeButtonText));
        RefreshRuler();
        DisplaySettingsChanged?.Invoke(this, EventArgs.Empty);
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
        OnPropertyChanged(nameof(TimelineWidth));
        OnPropertyChanged(nameof(PlayheadLeft));
        NotifyRangeDisplayChanged();
        RefreshRuler();
    }

    private TimeSpan NormalizeFrameTime(TimeSpan value)
    {
        var maximum = Math.Max(TargetDuration.TotalSeconds, Duration.TotalSeconds);
        var seconds = Math.Clamp(value.TotalSeconds, 0, Math.Max(0, maximum));
        var frame = Math.Round(seconds * _framesPerSecond);
        return TimeSpan.FromSeconds(frame / _framesPerSecond);
    }

    private void NotifyRangeDisplayChanged()
    {
        OnPropertyChanged(nameof(HasRangeSelection));
        OnPropertyChanged(nameof(RangeStart));
        OnPropertyChanged(nameof(RangeEnd));
        OnPropertyChanged(nameof(RangeLeft));
        OnPropertyChanged(nameof(RangeEndLeft));
        OnPropertyChanged(nameof(RangeWidth));
        OnPropertyChanged(nameof(RangeText));
    }

    private void RefreshRuler()
    {
        RulerTicks.Clear();
        var seconds = Math.Max(1, Math.Max(TargetDuration.TotalSeconds, Duration.TotalSeconds));
        var requestedMinor = _snapMode switch
        {
            TimelineSnapMode.Frame => 1 / _framesPerSecond,
            TimelineSnapMode.TenthSecond => 0.1,
            TimelineSnapMode.HalfSecond => 0.5,
            _ => 1
        };
        var visualMinor = requestedMinor;
        while (seconds / visualMinor > 1800 || visualMinor * PixelsPerSecond < 4)
        {
            visualMinor *= visualMinor < 1 ? 2 : 5;
        }

        var majorEvery = Math.Max(1, (int)Math.Ceiling(72 / (visualMinor * PixelsPerSecond)));
        var tickCount = (int)Math.Ceiling(seconds / visualMinor);
        for (var index = 0; index <= tickCount; index++)
        {
            var at = Math.Min(seconds, index * visualMinor);
            var major = index % majorEvery == 0 || index == tickCount;
            RulerTicks.Add(new TimelineTickViewModel(
                at * PixelsPerSecond,
                major ? 19 : 8,
                major ? FormatRulerLabel(at) : string.Empty));
        }

        OnPropertyChanged(nameof(TimelineWidth));
    }

    private string FormatRulerLabel(double seconds)
    {
        var time = DurationFormatter.Format(TimeSpan.FromSeconds(seconds));
        var frame = $"f{Math.Round(seconds * _framesPerSecond):0}";
        return _rulerMode switch
        {
            TimelineRulerMode.Time => time,
            TimelineRulerMode.Frames => frame,
            _ => $"{time} · {frame}"
        };
    }
}
