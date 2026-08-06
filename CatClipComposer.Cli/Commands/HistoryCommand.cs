using CatClipComposer.Cli.CommandLine;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Cli.Commands;

internal static class HistoryCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions("config", "data", "json", "help");
        var history = await context.Services.Catalog.GetExportHistoryAsync(context.CancellationToken);

        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, new
            {
                count = history.Count,
                items = history.Select(entry => new
                {
                    entry.Id,
                    entry.OutputPath,
                    entry.Duration,
                    entry.CreatedUtc,
                    entry.ProjectName,
                    entry.ProjectFilePath,
                    clips = entry.Clips.Select(clip => new
                    {
                        clip.Order,
                        clip.MediaFileId,
                        clip.FileName,
                        clip.FullPath
                    })
                })
            });
            return CliExitCodes.Success;
        }

        await context.Output.WriteLineAsync($"Completed exports: {history.Count}");
        foreach (var entry in history)
        {
            await context.Output.WriteLineAsync(
                $"{entry.Id,6}  {entry.CreatedUtc:u}  {DurationFormatter.Format(entry.Duration)}  " +
                $"{entry.ProjectName ?? "(unnamed project)"}  {entry.OutputPath}");
            foreach (var clip in entry.Clips)
            {
                await context.Output.WriteLineAsync(
                    $"        {clip.Order,3}. [{clip.MediaFileId}] {clip.FileName}");
            }
        }

        return CliExitCodes.Success;
    }
}
