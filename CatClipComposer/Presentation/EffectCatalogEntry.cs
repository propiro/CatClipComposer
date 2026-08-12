using CatClipComposer.Core.Models;

namespace CatClipComposer.Presentation;

public sealed record EffectCatalogEntry(
    string Category,
    string Name,
    string Description,
    ProjectTrackKind TrackKind,
    LayerEditorKind? LayerKind = null,
    string? PluginId = null);
