using System.Globalization;
using CatClipComposer.Cli.CommandLine;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Cli.Commands;

internal static class RenderCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions(
            "config", "data", "json", "help", "output", "orientation", "encoder",
            "clip", "screen", "overwrite", "project-file", "project-name");
        var outputOption = invocation.GetSingleValue("output");
        if (string.IsNullOrWhiteSpace(outputOption))
        {
            throw new CliUsageException("The render command requires '--output <file>'.");
        }

        var outputPath = ResolveOutputPath(outputOption, context.Settings.OutputFolder);
        if (File.Exists(outputPath) && !invocation.HasOption("overwrite"))
        {
            throw new CliUsageException(
                $"Output already exists: {outputPath}. Add '--overwrite' to replace it.");
        }

        var orientation = ParseOrientation(
            invocation.GetSingleValue("orientation"),
            context.Settings.Orientation);
        var encoder = ParseEncoder(
            invocation.GetSingleValue("encoder"),
            context.Settings.VideoEncoder);
        var segments = await CreateSegmentsAsync(invocation, context);
        if (segments.Count == 0)
        {
            throw new CliUsageException(
                "Add at least one ordered '--clip <catalog-id>' or '--screen \"<seconds>|<image-path>\"' option.");
        }

        IProgress<RenderProgress>? progress = context.Json
            ? null
            : new InlineProgress<RenderProgress>(update =>
                context.Error.WriteLine(
                    $"Render {update.Percent,6:0.0}%  {update.ProcessedDuration:hh\\:mm\\:ss}  {update.Message}"));
        var projectName = invocation.GetSingleValue("project-name");
        string? projectFilePath = null;
        var projectFileOption = invocation.GetSingleValue("project-file");
        if (!string.IsNullOrWhiteSpace(projectFileOption))
        {
            projectFilePath = ResolvePath(projectFileOption, "project-file");
            if (!File.Exists(projectFilePath))
            {
                throw new CliUsageException($"Project file does not exist: {projectFilePath}");
            }

            var project = await context.Services.ProjectStore.LoadAsync(
                projectFilePath,
                context.CancellationToken);
            projectName ??= project.Name;
        }

        var request = new RenderRequest(
            segments,
            outputPath,
            orientation,
            context.Settings.ProgressStyle,
            context.Settings.OverlayImagePath,
            context.Settings.OverlayText,
            context.Settings.OverlayFontPath,
            context.Settings.OverlayTextSize,
            context.Settings.OverlayPosition,
            encoder,
            ProjectName: projectName,
            ProjectFilePath: projectFilePath);
        var result = await context.Services.CompositionExporter.ExportAsync(
            request,
            context.Settings.FfmpegPath,
            progress,
            context.CancellationToken);

        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, new
            {
                status = "success",
                exitCode = CliExitCodes.Success,
                result.OutputPath,
                result.Duration,
                orientation = orientation.ToString(),
                encoder = encoder.ToString(),
                segmentCount = segments.Count
            });
            return CliExitCodes.Success;
        }

        await context.Output.WriteLineAsync($"Compilation saved: {result.OutputPath}");
        await context.Output.WriteLineAsync($"Duration: {result.Duration:c}");
        return CliExitCodes.Success;
    }

    private static async Task<IReadOnlyList<RenderSegment>> CreateSegmentsAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        var catalog = await context.Services.Catalog.GetAllAsync(
            includeUnavailable: false,
            context.CancellationToken);
        var mediaById = catalog.ToDictionary(media => media.Id);
        var segments = new List<RenderSegment>();

        foreach (var option in invocation.Options.Where(option => option.Name is "clip" or "screen"))
        {
            if (option.Name == "clip")
            {
                if (!long.TryParse(
                        option.Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var mediaId) ||
                    !mediaById.TryGetValue(mediaId, out var media))
                {
                    throw new CliUsageException(
                        $"Catalog clip '{option.Value}' is not an available media ID. Run 'list' to inspect IDs.");
                }

                segments.Add(new RenderSegment(
                    RenderSegmentKind.Video,
                    media.FullPath,
                    media.Duration,
                    media.HasAudio,
                    media.Id));
                continue;
            }

            segments.Add(ParseStillScreen(option.Value!));
        }

        return segments;
    }

    private static RenderSegment ParseStillScreen(string value)
    {
        var separatorIndex = value.IndexOf('|');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1 ||
            !double.TryParse(
                value[..separatorIndex],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds) ||
            !double.IsFinite(seconds) ||
            seconds <= 0)
        {
            throw new CliUsageException(
                $"Invalid screen '{value}'. Use '--screen \"<positive-seconds>|<image-path>\"'.");
        }

        if (seconds > TimeSpan.MaxValue.TotalSeconds)
        {
            throw new CliUsageException($"Screen duration is too large: {seconds} seconds.");
        }

        var path = ResolvePath(value[(separatorIndex + 1)..], "screen");
        return new RenderSegment(
            RenderSegmentKind.StillImage,
            path,
            TimeSpan.FromSeconds(seconds),
            false);
    }

    private static string ResolveOutputPath(string value, string configuredOutputFolder)
    {
        try
        {
            var path = Path.IsPathRooted(value)
                ? value
                : Path.Combine(configuredOutputFolder, value);
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new CliUsageException($"Invalid output path '{value}': {exception.Message}");
        }
    }

    private static string ResolvePath(string value, string optionName)
    {
        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new CliUsageException($"Invalid --{optionName} path '{value}': {exception.Message}");
        }
    }

    private static OutputOrientation ParseOrientation(
        string? value,
        OutputOrientation fallback) => value?.ToLowerInvariant() switch
        {
            null => fallback,
            "landscape" => OutputOrientation.Landscape,
            "portrait" => OutputOrientation.Portrait,
            _ => throw new CliUsageException(
                $"Unknown orientation '{value}'. Use 'landscape' or 'portrait'.")
        };

    private static VideoEncoderPreset ParseEncoder(
        string? value,
        VideoEncoderPreset fallback) => value?.ToLowerInvariant() switch
        {
            null => fallback,
            "native-mpeg4" => VideoEncoderPreset.NativeMpeg4,
            "windows-h264" => VideoEncoderPreset.WindowsMediaFoundationH264,
            "libx264-gpl" => VideoEncoderPreset.Libx264Gpl,
            _ => throw new CliUsageException(
                $"Unknown encoder '{value}'. Use 'native-mpeg4', 'windows-h264', or 'libx264-gpl'.")
        };
}
