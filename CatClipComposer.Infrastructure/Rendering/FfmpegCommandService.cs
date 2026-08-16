using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure.Rendering;

public sealed class FfmpegCommandService : IFfmpegCommandService
{
    private const int CapturedOutputCharacterLimit = 64 * 1024;
    private readonly FfmpegRenderCommandBuilder _commandBuilder = new();

    public async Task<FfmpegCommandPreview> CreateAsync(
        RenderRequest request,
        string ffmpegPath,
        string supportingFilesRoot,
        CancellationToken cancellationToken = default)
    {
        FfmpegVideoRenderer.ValidateRequest(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(supportingFilesRoot);

        var executablePath = ResolveRequiredExecutable(ffmpegPath);
        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException($"FFmpeg was not found: {executablePath}");
        }

        var operationId = Guid.NewGuid().ToString("N");
        var supportingFilesFolder = Path.Combine(
            Path.GetFullPath(supportingFilesRoot),
            operationId);
        var timedTextPaths = await FfmpegTimedTextFileWriter.CreateAsync(
            request,
            supportingFilesFolder,
            operationId,
            cancellationToken);
        var (width, height) = FfmpegVideoRenderer.ResolveOutputDimensions(request);
        var startInfo = _commandBuilder.Build(
            request,
            executablePath,
            request.OutputPath,
            timedTextPaths,
            width,
            height);
        return new FfmpegCommandPreview(
            WindowsCommandLine.Format(startInfo),
            request.OutputPath,
            timedTextPaths.Count == 0 ? null : supportingFilesFolder);
    }

    public async Task<FfmpegCommandExecutionResult> ExecuteAsync(
        string commandText,
        string requiredFfmpegPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        var arguments = WindowsCommandLine.Parse(commandText);
        if (arguments.Count == 0)
        {
            throw new InvalidOperationException("The FFmpeg command is empty.");
        }

        var requiredExecutable = ResolveRequiredExecutable(requiredFfmpegPath);
        var editedExecutable = Path.GetFullPath(arguments[0]);
        if (!editedExecutable.Equals(requiredExecutable, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The executable must remain the configured FFmpeg path:{Environment.NewLine}{requiredExecutable}");
        }

        if (!File.Exists(requiredExecutable))
        {
            throw new InvalidOperationException($"FFmpeg was not found: {requiredExecutable}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = requiredExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments.Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException($"FFmpeg could not be started: {requiredExecutable}", exception);
        }

        using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));
        var outputTask = ReadTailAsync(
            process.StandardOutput,
            CapturedOutputCharacterLimit,
            cancellationToken);
        var errorTask = ReadTailAsync(
            process.StandardError,
            CapturedOutputCharacterLimit,
            cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new FfmpegCommandExecutionResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static string ResolveRequiredExecutable(string configuredPath) =>
        Path.GetFullPath(global::CatClipComposer.Infrastructure.FfmpegToolPaths.ResolveFfmpeg(configuredPath));

    private static async Task<string> ReadTailAsync(
        StreamReader reader,
        int characterLimit,
        CancellationToken cancellationToken)
    {
        var result = new StringBuilder(Math.Min(characterLimit, 4096));
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            result.Append(buffer, 0, read);
            if (result.Length > characterLimit)
            {
                result.Remove(0, result.Length - characterLimit);
            }
        }

        return result.ToString();
    }

    private static void TryKill(Process process)
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
    }

    private static class WindowsCommandLine
    {
        public static string Format(ProcessStartInfo startInfo) => string.Join(
            " ",
            new[] { startInfo.FileName }
                .Concat(startInfo.ArgumentList)
                .Select(Quote));

        public static IReadOnlyList<string> Parse(string commandLine)
        {
            var argumentPointer = CommandLineToArgvW(commandLine.Trim(), out var argumentCount);
            if (argumentPointer == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The edited FFmpeg command could not be parsed.");
            }

            try
            {
                var arguments = new string[argumentCount];
                for (var index = 0; index < argumentCount; index++)
                {
                    var valuePointer = Marshal.ReadIntPtr(argumentPointer, index * IntPtr.Size);
                    arguments[index] = Marshal.PtrToStringUni(valuePointer) ?? string.Empty;
                }

                return arguments;
            }
            finally
            {
                LocalFree(argumentPointer);
            }
        }

        private static string Quote(string argument)
        {
            var result = new StringBuilder(argument.Length + 2);
            result.Append('"');
            var backslashes = 0;
            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(character);
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW(string commandLine, out int argumentCount);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
