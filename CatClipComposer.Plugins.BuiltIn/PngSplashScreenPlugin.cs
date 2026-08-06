using CatClipComposer.Core.Models;
using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Plugins.BuiltIn;

public sealed class PngSplashScreenPlugin : ICatClipSourcePlugin
{
    public PluginDescriptor Descriptor { get; } = new(
        BuiltInPluginIds.PngSplashScreen,
        "PNG splash screen",
        "1.0.0",
        "Loads a PNG as a timed still-image source on a video timeline.",
        PluginMediaType.Image,
        PluginRenderStage.Source,
        [ProjectTrackKind.Video],
        []);

    public bool CanOpen(string sourcePath) =>
        Path.GetExtension(sourcePath).Equals(".png", StringComparison.OrdinalIgnoreCase);

    public RenderSegmentKind ResolveSourceKind(string sourcePath) =>
        CanOpen(sourcePath)
            ? RenderSegmentKind.StillImage
            : throw new InvalidOperationException($"The PNG splash module cannot open '{sourcePath}'.");
}
