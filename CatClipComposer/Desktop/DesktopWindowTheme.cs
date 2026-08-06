using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CatClipComposer.Desktop;

internal static class DesktopWindowTheme
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var enabled = 1;
            if (DwmSetWindowAttribute(
                    handle,
                    UseImmersiveDarkMode,
                    ref enabled,
                    sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(
                    handle,
                    UseImmersiveDarkModeLegacy,
                    ref enabled,
                    sizeof(int));
            }
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
