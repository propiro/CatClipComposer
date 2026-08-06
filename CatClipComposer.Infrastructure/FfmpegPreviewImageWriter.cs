using System.Diagnostics;

namespace CatClipComposer.Infrastructure;

internal static class FfmpegPreviewImageWriter
{
    public static async Task<bool> RunAsync(
        string ffmpegPath,
        IEnumerable<string> arguments,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FfmpegToolPaths.ResolveFfmpeg(ffmpegPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return false;
        }
    }
}
