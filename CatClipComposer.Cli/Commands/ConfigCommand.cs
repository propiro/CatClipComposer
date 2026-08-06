using System.Globalization;
using CatClipComposer.Cli.CommandLine;

namespace CatClipComposer.Cli.Commands;

internal static class ConfigCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions("config", "data", "json", "help");
        var settings = context.Settings;
        var response = new
        {
            configurationPath = context.Services.Paths.ConfigurationPath,
            configurationExists = File.Exists(context.Services.Paths.ConfigurationPath),
            dataFolder = context.Services.Paths.DataFolder,
            databasePath = context.Services.Paths.DatabasePath,
            settings = new
            {
                sourceFolders = settings.SourceFolders,
                settings.MetadataFolder,
                settings.PreviewSlideCount,
                settings.IncludeSubfolders,
                settings.ShowFileNames,
                settings.RescanLibraryOnStartup,
                settings.OutputFolder,
                settings.ProjectFolder,
                settings.FfmpegPath,
                settings.CustomFontFolder,
                workspace = new
                {
                    contentBrowserDock = settings.ContentBrowserDock.ToString(),
                    previewDock = settings.PreviewDock.ToString(),
                    layersDock = settings.LayersDock.ToString(),
                    timelineDock = settings.TimelineDock.ToString()
                }
            }
        };

        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, response);
            return CliExitCodes.Success;
        }

        await context.Output.WriteLineAsync($"Configuration: {response.configurationPath}");
        await context.Output.WriteLineAsync($"Configuration exists: {response.configurationExists}");
        await context.Output.WriteLineAsync($"Data folder: {response.dataFolder}");
        await context.Output.WriteLineAsync($"Database: {response.databasePath}");
        await context.Output.WriteLineAsync($"Source folders: {settings.SourceFolders.Count.ToString(CultureInfo.InvariantCulture)}");
        foreach (var folder in settings.SourceFolders)
        {
            await context.Output.WriteLineAsync($"  {folder}");
        }

        await context.Output.WriteLineAsync($"Output folder: {settings.OutputFolder}");
        await context.Output.WriteLineAsync($"Project folder: {settings.ProjectFolder}");
        await context.Output.WriteLineAsync($"Metadata folder: {settings.MetadataFolder}");
        await context.Output.WriteLineAsync($"Preview slides: {settings.PreviewSlideCount}");
        await context.Output.WriteLineAsync($"FFmpeg: {settings.FfmpegPath}");
        await context.Output.WriteLineAsync($"Custom fonts: {settings.CustomFontFolder}");
        await context.Output.WriteLineAsync($"Rescan on startup: {settings.RescanLibraryOnStartup}");
        await context.Output.WriteLineAsync(
            $"Workspace: browser={settings.ContentBrowserDock}, preview={settings.PreviewDock}, " +
            $"layers={settings.LayersDock}, timeline={settings.TimelineDock}");
        return CliExitCodes.Success;
    }
}
