namespace CatClipComposer.Core.Models;

public enum ProjectTrackKind
{
    Video,
    Overlay,
    Audio,
    Progress,
    Effects
}

public enum ProjectItemKind
{
    Video,
    StillImage,
    TextOverlay,
    ImageOverlay,
    Audio,
    ProgressBar,
    Effect
}

public enum VideoFitMode
{
    Fit,
    Fill,
    Stretch,
    BlurBackground
}

public enum ProgressTimeMode
{
    WholeProject,
    SourceSegment,
    CustomRange
}

public sealed class EditorProject
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Untitled project";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    public string? ProjectFilePath { get; set; }

    public ProjectOutputSettings Output { get; set; } = new();

    public List<ProjectTrack> Tracks { get; set; } = [];

    public static EditorProject Create(string name, ProjectOutputSettings output) => new()
    {
        Name = name,
        Output = output,
        Tracks =
        [
            new ProjectTrack { Name = "Video", Kind = ProjectTrackKind.Video, Order = 0 },
            new ProjectTrack { Name = "Overlays", Kind = ProjectTrackKind.Overlay, Order = 1 },
            new ProjectTrack { Name = "Audio", Kind = ProjectTrackKind.Audio, Order = 2 },
            new ProjectTrack { Name = "Progress", Kind = ProjectTrackKind.Progress, Order = 3 },
            new ProjectTrack { Name = "Effects", Kind = ProjectTrackKind.Effects, Order = 4 }
        ]
    };
}

public sealed class ProjectTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public ProjectTrackKind Kind { get; set; }

    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsLocked { get; set; }

    public List<ProjectTimelineItem> Items { get; set; } = [];
}

public sealed class ProjectTimelineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ProjectItemKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public long? MediaFileId { get; set; }

    public long StartTicks { get; set; }

    public long DurationTicks { get; set; }

    public bool HasAudio { get; set; }

    public bool IsEnabled { get; set; } = true;

    public VideoFitMode FitMode { get; set; } = VideoFitMode.Fit;

    public double FadeInSeconds { get; set; }

    public double FadeOutSeconds { get; set; }

    public double Volume { get; set; } = 1;

    public string Text { get; set; } = string.Empty;

    public string FontPath { get; set; } = string.Empty;

    public int FontSize { get; set; } = 42;

    public OverlayPosition Position { get; set; } = OverlayPosition.Center;

    public ProgressTimeMode ProgressTimeMode { get; set; } = ProgressTimeMode.WholeProject;

    public TimeSpan Start => TimeSpan.FromTicks(StartTicks);

    public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);
}

public sealed class ProjectOutputSettings
{
    public string PresetName { get; set; } = "YouTube 1080p";

    public int Width { get; set; } = 1920;

    public int Height { get; set; } = 1080;

    public double FramesPerSecond { get; set; } = 30;

    public VideoEncoderPreset VideoEncoder { get; set; } = VideoEncoderPreset.NativeMpeg4;

    public int QualityPercent { get; set; } = 80;

    public int VideoBitrateKbps { get; set; } = 8000;

    public int AudioBitrateKbps { get; set; } = 192;
}
