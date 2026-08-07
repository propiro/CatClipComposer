using CatClipComposer.Core.Services;
using CatClipComposer.Core.Plugins;
using CatClipComposer.Infrastructure.Configuration;
using CatClipComposer.Infrastructure.Rendering;
using CatClipComposer.Infrastructure.Projects;
using CatClipComposer.Infrastructure.Plugins;

namespace CatClipComposer.Infrastructure.Composition;

public static class ApplicationServicesFactory
{
    public static async Task<ApplicationServices> CreateAsync(
        string? dataFolder = null,
        string? configurationPath = null,
        CancellationToken cancellationToken = default)
    {
        var bootstrapPaths = new AppPaths(dataFolder, configurationPath);
        ISettingsStore bootstrapSettingsStore = new IniSettingsStore(bootstrapPaths);
        var bootstrapSettings = await bootstrapSettingsStore.LoadAsync(cancellationToken);
        var paths = new AppPaths(
            dataFolder ?? bootstrapSettings.MetadataFolder,
            bootstrapPaths.ConfigurationPath);
        paths.EnsureCreated();

        ISettingsStore settingsStore = new IniSettingsStore(paths);
        IProjectStore projectStore = new JsonProjectStore(paths);
        IMediaCatalog catalog = new SqliteMediaCatalog(paths);
        await catalog.InitializeAsync(cancellationToken);

        IMediaProbe mediaProbe = new FfprobeMediaProbe();
        IThumbnailGenerator thumbnailGenerator = new FfmpegThumbnailGenerator(paths);
        IPreviewSheetGenerator previewSheetGenerator = new FfmpegPreviewSheetGenerator(paths);
        IMediaScanner scanner = new MediaScanner(
            catalog,
            mediaProbe,
            thumbnailGenerator,
            previewSheetGenerator);
        IVideoRenderer videoRenderer = new FfmpegVideoRenderer();
        ICompositionExporter compositionExporter = new CompositionExportService(videoRenderer, catalog);
        IPluginCatalog plugins = PluginCatalog.Load(Path.Combine(AppContext.BaseDirectory, "plugins"));

        return new ApplicationServices(
            paths,
            settingsStore,
            projectStore,
            catalog,
            scanner,
            videoRenderer,
            compositionExporter,
            plugins);
    }
}
