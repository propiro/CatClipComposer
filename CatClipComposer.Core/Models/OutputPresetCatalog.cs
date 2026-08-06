namespace CatClipComposer.Core.Models;

public sealed record OutputPreset(
    string Name,
    int Width,
    int Height,
    double FramesPerSecond,
    int VideoBitrateKbps,
    int AudioBitrateKbps,
    string Description);

public static class OutputPresetCatalog
{
    public static IReadOnlyList<OutputPreset> Common { get; } =
    [
        new("YouTube 1080p", 1920, 1080, 30, 8000, 192, "16:9 standard frame rate"),
        new("YouTube 1080p60", 1920, 1080, 60, 12000, 192, "16:9 high frame rate"),
        new("YouTube 4K", 3840, 2160, 30, 45000, 256, "16:9 UHD standard frame rate"),
        new("YouTube 4K60", 3840, 2160, 60, 68000, 256, "16:9 UHD high frame rate"),
        new("YouTube Shorts", 1080, 1920, 30, 8000, 192, "Vertical 9:16"),
        new("Square", 1080, 1080, 30, 8000, 192, "Square 1:1"),
        new("Classic 4:3", 1440, 1080, 30, 8000, 192, "Classic 4:3"),
        new("Custom", 1920, 1080, 30, 8000, 192, "User-defined values")
    ];
}
