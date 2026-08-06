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
        var overlayTextPath = await CreateOverlayTextFileAsync(
            request,
            outputDirectory,
            operationId,
            cancellationToken);
        var totalDuration = TimeSpan.FromTicks(request.Segments.Sum(segment => segment.Duration.Ticks));
        var (width, height) = request.Orientation == OutputOrientation.Portrait
            ? (1080, 1920)
            : (1920, 1080);

        try
        {
            var startInfo = _commandBuilder.Build(
                request,
                ffmpegPath,
                temporaryOutputPath,
                overlayTextPath,
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
            segment.Duration <= TimeSpan.Zero || !File.Exists(segment.SourcePath));
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
}
