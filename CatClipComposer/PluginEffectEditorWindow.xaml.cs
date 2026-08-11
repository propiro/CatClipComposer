using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CatClipComposer.Controls;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Plugins;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class PluginEffectEditorWindow : Window
{
    private readonly ProjectTrack _track;
    private readonly TimelineSnapMode _snapMode;
    private readonly double _framesPerSecond;
    private readonly Dictionary<string, FrameworkElement> _parameterEditors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProjectTimelineItem? _existing;

    public PluginEffectEditorWindow(
        IEnumerable<ICatClipPlugin> plugins,
        ProjectTrack track,
        TimeSpan projectDuration,
        TimelineSnapMode snapMode,
        double framesPerSecond,
        ProjectTimelineItem? existing = null,
        TimeSpan? initialStart = null,
        TimeSpan? initialDuration = null,
        string? initialPluginId = null)
    {
        _track = track;
        _snapMode = snapMode;
        _framesPerSecond = framesPerSecond;
        _existing = existing;
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        TrackText.Text = $"Target timeline: {track.Name} ({track.Kind})";
        SnapText.Text = $"Sliders and ± buttons use {DescribeSnap(snapMode, framesPerSecond)}; typed values stay exact. Timeline dragging can also snap to clip boundaries.";
        var compatible = plugins.OfType<ICatClipVideoEffectPlugin>()
            .Where(plugin => plugin.Descriptor.CompatibleTracks.Contains(track.Kind))
            .OrderBy(plugin => plugin.Descriptor.Name)
            .Cast<ICatClipPlugin>()
            .ToList();
        PluginComboBox.ItemsSource = compatible;
        PluginComboBox.SelectedItem = existing is null
            ? compatible.FirstOrDefault(plugin => plugin.Descriptor.Id.Equals(
                  initialPluginId,
                  StringComparison.OrdinalIgnoreCase)) ?? compatible.FirstOrDefault()
            : compatible.FirstOrDefault(plugin => plugin.Descriptor.Id.Equals(existing.PluginId, StringComparison.OrdinalIgnoreCase));
        var start = existing?.Start ?? initialStart ?? TimeSpan.Zero;
        var duration = existing?.Duration ??
                       initialDuration ??
                       TimeSpan.FromSeconds(track.Kind == ProjectTrackKind.Background
                           ? Math.Max(SnapIncrement, projectDuration.TotalSeconds)
                           : Math.Max(SnapIncrement, Math.Min(5, projectDuration.TotalSeconds)));
        TimeRangeEditor.Configure(start, duration, projectDuration, SnapIncrement);
        if (existing is not null)
        {
            TitleText.Text = "Edit plugin effect";
            ApplyButton.Content = "Apply changes";
        }
        else if (initialStart.HasValue && initialDuration.HasValue)
        {
            TitleText.Text = "Add effect for selected clip";
        }
    }

    public ProjectTimelineItem? ResultItem { get; private set; }

    private void PluginComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ParametersPanel.Children.Clear();
        _parameterEditors.Clear();
        if (PluginComboBox.SelectedItem is not ICatClipPlugin plugin)
        {
            DescriptionText.Text = "No compatible plugin module is loaded for this timeline.";
            ApplyButton.IsEnabled = false;
            return;
        }

        ApplyButton.IsEnabled = true;
        DescriptionText.Text = $"{plugin.Descriptor.Description}  Module {plugin.Descriptor.Version}";
        foreach (var parameter in plugin.Descriptor.Parameters)
        {
            ParametersPanel.Children.Add(new TextBlock
            {
                Margin = new Thickness(0, _parameterEditors.Count == 0 ? 0 : 10, 0, 4),
                Text = parameter.DisplayName,
                ToolTip = parameter.Description
            });
            var value = _existing?.PluginParameters.GetValueOrDefault(parameter.Key) ?? parameter.DefaultValue;
            FrameworkElement editor = parameter.Type switch
            {
                PluginParameterType.Boolean => new CheckBox
                {
                    IsChecked = bool.TryParse(value, out var enabled) && enabled,
                    Content = "Enabled"
                },
                PluginParameterType.Choice => new ComboBox
                {
                    ItemsSource = parameter.Choices,
                    SelectedItem = parameter.Choices?.FirstOrDefault(choice =>
                        choice.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? parameter.Choices?.FirstOrDefault()
                },
                PluginParameterType.Number => CreateNumberEditor(parameter, value),
                _ => new TextBox { Text = value }
            };
            _parameterEditors[parameter.Key] = editor;
            ParametersPanel.Children.Add(editor);
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (PluginComboBox.SelectedItem is not ICatClipPlugin plugin ||
            !TimeRangeEditor.TryGetRange(out var start, out var duration))
        {
            ShowInvalid("Choose a module and enter a non-negative start with an end after it.");
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in plugin.Descriptor.Parameters)
        {
            var value = ReadEditorValue(_parameterEditors[definition.Key]);
            if (definition.Type == PluginParameterType.Number)
            {
                if (!TryNumber(value, out _))
                {
                    ShowInvalid($"{definition.DisplayName} must be a finite number.");
                    return;
                }
            }
            else if (definition.Type == PluginParameterType.Color && !IsHexColor(value))
            {
                ShowInvalid($"{definition.DisplayName} must use #RRGGBB format.");
                return;
            }

            values[definition.Key] = value;
        }

        ResultItem = new ProjectTimelineItem
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Kind = ProjectItemKind.Effect,
            Name = plugin.Descriptor.Name,
            StartTicks = start.Ticks,
            DurationTicks = duration.Ticks,
            PluginId = plugin.Descriptor.Id,
            PluginParameters = values
        };
        DialogResult = true;
    }

    private double SnapIncrement => _snapMode switch
    {
        TimelineSnapMode.Frame => 1 / Math.Clamp(_framesPerSecond, 1, 240),
        TimelineSnapMode.TenthSecond => 0.1,
        TimelineSnapMode.HalfSecond => 0.5,
        _ => 1
    };

    private static string ReadEditorValue(FrameworkElement editor) => editor switch
    {
        NumericEditorControl numericEditor => numericEditor.Text.Trim(),
        TextBox textBox => textBox.Text.Trim(),
        CheckBox checkBox => (checkBox.IsChecked == true).ToString().ToLowerInvariant(),
        ComboBox comboBox => comboBox.SelectedItem?.ToString() ?? string.Empty,
        _ => string.Empty
    };

    private static NumericEditorControl CreateNumberEditor(
        PluginParameterDefinition parameter,
        string value)
    {
        var minimum = parameter.Minimum ?? -100;
        var maximum = parameter.Maximum ?? 100;
        if (maximum <= minimum)
        {
            maximum = minimum + 1;
        }

        var range = maximum - minimum;
        var editor = new NumericEditorControl
        {
            Minimum = minimum,
            Maximum = maximum,
            Step = range <= 4 ? 0.01 : range <= 20 ? 0.1 : 1,
            Text = value
        };
        return editor;
    }

    private static bool TryNumber(string value, out double number) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    private static bool IsHexColor(string value) =>
        value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private static string DescribeSnap(TimelineSnapMode mode, double fps) => mode switch
    {
        TimelineSnapMode.Frame => $"one frame ({fps:0.###} fps)",
        TimelineSnapMode.TenthSecond => "0.1 second",
        TimelineSnapMode.HalfSecond => "0.5 second",
        _ => "1 second"
    };

    private void ShowInvalid(string message) => MessageBox.Show(
        this, message, "Invalid plugin settings", MessageBoxButton.OK, MessageBoxImage.Warning);
}
