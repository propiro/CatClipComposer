using System.Windows;
using System.IO;
using CatClipComposer.Desktop;
using CatClipComposer.Infrastructure.Composition;
using CatClipComposer.Presentation;

namespace CatClipComposer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var splash = new SplashWindow();
        splash.Show();
        IProgress<StartupProgress> progress = new Progress<StartupProgress>(splash.Report);

        try
        {
            progress.Report(new StartupProgress(2, "Preparing portable application services…"));
            var services = await ApplicationServicesFactory.CreateAsync();
            progress.Report(new StartupProgress(5, "Reading application preferences…"));
            var settings = await services.SettingsStore.LoadAsync();
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
                services.CompositionExporter);
            MainWindow = mainWindow;
            await mainWindow.InitializeAsync(progress);
            mainWindow.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            splash.Close();
        }
        catch (Exception exception)
        {
            DesktopDialogs.ShowError(splash, "Cat Clip Composer could not start.", exception);
            splash.Close();
            Shutdown(1);
        }
    }
}
