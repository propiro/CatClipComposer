using System.Windows;
using CatClipComposer.Infrastructure.Composition;

namespace CatClipComposer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = await ApplicationServicesFactory.CreateAsync();
            var settings = await services.SettingsStore.LoadAsync();

            var mainWindow = new MainWindow(
                settings,
                services.SettingsStore,
                services.Catalog,
                services.Scanner,
                services.CompositionExporter);
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
