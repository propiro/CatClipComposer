using System.Windows;
using System.IO;
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
            var initialProgress = new List<StartupProgress>
            {
                new(2, "Preparing portable application services…")
            };
            var services = await ApplicationServicesFactory.CreateAsync();
            initialProgress.Add(new StartupProgress(5, "Reading application preferences…"));
            var settings = await services.SettingsStore.LoadAsync();
            var paceFastMessages = !(settings.RescanLibraryOnStartup && settings.SourceFolders.Count > 0);
            foreach (var update in initialProgress)
            {
                splash.QueueReport(update, paceFastMessages);
            }

            IProgress<StartupProgress> progress = new DirectProgress<StartupProgress>(
                update => splash.QueueReport(update, paceFastMessages));
            try
            {
                Directory.CreateDirectory(settings.CustomFontFolder);
            }
            catch (Exception exception)
            {
                progress.Report(new StartupProgress(6, $"Custom font folder unavailable: {exception.Message}"));
            }

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
            await mainWindow.InitializeAsync(progress);
            await splash.WaitForPendingReportsAsync();
            await splash.WaitForMinimumDisplayAsync();
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
