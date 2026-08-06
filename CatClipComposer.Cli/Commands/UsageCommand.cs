using System.Globalization;
using CatClipComposer.Cli.CommandLine;

namespace CatClipComposer.Cli.Commands;

internal static class UsageCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions("config", "data", "json", "help", "clip");
        var clipValue = invocation.GetSingleValue("clip");
        if (!long.TryParse(clipValue, NumberStyles.None, CultureInfo.InvariantCulture, out var mediaId) ||
            mediaId <= 0)
        {
            throw new CliUsageException("The usage command requires '--clip <catalog-id>'.");
        }

        var entries = await context.Services.Catalog.GetUsageAsync(mediaId, context.CancellationToken);
        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, new { mediaId, count = entries.Count, items = entries });
        }
        else
        {
            await context.Output.WriteLineAsync($"Completed export uses for clip {mediaId}: {entries.Count}");
            foreach (var entry in entries)
            {
                await context.Output.WriteLineAsync(
                    $"{entry.ExportedUtc:u}  {entry.ProjectName ?? "(unnamed project)"}  " +
                    $"x{entry.Occurrences}  {entry.OutputPath}");
            }
        }

        return CliExitCodes.Success;
    }
}
