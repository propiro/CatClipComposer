using System.Reflection;

namespace CatClipComposer.Core;

public static class ProductInfo
{
    public const string Name = "Cat Clip Composer";

    private static string InformationalVersion { get; } = ResolveInformationalVersion();

    public static string Version { get; } = InformationalVersion.Split('+', 2)[0];

    public static string? BuildRevision { get; } = ResolveBuildRevision();

    public static string DisplayVersion => $"v{Version}";

    public static string WindowTitle => $"{Name} — {DisplayVersion}";

    private static string ResolveInformationalVersion()
    {
        var informationalVersion = typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return typeof(ProductInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string? ResolveBuildRevision()
    {
        var separator = InformationalVersion.IndexOf('+');
        return separator >= 0 && separator < InformationalVersion.Length - 1
            ? InformationalVersion[(separator + 1)..]
            : null;
    }
}
