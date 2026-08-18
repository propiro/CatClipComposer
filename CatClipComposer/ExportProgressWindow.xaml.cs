using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class ExportProgressWindow : Window
{
    private readonly Func<IProgress<RenderProgress>, Task<RenderResult>> _exportOperation;
    private readonly Action _cancelOperation;
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _elapsedTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly StringBuilder _activity = new();
    private bool _started;
    private bool _running;
    private string? _lastMessage;

    public ExportProgressWindow(
        string outputPath,
        string exportSummary,
        Func<IProgress<RenderProgress>, Task<RenderResult>> exportOperation,
        Action cancelOperation)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _exportOperation = exportOperation;
        _cancelOperation = cancelOperation;
        SummaryTextBlock.Text = exportSummary;
        DestinationTextBlock.Text = $"Destination: {outputPath}";
        DestinationTextBlock.ToolTip = outputPath;
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();
        Loaded += ExportProgressWindow_Loaded;
        Closing += ExportProgressWindow_Closing;
    }

    public RenderResult? Result { get; private set; }

    public Exception? Failure { get; private set; }

    private async void ExportProgressWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _running = true;
        _stopwatch.Start();
        _elapsedTimer.Start();
        AppendActivity("Export window opened; destination and project snapshot accepted.");
        var progress = new Progress<RenderProgress>(ReportProgress);
        try
        {
            Result = await _exportOperation(progress);
            ExportProgressBar.Value = 100;
            PercentTextBlock.Text = "100.0%";
            StageTextBlock.Text = "Export complete";
            ResultTextBlock.Text = $"Saved {System.IO.Path.GetFileName(Result.OutputPath)}";
            AppendActivity($"Complete: {Result.OutputPath}");
            OpenOutputButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            StageTextBlock.Text = "Export cancelled";
            ResultTextBlock.Text = "No replacement output was committed.";
            AppendActivity("Cancelled by the user; temporary render files are being discarded.");
        }
        catch (Exception exception)
        {
            Failure = exception;
            StageTextBlock.Text = "Export failed";
            StageTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            ResultTextBlock.Text = exception.Message;
            ResultTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            AppendActivity($"ERROR: {exception}");
        }
        finally
        {
            _running = false;
            _stopwatch.Stop();
            _elapsedTimer.Stop();
            UpdateElapsed();
            CancelButton.IsEnabled = false;
            CloseButton.IsEnabled = true;
        }
    }

    private void ReportProgress(RenderProgress progress)
    {
        var percent = Math.Clamp(progress.Percent, 0, 100);
        if (!_running || percent + 0.001 < ExportProgressBar.Value)
        {
            return;
        }

        ExportProgressBar.Value = Math.Max(ExportProgressBar.Value, percent);
        PercentTextBlock.Text = $"{ExportProgressBar.Value:0.0}%";
        StageTextBlock.Text = progress.Message;
        MediaTimeTextBlock.Text = progress.TotalDuration > TimeSpan.Zero
            ? $"Timeline: {FormatDuration(progress.ProcessedDuration)} / {FormatDuration(progress.TotalDuration)}"
            : "Timeline: preparing";
        if (!string.Equals(_lastMessage, progress.Message, StringComparison.Ordinal))
        {
            _lastMessage = progress.Message;
            AppendActivity(progress.Message);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_running)
        {
            return;
        }

        CancelButton.IsEnabled = false;
        StageTextBlock.Text = "Cancelling FFmpeg and cleaning temporary output…";
        AppendActivity("Cancellation requested.");
        _cancelOperation();
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (Result is not null)
        {
            DesktopShell.ShowFileInExplorer(Result.OutputPath);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ExportProgressWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_running)
        {
            return;
        }

        e.Cancel = true;
        Cancel_Click(this, new RoutedEventArgs());
    }

    private void AppendActivity(string message)
    {
        if (_activity.Length > 0)
        {
            _activity.AppendLine();
        }

        _activity.Append('[')
            .Append(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
            .Append("] ")
            .Append(message);
        ActivityTextBox.Text = _activity.ToString();
        ActivityTextBox.ScrollToEnd();
    }

    private void UpdateElapsed() =>
        ElapsedTextBlock.Text = $"Elapsed: {FormatDuration(_stopwatch.Elapsed)}";

    private static string FormatDuration(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
