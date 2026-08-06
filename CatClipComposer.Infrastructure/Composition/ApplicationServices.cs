using CatClipComposer.Core.Services;
using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Infrastructure.Composition;

public sealed record ApplicationServices(
    AppPaths Paths,
    ISettingsStore SettingsStore,
    IProjectStore ProjectStore,
    IMediaCatalog Catalog,
    IMediaScanner Scanner,
    ICompositionExporter CompositionExporter,
    IPluginCatalog Plugins);
