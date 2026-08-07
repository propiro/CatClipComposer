using System.Text.Json;
using System.Text.Json.Serialization;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Plugins;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure.Projects;

public sealed class JsonProjectStore : IProjectStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonProjectStore(AppPaths paths)
    {
        RecoveryPath = Path.Combine(paths.RecoveryFolder, "autosave.nya");
    }

    public string RecoveryPath { get; }

    public Task SaveAsync(
        EditorProject project,
        string projectPath,
        CancellationToken cancellationToken = default) =>
        WriteAtomicAsync(project, Path.GetFullPath(projectPath), cancellationToken);

    public async Task<EditorProject> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default) =>
        await ReadAsync(projectPath, setProjectPath: true, cancellationToken);

    public Task SaveRecoveryAsync(
        EditorProject project,
        CancellationToken cancellationToken = default) =>
        WriteAtomicAsync(project, RecoveryPath, cancellationToken);

    public async Task<EditorProject?> LoadRecoveryAsync(
        CancellationToken cancellationToken = default) =>
        !File.Exists(RecoveryPath)
            ? null
            : await ReadAsync(RecoveryPath, setProjectPath: false, cancellationToken);

    public Task ClearRecoveryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(RecoveryPath);
        return Task.CompletedTask;
    }

    private static async Task<EditorProject> ReadAsync(
        string projectPath,
        bool setProjectPath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(projectPath);
        await using var stream = File.OpenRead(fullPath);
        var project = await JsonSerializer.DeserializeAsync<EditorProject>(
            stream,
            SerializerOptions,
            cancellationToken) ?? throw new InvalidOperationException(
            $"The project file is empty or invalid: {fullPath}");
        Validate(project, fullPath);
        if (setProjectPath)
        {
            project.ProjectFilePath = fullPath;
        }

        return project;
    }

    private async Task WriteAtomicAsync(
        EditorProject project,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            project.SchemaVersion = EditorProject.CurrentSchemaVersion;
            project.ModifiedUtc = DateTime.UtcNow;
            var directory = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidOperationException("The project path must include a directory.");
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             65536,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    project,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _writeLock.Release();
        }
    }

    private static void Validate(EditorProject project, string path)
    {
        if (project.SchemaVersion <= 0 ||
            project.SchemaVersion > EditorProject.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Project '{path}' uses unsupported schema version {project.SchemaVersion}.");
        }

        if (project.Id == Guid.Empty || string.IsNullOrWhiteSpace(project.Name))
        {
            throw new InvalidOperationException($"Project '{path}' is missing required identity data.");
        }

        project.Tracks ??= [];
        project.Output ??= new ProjectOutputSettings();
        project.TargetDurationMinutes = Math.Clamp(project.TargetDurationMinutes, 0.1, 720);
        project.BackgroundColor = IsHexColor(project.BackgroundColor)
            ? project.BackgroundColor.ToUpperInvariant()
            : "#101010";
        var legacyBlurItems = new List<ProjectTimelineItem>();
        foreach (var track in project.Tracks)
        {
            track.Color = NormalizeOptionalColor(track.Color);
            track.Items ??= [];
            foreach (var item in track.Items)
            {
                item.Color = NormalizeOptionalColor(item.Color);
                item.FontFamily = string.IsNullOrWhiteSpace(item.FontFamily)
                    ? "Segoe UI"
                    : item.FontFamily.Trim();
                item.ProgressColor = IsHexColor(item.ProgressColor)
                    ? item.ProgressColor.ToUpperInvariant()
                    : "#C8C0B2";
                item.ProgressHeight = Math.Clamp(item.ProgressHeight, 2, 100);
                item.PluginParameters = item.PluginParameters is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(item.PluginParameters, StringComparer.OrdinalIgnoreCase);
                if (item.FitMode == VideoFitMode.BlurBackground)
                {
                    item.FitMode = VideoFitMode.Fit;
                    legacyBlurItems.Add(item);
                }
            }
        }

        if (legacyBlurItems.Count > 0)
        {
            var backgroundTrack = project.Tracks.FirstOrDefault(track => track.Kind == ProjectTrackKind.Background);
            if (backgroundTrack is null)
            {
                foreach (var track in project.Tracks)
                {
                    track.Order++;
                }

                backgroundTrack = new ProjectTrack
                {
                    Name = "Background",
                    Kind = ProjectTrackKind.Background,
                    Order = 0
                };
                project.Tracks.Add(backgroundTrack);
            }

            foreach (var item in legacyBlurItems.Where(item => backgroundTrack.Items.All(effect =>
                         effect.PluginId != BuiltInPluginIds.BackgroundBlur ||
                         effect.StartTicks != item.StartTicks || effect.DurationTicks != item.DurationTicks)))
            {
                backgroundTrack.Items.Add(new ProjectTimelineItem
                {
                    Kind = ProjectItemKind.Effect,
                    Name = "Blur content background",
                    StartTicks = item.StartTicks,
                    DurationTicks = item.DurationTicks,
                    PluginId = BuiltInPluginIds.BackgroundBlur
                });
            }
        }
    }

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private static string NormalizeOptionalColor(string? value) =>
        IsHexColor(value) ? value!.ToUpperInvariant() : string.Empty;
}
