using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Plugins;

public static class PluginContract
{
    public const int CurrentApiVersion = 1;
}

public static class BuiltInPluginIds
{
    public const string BackgroundBlur = "catclip.background.blur";
    public const string VideoBlur = "catclip.video.blur";
    public const string PngSplashScreen = "catclip.image.png-splash";
}

[Flags]
public enum PluginMediaType
{
    None = 0,
    Video = 1,
    Audio = 2,
    Image = 4,
    Overlay = 8,
    Background = 16
}

public enum PluginRenderStage
{
    Source,
    Background,
    Filter,
    Overlay
}

public enum PluginParameterType
{
    Number,
    Color,
    Text,
    Boolean,
    Choice,
    File
}

public sealed record PluginParameterDefinition(
    string Key,
    string DisplayName,
    PluginParameterType Type,
    string DefaultValue,
    double? Minimum = null,
    double? Maximum = null,
    IReadOnlyList<string>? Choices = null,
    string? Description = null);

public sealed record PluginDescriptor(
    string Id,
    string Name,
    string Version,
    string Description,
    PluginMediaType MediaTypes,
    PluginRenderStage Stage,
    IReadOnlyList<ProjectTrackKind> CompatibleTracks,
    IReadOnlyList<PluginParameterDefinition> Parameters);

public sealed record PluginVideoFilterContext(
    string InputLabel,
    string OutputLabel,
    int Width,
    int Height,
    double FramesPerSecond,
    TimeSpan EffectStart,
    TimeSpan EffectDuration,
    string BackgroundColor);

public sealed record PluginAudioFilterContext(
    string InputLabel,
    string OutputLabel,
    double SampleRate,
    TimeSpan EffectStart,
    TimeSpan EffectDuration);

public interface ICatClipPlugin
{
    int ApiVersion => PluginContract.CurrentApiVersion;

    PluginDescriptor Descriptor { get; }
}

public interface ICatClipVideoEffectPlugin : ICatClipPlugin
{
    string BuildFilterGraph(
        PluginVideoFilterContext context,
        IReadOnlyDictionary<string, string> parameters);
}

public interface ICatClipAudioEffectPlugin : ICatClipPlugin
{
    string BuildFilterGraph(
        PluginAudioFilterContext context,
        IReadOnlyDictionary<string, string> parameters);
}

public interface ICatClipSourcePlugin : ICatClipPlugin
{
    bool CanOpen(string sourcePath);

    RenderSegmentKind ResolveSourceKind(string sourcePath);
}

public interface ICatClipOverlayPlugin : ICatClipVideoEffectPlugin;

public interface IPluginCatalog
{
    IReadOnlyList<ICatClipPlugin> Plugins { get; }

    IReadOnlyList<string> Diagnostics { get; }

    ICatClipPlugin? Find(string pluginId);
}
