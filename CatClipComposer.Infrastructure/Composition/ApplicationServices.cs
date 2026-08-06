using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure.Composition;

public sealed record ApplicationServices(
    AppPaths Paths,
    ISettingsStore SettingsStore,
    IMediaCatalog Catalog,
    IMediaScanner Scanner,
    ICompositionExporter CompositionExporter);
