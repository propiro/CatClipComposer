using System.Reflection;

namespace CatClipComposer.Core;

public static class ProductInfo
{
    public const string Name = "Cat Clip Composer";

    public static string Version { get; } = ResolveVersion();

    public static string DisplayVersion => $"v{Version}";

    public static string WindowTitle => $"{Name} — {DisplayVersion}";

    private static string ResolveVersion()
    {
        var informationalVersion = typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return typeof(ProductInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
