using System.Windows;
using System.Windows.Controls;

namespace CatClipComposer.Controls;

public partial class TimeRangeEditorControl : UserControl
{
    private bool _changingMode;
    private bool _useDuration;
    private double _projectEnd;
    private double _snapIncrement = 0.1;

    public TimeRangeEditorControl()
    {
        InitializeComponent();
    }

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

    private void SetStartZero_Click(object sender, RoutedEventArgs e) => StartEditor.SetValue(0);

    private void SetEndProject_Click(object sender, RoutedEventArgs e)
    {
        var start = StartEditor.TryGetValue(out var parsedStart) ? parsedStart : 0;
        EndEditor.SetValue(_useDuration ? Math.Max(_snapIncrement, _projectEnd - start) : _projectEnd);
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
    }
}
