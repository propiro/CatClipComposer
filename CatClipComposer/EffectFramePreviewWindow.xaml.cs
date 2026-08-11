using System.Windows;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class EffectFramePreviewWindow : Window
{
    public EffectFramePreviewWindow()
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
    }

    public void SetLoading(TimeSpan frame)
    {
        StatusText.Text = $"Rendering selected frame {FormatTime(frame)}…";
    }

    public void ShowPreview(string path, TimeSpan frame)
    {
        FramePlayer.Stop();
        FramePlayer.Source = new Uri(path, UriKind.Absolute);
        FramePlayer.IsMuted = true;
        StatusText.Text = $"Selected frame {FormatTime(frame)}";
        FramePlayer.Play();
    }

    public void ShowError(string message)
    {
        StatusText.Text = message;
    }

    public void SnapBeside(Window editor)
    {
        var workArea = SystemParameters.WorkArea;
        var desiredLeft = editor.Left + editor.ActualWidth + 8;
        Left = desiredLeft + Width <= workArea.Right
            ? desiredLeft
            : Math.Max(workArea.Left, editor.Left - Width - 8);
        Top = Math.Clamp(editor.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
    }

    private void FramePlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        FramePlayer.Position = TimeSpan.Zero;
        Dispatcher.BeginInvoke(() => FramePlayer.Pause());
    }

    private void FramePlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        StatusText.Text = "Windows could not display the rendered frame preview.";
    }

    protected override void OnClosed(EventArgs e)
    {
        FramePlayer.Stop();
        FramePlayer.Source = null;
        base.OnClosed(e);
    }

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss\.fff") : value.ToString(@"m\:ss\.fff");
}
