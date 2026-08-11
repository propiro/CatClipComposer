using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Globalization;

namespace CatClipComposer.Controls;

public partial class TimeRangeEditorControl : UserControl
{
    private bool _changingMode;
    private bool _useDuration;
    private double _projectEnd;
    private double _snapIncrement = 0.1;
    private double _miniTrackDragPixels;
    private double _miniTrackOriginalStart;
    private double _miniTrackOriginalEnd;

    public TimeRangeEditorControl()
    {
        InitializeComponent();
        StartEditor.Edited += Editor_Edited;
        EndEditor.Edited += Editor_Edited;
    }

    public event EventHandler? RangeEdited;

    public void Configure(
        TimeSpan start,
        TimeSpan duration,
        TimeSpan projectDuration,
        double snapIncrement)
    {
        _projectEnd = Math.Max(snapIncrement, projectDuration.TotalSeconds);
        _snapIncrement = Math.Max(0.000001, snapIncrement);
        ConfigureEditor(StartEditor);
        ConfigureEditor(EndEditor);

        _changingMode = true;
        _useDuration = false;
        UseDurationCheckBox.IsChecked = false;
        EndLabel.Text = "End (seconds)";
        _changingMode = false;
        StartEditor.SetValue(start.TotalSeconds);
        EndEditor.SetValue((start + duration).TotalSeconds);
        MiniTrackEndText.Text = $"last clip  {_projectEnd.ToString("0.###", CultureInfo.InvariantCulture)} s";
        UpdateMiniTrack();
    }

    public bool TryGetRange(out TimeSpan start, out TimeSpan duration)
    {
        start = TimeSpan.Zero;
        duration = TimeSpan.Zero;
        if (!StartEditor.TryGetValue(out var startSeconds) ||
            !EndEditor.TryGetValue(out var secondValue) ||
            startSeconds < 0)
        {
            return false;
        }

        var durationSeconds = _useDuration
            ? secondValue
            : secondValue - startSeconds;
        if (durationSeconds <= 0)
        {
            return false;
        }

        start = TimeSpan.FromSeconds(startSeconds);
        duration = TimeSpan.FromSeconds(durationSeconds);
        return true;
    }

    private void ConfigureEditor(NumericEditorControl editor)
    {
        editor.Minimum = 0;
        editor.Maximum = _projectEnd;
        editor.Step = _snapIncrement;
    }

    private void SetStartZero_Click(object sender, RoutedEventArgs e)
    {
        StartEditor.SetValue(0);
        UpdateMiniTrack();
        RangeEdited?.Invoke(this, EventArgs.Empty);
    }

    private void SetEndProject_Click(object sender, RoutedEventArgs e)
    {
        var start = StartEditor.TryGetValue(out var parsedStart) ? parsedStart : 0;
        EndEditor.SetValue(_useDuration ? Math.Max(_snapIncrement, _projectEnd - start) : _projectEnd);
        UpdateMiniTrack();
        RangeEdited?.Invoke(this, EventArgs.Empty);
    }

    private void UseDurationCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_changingMode)
        {
            return;
        }

        var newUseDuration = UseDurationCheckBox.IsChecked == true;
        if (newUseDuration == _useDuration)
        {
            return;
        }

        var start = StartEditor.TryGetValue(out var parsedStart) ? parsedStart : 0;
        var second = EndEditor.TryGetValue(out var parsedSecond) ? parsedSecond : start + _snapIncrement;
        EndEditor.SetValue(newUseDuration
            ? Math.Max(_snapIncrement, second - start)
            : start + Math.Max(_snapIncrement, second));
        _useDuration = newUseDuration;
        EndLabel.Text = _useDuration ? "Duration (seconds)" : "End (seconds)";
        UpdateMiniTrack();
        RangeEdited?.Invoke(this, EventArgs.Empty);
    }

    private void Editor_Edited(object? sender, EventArgs e)
    {
        UpdateMiniTrack();
        RangeEdited?.Invoke(this, EventArgs.Empty);
    }

    private void MiniTrackCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMiniTrack();

    private void MiniTrack_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (!TryGetRange(out var start, out var duration))
        {
            return;
        }

        _miniTrackDragPixels = 0;
        _miniTrackOriginalStart = start.TotalSeconds;
        _miniTrackOriginalEnd = (start + duration).TotalSeconds;
    }

    private void MiniTrack_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var width = MiniTrackCanvas.ActualWidth;
        if (width <= 0 || _projectEnd <= 0)
        {
            return;
        }

        _miniTrackDragPixels += e.HorizontalChange;
        var delta = _miniTrackDragPixels / width * _projectEnd;
        var start = _miniTrackOriginalStart;
        var end = _miniTrackOriginalEnd;
        if (sender is Thumb { Tag: "Start" })
        {
            start = Math.Clamp(Snap(_miniTrackOriginalStart + delta), 0, end - _snapIncrement);
        }
        else if (sender is Thumb { Tag: "End" })
        {
            end = Math.Clamp(Snap(_miniTrackOriginalEnd + delta), start + _snapIncrement, _projectEnd);
        }
        else
        {
            var duration = Math.Min(_projectEnd, Math.Max(_snapIncrement, end - start));
            start = Math.Clamp(Snap(_miniTrackOriginalStart + delta), 0, _projectEnd - duration);
            end = start + duration;
        }

        SetRange(start, end);
        RangeEdited?.Invoke(this, EventArgs.Empty);
    }

    private double Snap(double value) => Math.Round(value / _snapIncrement) * _snapIncrement;

    private void SetRange(double start, double end)
    {
        StartEditor.SetValue(start);
        EndEditor.SetValue(_useDuration ? end - start : end);
        UpdateMiniTrack();
    }

    private void UpdateMiniTrack()
    {
        var width = MiniTrackCanvas.ActualWidth;
        if (width <= 0 || _projectEnd <= 0 || !TryGetRange(out var start, out var duration))
        {
            return;
        }

        var startPixel = Math.Clamp(start.TotalSeconds / _projectEnd * width, 0, width);
        var endPixel = Math.Clamp((start + duration).TotalSeconds / _projectEnd * width, 0, width);
        var handleWidth = RangeStartThumb.Width;
        Canvas.SetLeft(RangeStartThumb, Math.Clamp(startPixel - handleWidth / 2, 0, Math.Max(0, width - handleWidth)));
        Canvas.SetLeft(RangeEndThumb, Math.Clamp(endPixel - handleWidth / 2, 0, Math.Max(0, width - handleWidth)));
        Canvas.SetLeft(RangeMoveThumb, startPixel);
        RangeMoveThumb.Width = Math.Max(handleWidth, endPixel - startPixel);
    }
}
