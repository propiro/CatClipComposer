using System.Text;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Infrastructure.Rendering;

internal static class FfmpegTimedTextFileWriter
{
    public static async Task<IReadOnlyDictionary<int, string>> CreateAsync(
        RenderRequest request,
        string outputDirectory,
        string operationId,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<int, string>();
        var overlays = request.TimedOverlays ?? [];
        try
        {
            for (var index = 0; index < overlays.Count; index++)
            {
                var overlay = overlays[index];
                if (overlay.Kind != RenderOverlayKind.Text || string.IsNullOrWhiteSpace(overlay.Text))
                {
                    continue;
                }

                Directory.CreateDirectory(outputDirectory);
                var path = Path.Combine(outputDirectory, $".overlay-{operationId}-{index}.txt");
                await File.WriteAllTextAsync(
                    path,
                    TextOverlayContent.NormalizeForRendering(overlay.Text),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken);
                paths[index] = path;
            }

            return paths;
        }
        catch
        {
            foreach (var path in paths.Values)
            {
                TemporaryFile.TryDelete(path);
            }

            throw;
        }
    }
}
