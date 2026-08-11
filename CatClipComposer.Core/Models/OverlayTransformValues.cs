namespace CatClipComposer.Core.Models;

public static class OverlayTransformValues
{
    public const double MinimumScale = 0.01;
    public const double MaximumScale = 10;
    public const double MinimumCoordinate = -2;
    public const double MaximumCoordinate = 3;

    public static (double X, double Y) GetPresetCenter(OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft => (0.1, 0.1),
        OverlayPosition.TopRight => (0.9, 0.1),
        OverlayPosition.BottomLeft => (0.1, 0.9),
        OverlayPosition.BottomRight => (0.9, 0.9),
        _ => (0.5, 0.5)
    };

    public static double NormalizeCoordinate(double value, double fallback = 0.5) =>
        double.IsFinite(value)
            ? Math.Clamp(value, MinimumCoordinate, MaximumCoordinate)
            : fallback;

    public static double NormalizeScale(double value) =>
        double.IsFinite(value)
            ? Math.Clamp(value, MinimumScale, MaximumScale)
            : 1;

    public static double NormalizeRotation(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        var normalized = value % 360;
        if (normalized > 180)
        {
            normalized -= 360;
        }
        else if (normalized <= -180)
        {
            normalized += 360;
        }

        return normalized;
    }
}
