using System.Globalization;

namespace CatClipComposer.Plugins.BuiltIn;

internal static class PluginValues
{
    public static double Number(
        IReadOnlyDictionary<string, string> values,
        string key,
        double fallback,
        double minimum,
        double maximum) =>
        values.TryGetValue(key, out var text) &&
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
        double.IsFinite(value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    public static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    public static string Color(string value)
    {
        var hex = value.Trim().TrimStart('#');
        return hex.Length == 6 && hex.All(Uri.IsHexDigit) ? $"0x{hex}" : "0x101010";
    }
}
