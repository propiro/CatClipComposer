using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Core.Utilities;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class HistoryWindow : Window
{
    private readonly IMediaCatalog _catalog;

    public HistoryWindow(IMediaCatalog catalog)
    {
        InitializeComponent();
        _catalog = catalog;
        DataContext = this;
        Loaded += HistoryWindow_Loaded;
    }

    public ObservableCollection<ExportHistoryCard> History { get; } = [];

    private async void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HistoryWindow_Loaded;
        try
        {
            var entries = await _catalog.GetExportHistoryAsync();
            foreach (var entry in entries)
            {
                History.Add(new ExportHistoryCard(entry));
            }

            HistoryListBox.SelectedIndex = History.Count > 0 ? 0 : -1;
            StatusTextBlock.Text = History.Count switch
            {
                0 => "No completed exports have been recorded yet.",
                1 => "1 completed export",
                _ => $"{History.Count} completed exports"
            };
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"Could not load history: {exception.Message}";
        }
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryListBox.SelectedItem is not ExportHistoryCard card)
        {
            return;
        }

        if (!File.Exists(card.Entry.OutputPath))
        {
            MessageBox.Show(
                this,
                "The recorded output file is no longer at that path.",
                "Output not found",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DesktopShell.ShowFileInExplorer(card.Entry.OutputPath);
    }

    private void HistoryClipListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryClipListBox.SelectedItem is ExportHistoryClip clip && File.Exists(clip.FullPath))
        {
            DesktopShell.ShowFileInExplorer(clip.FullPath);
        }
    }

    public sealed class ExportHistoryCard(ExportHistoryEntry entry)
    {
        public ExportHistoryEntry Entry { get; } = entry;

        public string OutputFileName => Path.GetFileName(Entry.OutputPath);

        public string CreatedText => Entry.CreatedUtc.ToLocalTime().ToString("g");

        public string SummaryText =>
            $"{DurationFormatter.Format(Entry.Duration)} • {Entry.Clips.Count} video clip(s)";
    }
}
