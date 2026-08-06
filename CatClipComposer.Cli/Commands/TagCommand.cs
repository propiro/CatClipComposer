using System.Globalization;
using CatClipComposer.Cli.CommandLine;

namespace CatClipComposer.Cli.Commands;

internal static class TagCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions(
            "config", "data", "json", "help", "clip", "tags", "clear-tags");
        var clipValue = invocation.GetSingleValue("clip");
        if (!long.TryParse(clipValue, NumberStyles.None, CultureInfo.InvariantCulture, out var mediaId) ||
            mediaId <= 0)
        {
            throw new CliUsageException("The tag command requires '--clip <catalog-id>'.");
        }

        var clear = invocation.HasOption("clear-tags");
        var tags = invocation.GetSingleValue("tags");
        if (clear == (tags is not null))
        {
            throw new CliUsageException("Specify exactly one of '--tags <values>' or '--clear-tags'.");
        }

        await context.Services.Catalog.UpdateTagsAsync(
            mediaId,
            clear ? string.Empty : tags!,
            context.CancellationToken);
        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, new
            {
                status = "success",
                mediaId,
                tags = clear ? string.Empty : tags
            });
        }
        else
        {
            await context.Output.WriteLineAsync(clear
                ? $"Cleared tags for catalog clip {mediaId}."
                : $"Saved tags for catalog clip {mediaId}.");
        }

        return CliExitCodes.Success;
    }
}
