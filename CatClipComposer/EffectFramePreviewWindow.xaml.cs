using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class EffectFramePreviewWindow : Window
{
    private readonly Stopwatch _renderTimer = new();
    private readonly DispatcherTimer _elapsedTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _preparationTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public EffectFramePreviewWindow()
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _elapsedTimer.Tick += (_, _) => UpdateElapsedText();
        _preparationTimer.Tick += (_, _) =>
        {
            if (RenderProgressBar.Value < 23)
            {
                RenderProgressBar.Value = Math.Min(23, RenderProgressBar.Value + 1);
            }
        };
    }

    public void SetLoading(TimeSpan frame)
    {
        StatusText.Text = $"Preparing project at {FormatTime(frame)}… 5%";
        RenderProgressBar.Value = 5;
        RenderProgressBar.IsIndeterminate = false;
        RenderProgressBar.Visibility = Visibility.Visible;
        _renderTimer.Restart();
        _elapsedTimer.Start();
        _preparationTimer.Start();
        UpdateElapsedText();
    }

    public void ReportProgress(RenderProgress progress)
    {
        RenderProgressBar.IsIndeterminate = false;
        var displayedPercent = progress.Percent >= 100
            ? 100
            : progress.ProcessedDuration == TimeSpan.Zero && progress.Percent is > 0 and < 25
                ? progress.Percent
                : 25 + Math.Clamp(progress.Percent, 0, 100) * 0.74;
        displayedPercent = Math.Max(RenderProgressBar.Value, displayedPercent);
        RenderProgressBar.Value = displayedPercent;
        if (displayedPercent >= 25)
        {
            _preparationTimer.Stop();
        }

        var timing = progress.TotalDuration > TimeSpan.Zero
            ? $" ({FormatTime(progress.ProcessedDuration)} / {FormatTime(progress.TotalDuration)})"
            : string.Empty;
        StatusText.Text = $"{progress.Message}… {displayedPercent:0}%{timing}";
    }

    public void ShowPreview(string path, TimeSpan frame)
    {
        FramePlayer.Stop();
        FramePlayer.Source = new Uri(path, UriKind.Absolute);
        FramePlayer.IsMuted = true;
        StatusText.Text = $"Selected frame {FormatTime(frame)}";
        CompleteProgress();
        FramePlayer.Play();
    }

    public void ShowError(string message)
    {
        StatusText.Text = message;
        CompleteProgress();
    }

    public void SnapBeside(Window editor)
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, editor.ActualWidth);
        Left = Math.Clamp(editor.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        var desiredTop = editor.Top - Height - 8;
        Top = desiredTop >= workArea.Top
            ? desiredTop
            : Math.Clamp(editor.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
    }

    private void FramePlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        FramePlayer.Position = TimeSpan.Zero;
        Dispatcher.BeginInvoke(() => FramePlayer.Pause());
    }

    private void FramePlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        StatusText.Text = "Windows could not display the rendered frame preview.";
        CompleteProgress();
    }

    protected override void OnClosed(EventArgs e)
    {
        FramePlayer.Stop();
        FramePlayer.Source = null;
        _elapsedTimer.Stop();
        _preparationTimer.Stop();
        base.OnClosed(e);
    }

    private void CompleteProgress()
    {
        _renderTimer.Stop();
        _elapsedTimer.Stop();
        _preparationTimer.Stop();
        RenderProgressBar.IsIndeterminate = false;
        RenderProgressBar.Value = 100;
        UpdateElapsedText();
    }

    private void UpdateElapsedText() => ElapsedText.Text = $"{_renderTimer.Elapsed.TotalSeconds:0.0} s";

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss\.fff") : value.ToString(@"m\:ss\.fff");
}
