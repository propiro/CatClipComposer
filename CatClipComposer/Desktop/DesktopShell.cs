using System.Diagnostics;
using System.IO;

namespace CatClipComposer.Desktop;

internal static class DesktopShell
{
    private const string RepositoryPath = "/propiro/CatClipComposer";

    public static void ShowFileInExplorer(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add($"/select,{Path.GetFullPath(filePath)}");
        Process.Start(startInfo);
    }

    public static void OpenTrustedGitHubPage(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var trustedPath = uri.AbsolutePath.Equals(RepositoryPath, StringComparison.OrdinalIgnoreCase) ||
                          uri.AbsolutePath.StartsWith($"{RepositoryPath}/", StringComparison.OrdinalIgnoreCase);
        if (!uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.IdnHost.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !trustedPath)
        {
            throw new InvalidOperationException("Only the official Cat Clip Composer GitHub pages can be opened.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }
}
