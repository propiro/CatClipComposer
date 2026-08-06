using System.Text.Json;
using System.Text.Json.Serialization;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure;

public sealed class JsonSettingsStore(AppPaths paths) : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();

        if (!File.Exists(paths.SettingsPath))
        {
            return CreateDefaults();
        }

        try
        {
            await using var stream = File.OpenRead(paths.SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(
                stream,
                SerializerOptions,
                cancellationToken);
            return Normalize(settings ?? CreateDefaults());
        }
        catch (JsonException)
        {
            return CreateDefaults();
        }
    }

    public async Task SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        var normalized = Normalize(settings.Copy());
        await using var stream = File.Create(paths.SettingsPath);
        await JsonSerializer.SerializeAsync(
            stream,
            normalized,
            SerializerOptions,
            cancellationToken);
    }

    private static ApplicationSettings CreateDefaults() => new()
    {
        OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
    };

    private static ApplicationSettings Normalize(ApplicationSettings settings)
    {
        settings.SourceFolders = settings.SourceFolders
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : settings.OutputFolder.Trim();
        settings.FfmpegPath = string.IsNullOrWhiteSpace(settings.FfmpegPath)
            ? "ffmpeg.exe"
            : settings.FfmpegPath.Trim();
        settings.TargetDurationMinutes = Math.Clamp(settings.TargetDurationMinutes, 1, 720);
        settings.OverlayImagePath = settings.OverlayImagePath?.Trim() ?? string.Empty;
        settings.OverlayText = settings.OverlayText?.Trim() ?? string.Empty;
        settings.OverlayFontPath = settings.OverlayFontPath?.Trim() ?? string.Empty;
        settings.OverlayTextSize = Math.Clamp(settings.OverlayTextSize, 8, 200);
        return settings;
    }
}
