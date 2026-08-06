using CatClipComposer.Cli.CommandLine;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Cli.Commands;

internal static class ProjectCommand
{
    public static async Task<int> ExecuteAsync(
        CliInvocation invocation,
        CliCommandContext context)
    {
        invocation.EnsureOnlyOptions(
            "config", "data", "json", "help", "project-file", "project-name",
            "create", "overwrite");
        var fileOption = invocation.GetSingleValue("project-file");
        if (string.IsNullOrWhiteSpace(fileOption))
        {
            throw new CliUsageException("The project command requires '--project-file <file>'.");
        }

        var projectPath = Path.GetFullPath(fileOption);
        EditorProject project;
        if (invocation.HasOption("create"))
        {
            if (File.Exists(projectPath) && !invocation.HasOption("overwrite"))
            {
                throw new CliUsageException(
                    $"Project already exists: {projectPath}. Add '--overwrite' to replace it.");
            }

            var projectName = invocation.GetSingleValue("project-name");
            project = EditorProject.Create(
                string.IsNullOrWhiteSpace(projectName)
                    ? Path.GetFileNameWithoutExtension(projectPath)
                    : projectName.Trim(),
                new ProjectOutputSettings
                {
                    Width = 1920,
                    Height = 1080,
                    VideoEncoder = VideoEncoderPreset.NativeMpeg4
                });
            project.ProjectFilePath = projectPath;
            await context.Services.ProjectStore.SaveAsync(
                project,
                projectPath,
                context.CancellationToken);
        }
        else
        {
            if (invocation.GetSingleValue("project-name") is not null ||
                invocation.HasOption("overwrite"))
            {
                throw new CliUsageException(
                    "Options '--project-name' and '--overwrite' require '--create'.");
            }

            project = await context.Services.ProjectStore.LoadAsync(
                projectPath,
                context.CancellationToken);
        }

        var response = new
        {
            status = invocation.HasOption("create") ? "created" : "loaded",
            project.SchemaVersion,
            project.Id,
            project.Name,
            project.CreatedUtc,
            project.ModifiedUtc,
            project.ProjectFilePath,
            output = project.Output,
            tracks = project.Tracks
                .OrderBy(track => track.Order)
                .Select(track => new
                {
                    track.Id,
                    track.Name,
                    kind = track.Kind.ToString(),
                    track.Order,
                    track.IsEnabled,
                    track.IsLocked,
                    itemCount = track.Items.Count
                })
        };
        if (context.Json)
        {
            await CliJson.WriteAsync(context.Output, response);
        }
        else
        {
            await context.Output.WriteLineAsync(
                $"Project {response.status}: {project.Name} ({project.Id})");
            await context.Output.WriteLineAsync($"File: {project.ProjectFilePath}");
            await context.Output.WriteLineAsync(
                $"Output: {project.Output.Width}x{project.Output.Height} @ " +
                $"{project.Output.FramesPerSecond:0.###} fps, {project.Output.VideoEncoder}");
            foreach (var track in project.Tracks.OrderBy(track => track.Order))
            {
                await context.Output.WriteLineAsync(
                    $"  {track.Order}: {track.Name} [{track.Kind}] - {track.Items.Count} item(s)");
            }
        }

        return CliExitCodes.Success;
    }
}
