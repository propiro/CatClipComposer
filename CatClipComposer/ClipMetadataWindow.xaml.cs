using System.Collections.ObjectModel;
using System.Windows;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;

namespace CatClipComposer;

public partial class ClipMetadataWindow : Window
{
    private readonly IMediaCatalog _catalog;
    private readonly long _mediaFileId;

    public ClipMetadataWindow(MediaCardViewModel media, IMediaCatalog catalog)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        DataContext = media;
        _catalog = catalog;
        _mediaFileId = media.Media.Id;
        Loaded += ClipMetadataWindow_Loaded;
    }

    public ObservableCollection<MediaUsageEntry> Usage { get; } = [];

    public string Tags => TagsTextBox.Text;

    private async void ClipMetadataWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ClipMetadataWindow_Loaded;
        try
        {
            var entries = await _catalog.GetUsageAsync(_mediaFileId);
            foreach (var entry in entries)
            {
                Usage.Add(entry);
            }

            UsageListBox.ItemsSource = Usage;
            UsageStatusText.Text = Usage.Count == 0
                ? "This clip has not appeared in a completed export."
                : string.Empty;
        }
        catch (Exception exception)
        {
            UsageStatusText.Text = $"Could not load usage: {exception.Message}";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
