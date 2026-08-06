using System.Windows;
using CatClipComposer.Core.Services;
using CatClipComposer.Infrastructure;

namespace CatClipComposer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var paths = new AppPaths();
            paths.EnsureCreated();
            ISettingsStore settingsStore = new JsonSettingsStore(paths);
            IMediaCatalog catalog = new SqliteMediaCatalog(paths);
            await catalog.InitializeAsync();
            var settings = await settingsStore.LoadAsync();
            IMediaProbe mediaProbe = new FfprobeMediaProbe();
            IThumbnailGenerator thumbnailGenerator = new FfmpegThumbnailGenerator(paths);
            IMediaScanner scanner = new MediaScanner(catalog, mediaProbe, thumbnailGenerator);
            IVideoRenderer videoRenderer = new FfmpegVideoRenderer();

            var mainWindow = new MainWindow(
                settings,
                settingsStore,
                catalog,
                scanner,
                videoRenderer);
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Cat Clip Composer could not start.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
