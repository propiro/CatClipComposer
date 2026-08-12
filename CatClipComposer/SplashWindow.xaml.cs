using System.Collections.ObjectModel;
using System.Windows;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;

namespace CatClipComposer;

public partial class SplashWindow : Window
{
    internal const int MessageDelayMinimumMilliseconds = 50;
    internal const int MessageDelayMaximumMilliseconds = 100;
    internal const int BoundaryDelayMinimumMilliseconds = 200;
    internal const int BoundaryDelayMaximumMilliseconds = 500;

    private Task _reportQueue = Task.CompletedTask;
    private DateTime? _lastReportUtc;

    public SplashWindow(bool canCancel = false)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        LogLines = [];
        DataContext = this;
        CancelButton.Visibility = canCancel ? Visibility.Visible : Visibility.Collapsed;
    }

    public event EventHandler? CancelRequested;

    public ObservableCollection<string> LogLines { get; }

    public Task WaitForOpeningDisplayAsync() => WaitForBoundaryPauseAsync();

    public Task WaitForCompletionDisplayAsync() => WaitForBoundaryPauseAsync();

    public void QueueReport(StartupProgress update, bool paceFastMessages)
    {
        if (!paceFastMessages)
        {
            Report(update);
            return;
        }

        _reportQueue = ReportPacedAsync(_reportQueue, update);
    }

    public Task WaitForPendingReportsAsync() => _reportQueue;

    private async Task ReportPacedAsync(Task precedingReport, StartupProgress update)
    {
        await precedingReport;
        if (_lastReportUtc.HasValue)
        {
            var targetGap = TimeSpan.FromMilliseconds(Random.Shared.Next(
                MessageDelayMinimumMilliseconds,
                MessageDelayMaximumMilliseconds + 1));
            var remaining = targetGap - (DateTime.UtcNow - _lastReportUtc.Value);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining);
            }
        }

        Report(update);
        _lastReportUtc = DateTime.UtcNow;
    }

    private static Task WaitForBoundaryPauseAsync() => Task.Delay(Random.Shared.Next(
        BoundaryDelayMinimumMilliseconds,
        BoundaryDelayMaximumMilliseconds + 1));

    public void Report(StartupProgress update)
    {
        var percent = Math.Clamp(update.Percent, 0, 100);
        var stage = string.IsNullOrWhiteSpace(update.Stage)
            ? "STARTUP"
            : update.Stage.Trim().ToUpperInvariant();
        SplashProgressBar.Value = percent;
        PercentText.Text = $"{percent:0.0}%";
        StageText.Text = stage;
        StatusText.Text = update.Message;
        LogLines.Add($"{DateTime.Now:HH:mm:ss}  [{percent,5:0.0}%]  {stage,-20}  {update.Message}");
        while (LogLines.Count > 200)
        {
            LogLines.RemoveAt(0);
        }

        LogList.ScrollIntoView(LogLines.LastOrDefault());
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelling…";
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
