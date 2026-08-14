using System.Windows;
using CatClipComposer.Core;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class AboutWindow : Window
{
    private readonly IApplicationUpdateChecker _updateChecker;
    private readonly CancellationTokenSource _closingCancellation = new();
    private ApplicationUpdateInfo? _lastResult;

    public AboutWindow(IApplicationUpdateChecker updateChecker)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _updateChecker = updateChecker;
        VersionText.Text = ProductInfo.DisplayVersion;
        RevisionText.Text = ProductInfo.BuildRevision is { Length: > 0 } revision
            ? $"Build revision: {revision[..Math.Min(12, revision.Length)]}"
            : "Build revision: unavailable";
        Closed += (_, _) =>
        {
            _closingCancellation.Cancel();
            _closingCancellation.Dispose();
        };
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        OpenLatestReleaseButton.Visibility = Visibility.Collapsed;
        UpdateProgressBar.Visibility = Visibility.Visible;
        UpdateStatusText.Text = "Contacting GitHub and comparing binary and source versions…";
        UpdateWarningText.Text = string.Empty;
        try
        {
            _lastResult = await _updateChecker.CheckAsync(
                ProductInfo.Version,
                ProductInfo.BuildRevision,
                _closingCancellation.Token);
            ShowUpdateResult(_lastResult);
        }
        catch (OperationCanceledException) when (_closingCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            UpdateStatusText.Text = $"The update check could not be completed: {exception.Message}";
        }
        finally
        {
            if (!_closingCancellation.IsCancellationRequested)
            {
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                CheckForUpdatesButton.IsEnabled = true;
            }
        }
    }

    private void ShowUpdateResult(ApplicationUpdateInfo result)
    {
        var binaryStatus = result.IsBinaryUpdateAvailable
            ? $"A newer downloadable application is available: v{result.LatestBinaryVersion}."
            : result.LatestBinaryVersion is not null
                ? $"Latest downloadable application: v{result.LatestBinaryVersion}; installed: v{result.CurrentVersion}."
                : "No downloadable application package could be identified.";
        var codeStatus = result.IsCodeUpdateAvailable
            ? $"Newer repository code is available" +
              (result.LatestCodeVersion is null ? "." : $": v{result.LatestCodeVersion}.")
            : result.LatestCodeVersion is not null
                ? $"Repository code version: v{result.LatestCodeVersion}; no newer code was detected."
                : "The repository code version could not be identified.";
        UpdateStatusText.Text = $"{binaryStatus}\n{codeStatus}\nChecked {result.CheckedAtUtc.ToLocalTime():g}.";
        UpdateWarningText.Text = result.Warnings.Count == 0
            ? string.Empty
            : "Partial-check notes: " + string.Join(" ", result.Warnings);
        OpenLatestReleaseButton.Visibility = result.IsBinaryUpdateAvailable
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OpenLatestRelease_Click(object sender, RoutedEventArgs e) =>
        OpenGitHubPage(_lastResult?.ReleasesUri ??
                       new Uri("https://github.com/propiro/CatClipComposer/releases/latest"));

    private void OpenRepository_Click(object sender, RoutedEventArgs e) =>
        OpenGitHubPage(_lastResult?.RepositoryUri ??
                       new Uri("https://github.com/propiro/CatClipComposer"));

    private void OpenGitHubPage(Uri uri)
    {
        try
        {
            DesktopShell.OpenTrustedGitHubPage(uri);
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(this, "The GitHub page could not be opened.", exception);
        }
    }
}
