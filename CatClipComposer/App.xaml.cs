using System.Windows;
using System.IO;
using System.Windows.Threading;
using CatClipComposer.Desktop;
using CatClipComposer.Infrastructure.Composition;
using CatClipComposer.Presentation;

namespace CatClipComposer;

public partial class App : Application
{
    private sealed class DirectProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var splash = new SplashWindow();
        splash.Show();

        try
        {
            splash.Report(new StartupProgress(1, "Starting the WPF application runtime…", "SYSTEM / BOOT"));
            await Dispatcher.Yield(DispatcherPriority.Render);
            await splash.WaitForOpeningDisplayAsync();
            splash.QueueReport(new StartupProgress(
                4,
                "Resolving portable data paths and opening the catalog database…",
                "SYSTEM / SERVICES"), paceFastMessages: true);
            var services = await ApplicationServicesFactory.CreateAsync();
            splash.QueueReport(new StartupProgress(
                8,
                "Catalog, rendering, thumbnail, and plugin services are online.",
                "SYSTEM / SERVICES"), paceFastMessages: true);
            splash.QueueReport(
                new StartupProgress(10, "Reading CatClipComposer.ini preferences…", "CONFIGURATION"),
                paceFastMessages: true);
            var settings = await services.SettingsStore.LoadAsync();
            await splash.WaitForPendingReportsAsync();
            var paceFastMessages = !(settings.RescanLibraryOnStartup && settings.SourceFolders.Count > 0);
            IProgress<StartupProgress> progress = new DirectProgress<StartupProgress>(
                update => splash.QueueReport(update, paceFastMessages));
            progress.Report(new StartupProgress(
                14,
                $"Preferences decoded: {settings.SourceFolders.Count} source folder(s), startup scan " +
                $"{(settings.RescanLibraryOnStartup ? "enabled" : "disabled")}.",
                "CONFIGURATION"));
            progress.Report(new StartupProgress(16, "Preparing the portable custom-font workspace…", "FONTS"));
            try
            {
                Directory.CreateDirectory(settings.CustomFontFolder);
            }
            catch (Exception exception)
            {
                progress.Report(new StartupProgress(
                    17,
                    $"Custom font folder unavailable: {exception.Message}",
                    "FONTS / WARNING"));
            }

            progress.Report(new StartupProgress(
                18,
                "Loading saved window geometry, panel docks, splitters, and preview arrangement…",
                "SOFTWARE LAYOUT"));
            var mainWindow = new MainWindow(
                settings,
                services.SettingsStore,
                services.ProjectStore,
                services.Catalog,
                services.Scanner,
                services.VideoRenderer,
                services.CompositionExporter,
                services.Plugins);
            MainWindow = mainWindow;
            progress.Report(new StartupProgress(
                24,
                $"Software layout applied: {settings.ContentBrowserDock}/{settings.PreviewDock}/" +
                $"{settings.LayersDock}/{settings.TimelineDock}; previews " +
                $"{(settings.PreviewsSplit ? "split" : "joined")}.",
                "SOFTWARE LAYOUT"));
            progress.Report(new StartupProgress(
                26,
                "Binding browser, preview, layer, and timeline workspaces…",
                "EDITOR WORKSPACE"));
            await mainWindow.InitializeAsync(progress);
            await splash.WaitForPendingReportsAsync();
            var minimumSplashDuration = settings.FirstStartupCompleted
                ? TimeSpan.FromSeconds(3)
                : TimeSpan.FromSeconds(5);
            if (!settings.FirstStartupCompleted)
            {
                settings.FirstStartupCompleted = true;
                await services.SettingsStore.SaveAsync(settings);
            }

            await splash.WaitForCompletionDisplayAsync(minimumSplashDuration);
            splash.Topmost = false;
            splash.Close();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            mainWindow.Activate();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(splash, "Cat Clip Composer could not start.", exception);
            splash.Close();
            Shutdown(1);
        }
    }
}
