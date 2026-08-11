using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CatClipComposer.Controls;

public partial class NumericEditorControl : UserControl
{
    private bool _synchronizing;
    private double _step = 1;

    public NumericEditorControl()
    {
        InitializeComponent();
        Minimum = 0;
        Maximum = 100;
        Step = 1;
        SetValue(0);
    }

    public event EventHandler? Edited;

    public bool ShowSlider
    {
        get => ValueSlider.Visibility == Visibility.Visible;
        set => ValueSlider.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public double Minimum
    {
        get => ValueSlider.Minimum;
        set
        {
            _synchronizing = true;
            ValueSlider.Minimum = value;
            _synchronizing = false;
            SynchronizeSliderFromText();
        }
    }

    public double Maximum
    {
        get => ValueSlider.Maximum;
        set
        {
            _synchronizing = true;
            ValueSlider.Maximum = Math.Max(value, ValueSlider.Minimum);
            _synchronizing = false;
            SynchronizeSliderFromText();
        }
    }

    public double Step
    {
        get => _step;
        set
        {
            _step = Math.Max(0.000001, value);
            ValueSlider.TickFrequency = _step;
            ValueSlider.SmallChange = _step;
            ValueSlider.LargeChange = _step * 10;
        }
    }

    public string Text
    {
        get => ValueTextBox.Text;
        set
        {
            _synchronizing = true;
            ValueTextBox.Text = value;
            _synchronizing = false;
            SynchronizeSliderFromText();
        }
    }

    public void SetValue(double value) =>
        Text = value.ToString("0.######", CultureInfo.InvariantCulture);

    public bool TryGetValue(out double value) =>
        double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value);

    private void ValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_synchronizing)
        {
            return;
        }

        _synchronizing = true;
        ValueTextBox.Text = e.NewValue.ToString("0.######", CultureInfo.InvariantCulture);
        _synchronizing = false;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_synchronizing)
        {
            return;
        }

        SynchronizeSliderFromText();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void Decrease_Click(object sender, RoutedEventArgs e) => Adjust(-Step);

    private void Increase_Click(object sender, RoutedEventArgs e) => Adjust(Step);

    private void Adjust(double change)
    {
        var current = TryGetValue(out var parsed)
            ? parsed
            : ValueSlider.Value;
        SetValue(Math.Clamp(current + change, Minimum, Maximum));
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void SynchronizeSliderFromText()
    {
        if (!TryGetValue(out var parsed))
        {
            return;
        }

        _synchronizing = true;
        ValueSlider.Value = Math.Clamp(parsed, Minimum, Maximum);
        _synchronizing = false;
    }
}
