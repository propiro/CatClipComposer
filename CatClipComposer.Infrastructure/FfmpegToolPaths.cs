namespace CatClipComposer.Infrastructure;

internal static class FfmpegToolPaths
{
    public static string ResolveFfmpeg(string configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath) ? "ffmpeg.exe" : configuredPath;

    public static string ResolveFfprobe(string configuredFfmpegPath)
    {
        var ffmpegPath = ResolveFfmpeg(configuredFfmpegPath);
        var directory = Path.GetDirectoryName(ffmpegPath);
        var extension = Path.GetExtension(ffmpegPath);
        var ffprobeName = string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
            ? "ffprobe.exe"
            : "ffprobe";

        return string.IsNullOrWhiteSpace(directory)
            ? ffprobeName
            : Path.Combine(directory, ffprobeName);
    }
}
