using CatClipComposer.Core.Models;
using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Plugins.BuiltIn;

public sealed class BackgroundBlurPlugin : ICatClipVideoEffectPlugin
{
    public PluginDescriptor Descriptor { get; } = new(
        BuiltInPluginIds.BackgroundBlur,
        "Blur content background",
        "1.0.0",
        "Fills unused project space with a zoomed, color-adjusted Gaussian blur of the active visual source.",
        PluginMediaType.Video | PluginMediaType.Image | PluginMediaType.Background,
        PluginRenderStage.Background,
        [ProjectTrackKind.Background],
        [
            new("saturation", "Saturation", PluginParameterType.Number, "1", 0, 3),
            new("lightness", "Lightness", PluginParameterType.Number, "0", -1, 1),
            new("hue", "Hue rotation (degrees)", PluginParameterType.Number, "0", -180, 180),
            new("zoom", "Background zoom", PluginParameterType.Number, "1.15", 1, 3),
            new("blur", "Gaussian blur", PluginParameterType.Number, "32", 0, 100)
        ]);

    public string BuildFilterGraph(
        PluginVideoFilterContext context,
        IReadOnlyDictionary<string, string> parameters)
    {
        var saturation = PluginValues.Number(parameters, "saturation", 1, 0, 3);
        var lightness = PluginValues.Number(parameters, "lightness", 0, -1, 1);
        var hue = PluginValues.Number(parameters, "hue", 0, -180, 180);
        var zoom = PluginValues.Number(parameters, "zoom", 1.15, 1, 3);
        var blur = PluginValues.Number(parameters, "blur", 32, 0, 100);
        var backgroundWidth = MakeEven((int)Math.Ceiling(context.Width * zoom));
        var backgroundHeight = MakeEven((int)Math.Ceiling(context.Height * zoom));
        var start = PluginValues.Format(Math.Max(0, context.EffectStart.TotalSeconds));
        var end = PluginValues.Format(Math.Max(
            context.EffectStart.TotalSeconds,
            (context.EffectStart + context.EffectDuration).TotalSeconds));
        var prefix = context.OutputLabel;

        return
            $"[{context.InputLabel}]split=3[{prefix}plain][{prefix}bg][{prefix}fg];" +
            $"[{prefix}plain]scale={context.Width}:{context.Height}:force_original_aspect_ratio=decrease," +
            $"pad={context.Width}:{context.Height}:(ow-iw)/2:(oh-ih)/2:color={PluginValues.Color(context.BackgroundColor)}[{prefix}base];" +
            $"[{prefix}bg]scale={backgroundWidth}:{backgroundHeight}:force_original_aspect_ratio=increase," +
            $"crop={context.Width}:{context.Height},hue=h={PluginValues.Format(hue)}:" +
            $"s={PluginValues.Format(saturation)}:b={PluginValues.Format(lightness)}," +
            $"gblur=sigma={PluginValues.Format(blur)}[{prefix}back];" +
            $"[{prefix}fg]scale={context.Width}:{context.Height}:force_original_aspect_ratio=decrease[{prefix}front];" +
            $"[{prefix}back][{prefix}front]overlay=(W-w)/2:(H-h)/2:shortest=1[{prefix}blurred];" +
            $"[{prefix}base][{prefix}blurred]overlay=0:0:enable='between(t,{start},{end})'[{context.OutputLabel}];";
    }

    private static int MakeEven(int value) => value % 2 == 0 ? value : value + 1;
}
