using System.Globalization;
using System.Text;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure.Rendering;

public sealed class FfmpegVideoRenderer : IVideoRenderer
{
    private readonly FfmpegRenderCommandBuilder _commandBuilder = new();
    private readonly FfmpegProcessRunner _processRunner = new();

    public async Task<RenderResult> RenderAsync(
        RenderRequest request,
        string ffmpegPath,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var outputDirectory = Path.GetDirectoryName(request.OutputPath)!;
        Directory.CreateDirectory(outputDirectory);

        var operationId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var temporaryOutputPath = CreateTemporaryOutputPath(request.OutputPath, operationId);
        string? overlayTextPath = null;
        IReadOnlyDictionary<int, string> timedTextPaths = new Dictionary<int, string>();
        var totalDuration = TimeSpan.FromTicks(request.Segments.Sum(segment => segment.Duration.Ticks));
        var (fallbackWidth, fallbackHeight) = request.Orientation == OutputOrientation.Portrait
            ? (1080, 1920)
            : (1920, 1080);
        var width = request.OutputWidth > 0 ? request.OutputWidth : fallbackWidth;
        var height = request.OutputHeight > 0 ? request.OutputHeight : fallbackHeight;

        try
        {
            overlayTextPath = await CreateOverlayTextFileAsync(
                request,
                outputDirectory,
                operationId,
                cancellationToken);
            timedTextPaths = await CreateTimedOverlayTextFilesAsync(
                request,
                outputDirectory,
                operationId,
                cancellationToken);
            var startInfo = _commandBuilder.Build(
                request,
                ffmpegPath,
                temporaryOutputPath,
                overlayTextPath,
                timedTextPaths,
                width,
                height);
            var processResult = await _processRunner.RunAsync(
                startInfo,
                totalDuration,
                progress,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (processResult.ExitCode != 0 || !File.Exists(temporaryOutputPath))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(processResult.StandardError)
                        ? $"FFmpeg stopped with exit code {processResult.ExitCode}."
                        : processResult.StandardError.Trim());
            }

            File.Move(temporaryOutputPath, request.OutputPath, overwrite: true);
            progress?.Report(new RenderProgress(
                100,
                totalDuration,
                totalDuration,
                "Compilation complete"));
            return new RenderResult(request.OutputPath, totalDuration);
        }
        finally
        {
            TemporaryFile.TryDelete(temporaryOutputPath);
            TemporaryFile.TryDelete(overlayTextPath);
            foreach (var path in timedTextPaths.Values)
            {
                TemporaryFile.TryDelete(path);
            }
        }
    }

    private static void ValidateRequest(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Segments.Count == 0)
        {
            throw new ArgumentException("At least one timeline segment is required.", nameof(request));
        }

        var invalidSegment = request.Segments.FirstOrDefault(segment =>
            segment.Duration <= TimeSpan.Zero ||
            !File.Exists(segment.SourcePath) ||
            !double.IsFinite(segment.FadeInSeconds) || segment.FadeInSeconds < 0 ||
            !double.IsFinite(segment.FadeOutSeconds) || segment.FadeOutSeconds < 0 ||
            !double.IsFinite(segment.Volume) || segment.Volume < 0);
        if (invalidSegment is not null)
        {
            throw new InvalidOperationException(
                $"A timeline source is missing or has no duration: {invalidSegment.SourcePath}");
        }

        if (!string.IsNullOrWhiteSpace(request.OverlayImagePath) && !File.Exists(request.OverlayImagePath))
        {
            throw new InvalidOperationException($"The overlay image does not exist: {request.OverlayImagePath}");
        }

        if (!string.IsNullOrWhiteSpace(request.OverlayFontPath) && !File.Exists(request.OverlayFontPath))
        {
            throw new InvalidOperationException($"The overlay font does not exist: {request.OverlayFontPath}");
        }

        if (request.FramesPerSecond is < 1 or > 240 || !double.IsFinite(request.FramesPerSecond))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Frame rate must be between 1 and 240.");
        }

        if ((request.OutputWidth == 0) != (request.OutputHeight == 0) ||
            request.OutputWidth < 0 || request.OutputHeight < 0 ||
            request.OutputWidth > 7680 || request.OutputHeight > 7680 ||
            request.OutputWidth % 2 != 0 || request.OutputHeight % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Custom output dimensions must be even numbers no larger than 7680.");
        }

        foreach (var overlay in request.TimedOverlays ?? [])
        {
            if (overlay.Start < TimeSpan.Zero || overlay.Duration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Timed overlays require a non-negative start and positive duration.");
            }

            if (overlay.Kind == RenderOverlayKind.Text && string.IsNullOrWhiteSpace(overlay.Text))
            {
                throw new InvalidOperationException("A timed text overlay cannot be empty.");
            }

            if (overlay.Kind == RenderOverlayKind.Image &&
                (string.IsNullOrWhiteSpace(overlay.SourcePath) || !File.Exists(overlay.SourcePath)))
            {
                throw new InvalidOperationException($"A timed overlay image is missing: {overlay.SourcePath}");
            }

            if (!string.IsNullOrWhiteSpace(overlay.FontPath) && !File.Exists(overlay.FontPath))
            {
                throw new InvalidOperationException($"A timed overlay font is missing: {overlay.FontPath}");
            }
        }

        foreach (var layer in request.AudioLayers ?? [])
        {
            if (!File.Exists(layer.SourcePath) || layer.Start < TimeSpan.Zero || layer.Duration <= TimeSpan.Zero ||
                !double.IsFinite(layer.Volume) || layer.Volume < 0 ||
                !double.IsFinite(layer.FadeInSeconds) || layer.FadeInSeconds < 0 ||
                !double.IsFinite(layer.FadeOutSeconds) || layer.FadeOutSeconds < 0)
            {
                throw new InvalidOperationException($"An audio layer is missing or has invalid timing: {layer.SourcePath}");
            }
        }

        if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(request.OutputPath)))
        {
            throw new ArgumentException("The output path must include a folder.", nameof(request));
        }
    }

    private static string CreateTemporaryOutputPath(string outputPath, string operationId)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath)!;
        return Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(outputPath)}.{operationId}.partial{Path.GetExtension(outputPath)}");
    }

    private static async Task<string?> CreateOverlayTextFileAsync(
        RenderRequest request,
        string outputDirectory,
        string operationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OverlayText))
        {
            return null;
        }

        var path = Path.Combine(outputDirectory, $".overlay-{operationId}.txt");
        await File.WriteAllTextAsync(
            path,
            request.OverlayText,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        return path;
    }

    private static async Task<IReadOnlyDictionary<int, string>> CreateTimedOverlayTextFilesAsync(
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

                var path = Path.Combine(outputDirectory, $".overlay-{operationId}-{index}.txt");
                await File.WriteAllTextAsync(
                    path,
                    overlay.Text,
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
