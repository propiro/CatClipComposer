using CatClipComposer.Core.Services;
using CatClipComposer.Infrastructure.Configuration;
using CatClipComposer.Infrastructure.Rendering;

namespace CatClipComposer.Infrastructure.Composition;

public static class ApplicationServicesFactory
{
    public static async Task<ApplicationServices> CreateAsync(
        string? dataFolder = null,
        string? configurationPath = null,
        CancellationToken cancellationToken = default)
    {
        var paths = new AppPaths(dataFolder, configurationPath);
        paths.EnsureCreated();

        ISettingsStore settingsStore = new IniSettingsStore(paths);
        IMediaCatalog catalog = new SqliteMediaCatalog(paths);
        await catalog.InitializeAsync(cancellationToken);

        IMediaProbe mediaProbe = new FfprobeMediaProbe();
        IThumbnailGenerator thumbnailGenerator = new FfmpegThumbnailGenerator(paths);
        IMediaScanner scanner = new MediaScanner(catalog, mediaProbe, thumbnailGenerator);
        IVideoRenderer videoRenderer = new FfmpegVideoRenderer();

        return new ApplicationServices(
            paths,
            settingsStore,
            catalog,
            scanner,
            videoRenderer);
    }
}
