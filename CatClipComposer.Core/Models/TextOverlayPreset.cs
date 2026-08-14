namespace CatClipComposer.Core.Models;

public sealed class TextOverlayPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string FontPath { get; set; } = string.Empty;

    public string FontFamily { get; set; } = "Segoe UI";

    public int FontSize { get; set; } = 42;

    public OverlayPosition Position { get; set; } = OverlayPosition.Center;

    public bool HasCustomTransform { get; set; }

    public double X { get; set; } = 0.5;

    public double Y { get; set; } = 0.5;

    public double Scale { get; set; } = 1;

    public double RotationDegrees { get; set; }

    public double Opacity { get; set; } = 1;

    public double FadeInSeconds { get; set; }

    public double FadeOutSeconds { get; set; }
}
