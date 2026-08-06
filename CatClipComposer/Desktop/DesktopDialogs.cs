using System.Windows;

namespace CatClipComposer.Desktop;

internal static class DesktopDialogs
{
    public static void ShowError(
        Window? owner,
        string message,
        Exception exception)
    {
        var detail = $"{message}{Environment.NewLine}{Environment.NewLine}{exception.Message}";
        if (owner is null)
        {
            MessageBox.Show(
                detail,
                "Cat Clip Composer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            owner,
            detail,
            "Cat Clip Composer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
