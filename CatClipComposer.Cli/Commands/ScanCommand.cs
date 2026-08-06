using CatClipComposer.Cli.CommandLine;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Cli.Commands;

internal static class ScanCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions("config", "data", "json", "help");
        if (context.Settings.SourceFolders.Count == 0)
        {
            throw new CliConfigurationException(
                $"No source folders are configured in '{context.Services.Paths.ConfigurationPath}'.");
        }

        IProgress<ScanProgress>? progress = context.Json
            ? null
            : new InlineProgress<ScanProgress>(update =>
                context.Error.WriteLine(
                    update.Total == 0
                        ? "Scanning catalog..."
                        : $"Scanning {Math.Min(update.Processed + 1, update.Total)}/{update.Total}: {update.CurrentFile}"));
        var result = await context.Services.Scanner.ScanAsync(
            context.Settings,
            progress,
            context.CancellationToken);
        var exitCode = result.Errors.Count == 0
            ? CliExitCodes.Success
            : CliExitCodes.CompletedWithWarnings;

        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, new
            {
                status = exitCode == CliExitCodes.Success ? "success" : "completedWithWarnings",
                exitCode,
                result.Discovered,
                result.Added,
                result.Updated,
                result.Failed,
                errors = result.Errors
            });
            return exitCode;
        }

        await context.Output.WriteLineAsync(
            $"Scan complete: {result.Discovered} discovered, {result.Added} added, " +
            $"{result.Updated} refreshed, {result.Failed} failed.");
        foreach (var error in result.Errors)
        {
            await context.Error.WriteLineAsync($"Warning: {error}");
        }

        return exitCode;
    }
}
