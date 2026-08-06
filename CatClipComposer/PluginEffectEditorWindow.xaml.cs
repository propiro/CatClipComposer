using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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
        ProjectTimelineItem? existing = null)
    {
        _track = track;
        _snapMode = snapMode;
        _framesPerSecond = framesPerSecond;
        _existing = existing;
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        TrackText.Text = $"Target timeline: {track.Name} ({track.Kind})";
        SnapText.Text = $"Typed values snap to {DescribeSnap(snapMode, framesPerSecond)}; timeline dragging also snaps to nearby block edges.";
        var compatible = plugins.OfType<ICatClipVideoEffectPlugin>()
            .Where(plugin => plugin.Descriptor.CompatibleTracks.Contains(track.Kind))
            .OrderBy(plugin => plugin.Descriptor.Name)
            .Cast<ICatClipPlugin>()
            .ToList();
        PluginComboBox.ItemsSource = compatible;
        PluginComboBox.SelectedItem = existing is null
            ? compatible.FirstOrDefault()
            : compatible.FirstOrDefault(plugin => plugin.Descriptor.Id.Equals(existing.PluginId, StringComparison.OrdinalIgnoreCase));
        StartTextBox.Text = (existing?.Start.TotalSeconds ?? 0).ToString("0.######", CultureInfo.InvariantCulture);
        DurationTextBox.Text = (existing?.Duration.TotalSeconds ??
                                (track.Kind == ProjectTrackKind.Background ? Math.Max(1, projectDuration.TotalSeconds) : 5))
            .ToString("0.######", CultureInfo.InvariantCulture);
        if (existing is not null)
        {
            TitleText.Text = "Edit plugin effect";
            ApplyButton.Content = "Apply changes";
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
                _ => new TextBox { Text = value }
            };
            _parameterEditors[parameter.Key] = editor;
            ParametersPanel.Children.Add(editor);
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (PluginComboBox.SelectedItem is not ICatClipPlugin plugin ||
            !TryNumber(StartTextBox.Text, out var start) || start < 0 ||
            !TryNumber(DurationTextBox.Text, out var duration) || duration <= 0)
        {
            ShowInvalid("Choose a module and enter non-negative start and positive duration values.");
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in plugin.Descriptor.Parameters)
        {
            var value = ReadEditorValue(_parameterEditors[definition.Key]);
            if (definition.Type == PluginParameterType.Number)
            {
                if (!TryNumber(value, out var number) ||
                    definition.Minimum.HasValue && number < definition.Minimum.Value ||
                    definition.Maximum.HasValue && number > definition.Maximum.Value)
                {
                    ShowInvalid($"{definition.DisplayName} is outside its allowed range.");
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
            StartTicks = TimeSpan.FromSeconds(Snap(start)).Ticks,
            DurationTicks = TimeSpan.FromSeconds(Math.Max(SnapIncrement, Snap(duration))).Ticks,
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

    private double Snap(double value) => Math.Round(value / SnapIncrement) * SnapIncrement;

    private static string ReadEditorValue(FrameworkElement editor) => editor switch
    {
        TextBox textBox => textBox.Text.Trim(),
        CheckBox checkBox => (checkBox.IsChecked == true).ToString().ToLowerInvariant(),
        ComboBox comboBox => comboBox.SelectedItem?.ToString() ?? string.Empty,
        _ => string.Empty
    };

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
