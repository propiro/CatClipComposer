using System.Windows;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class RefreshLibraryWindow : Window
{
    public RefreshLibraryWindow()
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
    }

    public bool RegeneratePreviews { get; private set; }

    private void CatalogOnly_Click(object sender, RoutedEventArgs e)
    {
        RegeneratePreviews = false;
        DialogResult = true;
    }

    private void Regenerate_Click(object sender, RoutedEventArgs e)
    {
        RegeneratePreviews = true;
        DialogResult = true;
    }
}
