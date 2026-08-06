using System.Diagnostics;
using System.IO;

namespace CatClipComposer.Desktop;

internal static class DesktopShell
{
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
}
