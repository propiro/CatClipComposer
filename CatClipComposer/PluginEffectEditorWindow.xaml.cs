using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
    private readonly Func<ProjectTimelineItem, CancellationToken, Task<RenderResult>>? _framePreviewRenderer;
    private readonly TimeSpan? _previewFrame;
    private readonly DispatcherTimer _framePreviewDebounce = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly SemaphoreSlim _framePreviewGate = new(1, 1);
    private EffectFramePreviewWindow? _framePreviewWindow;
    private CancellationTokenSource? _framePreviewCancellation;
    private long _framePreviewGeneration;

    public PluginEffectEditorWindow(
        IEnumerable<ICatClipPlugin> plugins,
        ProjectTrack track,
        TimeSpan projectDuration,
        TimelineSnapMode snapMode,
        double framesPerSecond,
        ProjectTimelineItem? existing = null,
        TimeSpan? initialStart = null,
        TimeSpan? initialDuration = null,
        string? initialPluginId = null,
        TimeSpan? previewFrame = null,
        Func<ProjectTimelineItem, CancellationToken, Task<RenderResult>>? framePreviewRenderer = null)
    {
        _track = track;
        _snapMode = snapMode;
        _framesPerSecond = framesPerSecond;
        _existing = existing;
        _previewFrame = previewFrame;
        _framePreviewRenderer = framePreviewRenderer;
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _framePreviewDebounce.Tick += FramePreviewDebounce_Tick;
        TimeRangeEditor.RangeEdited += EditorValueChanged;
        FramePreviewControls.Visibility = previewFrame.HasValue && framePreviewRenderer is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        LocationChanged += (_, _) => _framePreviewWindow?.SnapBeside(this);
        SizeChanged += (_, _) => _framePreviewWindow?.SnapBeside(this);
        Closed += (_, _) => CloseFramePreview();
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
            WatchEditor(editor);
        }

        QueueFramePreview();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateResultItem(out var resultItem, out var error))
        {
            ShowInvalid(error);
            return;
        }

        ResultItem = resultItem;
        DialogResult = true;
    }

    private bool TryCreateResultItem(out ProjectTimelineItem resultItem, out string error)
    {
        resultItem = null!;
        error = string.Empty;
        if (PluginComboBox.SelectedItem is not ICatClipPlugin plugin ||
            !TimeRangeEditor.TryGetRange(out var start, out var duration))
        {
            error = "Choose a module and enter a non-negative start with an end after it.";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in plugin.Descriptor.Parameters)
        {
            var value = ReadEditorValue(_parameterEditors[definition.Key]);
            if (definition.Type == PluginParameterType.Number)
            {
                if (!TryNumber(value, out _))
                {
                    error = $"{definition.DisplayName} must be a finite number.";
                    return false;
                }
            }
            else if (definition.Type == PluginParameterType.Color && !IsHexColor(value))
            {
                error = $"{definition.DisplayName} must use #RRGGBB format.";
                return false;
            }

            values[definition.Key] = value;
        }

        resultItem = new ProjectTimelineItem
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Kind = ProjectItemKind.Effect,
            Name = plugin.Descriptor.Name,
            StartTicks = start.Ticks,
            DurationTicks = duration.Ticks,
            PluginId = plugin.Descriptor.Id,
            PluginParameters = values,
            IsEnabled = _existing?.IsEnabled ?? true,
            Color = _existing?.Color ?? string.Empty
        };
        return true;
    }

    private void WatchEditor(FrameworkElement editor)
    {
        switch (editor)
        {
            case NumericEditorControl numeric:
                numeric.Edited += EditorValueChanged;
                break;
            case TextBox text:
                text.TextChanged += (_, _) => EditorValueChanged(text, EventArgs.Empty);
                break;
            case CheckBox checkBox:
                checkBox.Checked += (_, _) => EditorValueChanged(checkBox, EventArgs.Empty);
                checkBox.Unchecked += (_, _) => EditorValueChanged(checkBox, EventArgs.Empty);
                break;
            case ComboBox comboBox:
                comboBox.SelectionChanged += (_, _) => EditorValueChanged(comboBox, EventArgs.Empty);
                break;
        }
    }

    private void EditorValueChanged(object? sender, EventArgs e) => QueueFramePreview();

    private void QueueFramePreview()
    {
        if (_framePreviewWindow is null || AutoPreviewCheckBox.IsChecked != true)
        {
            return;
        }

        _framePreviewDebounce.Stop();
        _framePreviewDebounce.Start();
    }

    private async void FramePreviewDebounce_Tick(object? sender, EventArgs e)
    {
        _framePreviewDebounce.Stop();
        await RefreshFramePreviewAsync();
    }

    private async void PreviewFrame_Click(object sender, RoutedEventArgs e)
    {
        EnsureFramePreviewWindow();
        await RefreshFramePreviewAsync();
    }

    private void EnsureFramePreviewWindow()
    {
        if (_framePreviewWindow is not null)
        {
            _framePreviewWindow.Activate();
            return;
        }

        _framePreviewWindow = new EffectFramePreviewWindow { Owner = this };
        _framePreviewWindow.Closed += (_, _) =>
        {
            _framePreviewCancellation?.Cancel();
            _framePreviewWindow = null;
        };
        _framePreviewWindow.Show();
        _framePreviewWindow.SnapBeside(this);
    }

    private async Task RefreshFramePreviewAsync()
    {
        if (_framePreviewWindow is null || !_previewFrame.HasValue || _framePreviewRenderer is null)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _framePreviewGeneration);
        _framePreviewCancellation?.Cancel();
        await _framePreviewGate.WaitAsync();
        try
        {
            if (generation != _framePreviewGeneration || _framePreviewWindow is null)
            {
                return;
            }

            if (!TryCreateResultItem(out var previewItem, out var error))
            {
                _framePreviewWindow.ShowError(error);
                return;
            }

            _framePreviewCancellation?.Dispose();
            _framePreviewCancellation = new CancellationTokenSource();
            _framePreviewWindow.SetLoading(_previewFrame.Value);
            try
            {
                var result = await _framePreviewRenderer(previewItem, _framePreviewCancellation.Token);
                if (generation == _framePreviewGeneration && _framePreviewWindow is not null)
                {
                    _framePreviewWindow.ShowPreview(result.OutputPath, _previewFrame.Value);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _framePreviewWindow?.ShowError($"Frame preview failed: {exception.Message}");
            }
        }
        finally
        {
            _framePreviewGate.Release();
        }
    }

    private void CloseFramePreview()
    {
        _framePreviewDebounce.Stop();
        _framePreviewCancellation?.Cancel();
        _framePreviewCancellation?.Dispose();
        _framePreviewCancellation = null;
        if (_framePreviewWindow is not null)
        {
            var window = _framePreviewWindow;
            _framePreviewWindow = null;
            window.Close();
        }
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
