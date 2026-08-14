using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Core.Utilities;
using CatClipComposer.Desktop;
using CatClipComposer.Presentation;

namespace CatClipComposer;

public partial class HistoryWindow : Window
{
    private readonly IMediaCatalog _catalog;
    private readonly string _metadataFolder;

    public HistoryWindow(
        IMediaCatalog catalog,
        ObservableCollection<ActionHistoryEntryViewModel> actionHistory,
        string metadataFolder)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        _catalog = catalog;
        _metadataFolder = metadataFolder;
        ActionHistory = actionHistory;
        DataContext = this;
        Loaded += HistoryWindow_Loaded;
    }

    public ObservableCollection<ExportHistoryCard> History { get; } = [];

    public ObservableCollection<ActionHistoryEntryViewModel> ActionHistory { get; }

    public ObservableCollection<LogFileCard> LogFiles { get; } = [];

    private async void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HistoryWindow_Loaded;
        LoadLogFiles();
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

    private void LoadLogFiles()
    {
        if (!Directory.Exists(_metadataFolder))
        {
            return;
        }

        IEnumerable<string> candidates;
        try
        {
            candidates = Directory.EnumerateFiles(_metadataFolder, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                               Path.GetFileName(path).Contains("crash", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(200)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var path in candidates)
        {
            LogFiles.Add(new LogFileCard(path));
        }
    }

    private void LogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LogListBox.SelectedItem is LogFileCard card && File.Exists(card.FullPath))
        {
            DesktopShell.ShowFileInExplorer(card.FullPath);
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

        public string ProjectName => string.IsNullOrWhiteSpace(Entry.ProjectName)
            ? "Unnamed / legacy export"
            : Entry.ProjectName;

        public string CreatedText => Entry.CreatedUtc.ToLocalTime().ToString("g");

        public string SummaryText =>
            $"{DurationFormatter.Format(Entry.Duration)} • {Entry.Clips.Count} video clip(s)";
    }

    public sealed class LogFileCard(string fullPath)
    {
        public string FullPath { get; } = fullPath;

        public string FileName => Path.GetFileName(FullPath);

        public string Detail => $"{File.GetLastWriteTime(FullPath):g} | {new FileInfo(FullPath).Length:N0} bytes | {FullPath}";
    }
}
