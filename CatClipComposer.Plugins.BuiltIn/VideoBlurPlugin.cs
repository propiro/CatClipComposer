using CatClipComposer.Core.Models;
using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Plugins.BuiltIn;

public sealed class VideoBlurPlugin : ICatClipVideoEffectPlugin
{
    public PluginDescriptor Descriptor { get; } = new(
        BuiltInPluginIds.VideoBlur,
        "Video blur",
        "1.0.0",
        "Applies a timed Gaussian blur to the composited video pixels.",
        PluginMediaType.Video,
        PluginRenderStage.Filter,
        [ProjectTrackKind.Video, ProjectTrackKind.Effects],
        [new("blur", "Gaussian blur", PluginParameterType.Number, "12", 0, 100)]);

    public string BuildFilterGraph(
        PluginVideoFilterContext context,
        IReadOnlyDictionary<string, string> parameters)
    {
        var blur = PluginValues.Number(parameters, "blur", 12, 0, 1000);
        var start = PluginValues.Format(context.EffectStart.TotalSeconds);
        var end = PluginValues.Format((context.EffectStart + context.EffectDuration).TotalSeconds);
        return $"[{context.InputLabel}]gblur=sigma={PluginValues.Format(blur)}:" +
               $"enable='between(t,{start},{end})'[{context.OutputLabel}];";
    }
}
