using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class FfmpegCommandWindow : Window
{
    private readonly IFfmpegCommandService _commandService;
    private readonly string _ffmpegPath;
    private CancellationTokenSource? _executionCancellation;
    private bool _isExecuting;

    public FfmpegCommandWindow(
        FfmpegCommandPreview preview,
        IFfmpegCommandService commandService,
        string ffmpegPath)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _commandService = commandService;
        _ffmpegPath = ffmpegPath;
        CommandTextBox.Text = preview.CommandText;
        OutputPathTextBlock.Text = $"Proposed output: {preview.OutputPath}";
        OutputPathTextBlock.ToolTip = preview.OutputPath;
        if (!string.IsNullOrWhiteSpace(preview.SupportingFilesFolder))
        {
            SupportingFilesTextBlock.Text =
                $"Persistent text-overlay command assets: {preview.SupportingFilesFolder}";
            SupportingFilesTextBlock.ToolTip = preview.SupportingFilesFolder;
            SupportingFilesTextBlock.Visibility = Visibility.Visible;
        }

        Closing += FfmpegCommandWindow_Closing;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(CommandTextBox.Text);
            StatusTextBlock.Text = "Complete edited command copied to the clipboard.";
        }
        catch (ExternalException exception)
        {
            DesktopDialogs.ShowError(this, "The command could not be copied to the clipboard.", exception);
        }
    }

    private async void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_isExecuting || string.IsNullOrWhiteSpace(CommandTextBox.Text))
        {
            return;
        }

        if (MessageBox.Show(
                this,
                "Execute the edited FFmpeg command now?\n\n" +
                "The command can read and write the paths shown in its arguments. Its -y argument permits " +
                "FFmpeg to overwrite its output without another prompt.",
                "Execute FFmpeg",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        _executionCancellation = new CancellationTokenSource();
        SetExecuting(true);
        StatusTextBlock.Text = "FFmpeg is running...";
        try
        {
            var result = await _commandService.ExecuteAsync(
                CommandTextBox.Text,
                _ffmpegPath,
                _executionCancellation.Token);
            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"FFmpeg stopped with exit code {result.ExitCode}."
                    : result.StandardError.Trim();
                throw new InvalidOperationException(detail);
            }

            StatusTextBlock.Text =
                "FFmpeg finished successfully. Check the output path in the edited command.";
            MessageBox.Show(
                this,
                "FFmpeg finished successfully.\n\nThis direct run was not added to Cat Clip Composer's export history.",
                "FFmpeg complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "FFmpeg execution cancelled.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "FFmpeg execution failed.";
            DesktopDialogs.ShowError(this, "The edited FFmpeg command failed.", exception);
        }
        finally
        {
            _executionCancellation.Dispose();
            _executionCancellation = null;
            SetExecuting(false);
        }
    }

    private void SetExecuting(bool value)
    {
        _isExecuting = value;
        ExecuteButton.IsEnabled = !value;
        CommandTextBox.IsReadOnly = value;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void FfmpegCommandWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExecuting)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                "FFmpeg is still running. Cancel it and close this window?",
                "Cancel FFmpeg",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _executionCancellation?.Cancel();
    }
}
