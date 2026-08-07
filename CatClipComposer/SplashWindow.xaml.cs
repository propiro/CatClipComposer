using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;

namespace CatClipComposer;

public partial class SplashWindow : Window
{
    public static readonly TimeSpan MinimumDisplayDuration = TimeSpan.FromSeconds(5);

    private readonly Stopwatch _displayTimer = Stopwatch.StartNew();

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

    public async Task WaitForMinimumDisplayAsync()
    {
        var remaining = MinimumDisplayDuration - _displayTimer.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }

    public void Report(StartupProgress update)
    {
        SplashProgressBar.Value = Math.Clamp(update.Percent, 0, 100);
        StatusText.Text = update.Message;
        LogLines.Add($"{DateTime.Now:HH:mm:ss}  {update.Message}");
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
