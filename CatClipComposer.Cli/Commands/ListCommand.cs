using CatClipComposer.Cli.CommandLine;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Cli.Commands;

internal static class ListCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions("config", "data", "json", "help", "all");
        var includeUnavailable = invocation.HasOption("all");
        var media = await context.Services.Catalog.GetAllAsync(
            includeUnavailable,
            context.CancellationToken);

        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, new
            {
                count = media.Count,
                includeUnavailable,
                items = media.Select(item => new
                {
                    item.Id,
                    item.FileName,
                    item.FullPath,
                    duration = item.Duration,
                    item.Width,
                    item.Height,
                    item.HasAudio,
                    item.IsAvailable,
                    item.UseCount,
                    item.LastUsedUtc,
                    item.LastOutputPath
                })
            });
            return CliExitCodes.Success;
        }

        await context.Output.WriteLineAsync($"Catalog items: {media.Count}");
        foreach (var item in media)
        {
            var availability = item.IsAvailable ? string.Empty : " [unavailable]";
            await context.Output.WriteLineAsync(
                $"{item.Id,6}  {DurationFormatter.Format(item.Duration),8}  " +
                $"{item.Width}x{item.Height}  used {item.UseCount,3}  {item.FileName}{availability}");
            await context.Output.WriteLineAsync($"        {item.FullPath}");
        }

        return CliExitCodes.Success;
    }
}
