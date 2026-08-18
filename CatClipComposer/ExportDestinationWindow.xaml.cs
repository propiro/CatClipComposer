using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class ExportDestinationWindow : Window
{
    private readonly ObservableCollection<FolderEntry> _folders = [];
    private bool _updatingDrive;

    public ExportDestinationWindow(string initialDirectory, string initialFileName)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        FolderListBox.ItemsSource = _folders;
        DriveComboBox.ItemsSource = DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FileNameTextBox.Text = initialFileName;
        NavigateTo(initialDirectory);
    }

    public string? OutputPath { get; private set; }

    private void NavigateTo(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                ShowStatus("That directory does not exist.");
                return;
            }

            var entries = Directory.EnumerateDirectories(fullPath)
                .Select(directory => new FolderEntry(Path.GetFileName(directory), directory))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            _folders.Clear();
            foreach (var entry in entries)
            {
                _folders.Add(entry);
            }

            PathTextBox.Text = fullPath;
            _updatingDrive = true;
            DriveComboBox.SelectedItem = Path.GetPathRoot(fullPath);
            _updatingDrive = false;
            ShowStatus(string.Empty);
            ResetOverwriteApproval();
            UpdateOutputPathPreview();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowStatus($"The directory could not be opened: {exception.Message}");
        }
    }

    private void DriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingDrive && DriveComboBox.SelectedItem is string path)
        {
            NavigateTo(path);
        }
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var parent = Directory.GetParent(PathTextBox.Text);
            if (parent is not null)
            {
                NavigateTo(parent.FullName);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            ShowStatus($"The parent directory could not be resolved: {exception.Message}");
        }
    }

    private void Go_Click(object sender, RoutedEventArgs e) => NavigateTo(PathTextBox.Text);

    private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateTo(PathTextBox.Text);
            e.Handled = true;
        }
    }

    private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ResetOverwriteApproval();
        UpdateOutputPathPreview();
    }

    private void FolderListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FolderListBox.SelectedItem is FolderEntry entry)
        {
            NavigateTo(entry.FullPath);
        }
    }

    private void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        var name = NewFolderTextBox.Text.Trim();
        if (!IsValidFileName(name))
        {
            ShowStatus("Enter a valid new folder name without path separators.");
            return;
        }

        try
        {
            var path = Path.Combine(PathTextBox.Text, name);
            Directory.CreateDirectory(path);
            NewFolderTextBox.Clear();
            NavigateTo(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowStatus($"The folder could not be created: {exception.Message}");
        }
    }

    private void FileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ResetOverwriteApproval();
        UpdateOutputPathPreview();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(PathTextBox.Text))
        {
            ShowStatus("Open an existing export directory before continuing.");
            return;
        }

        var fileName = FileNameTextBox.Text.Trim();
        if (!IsValidFileName(fileName))
        {
            ShowStatus("Enter a valid file name without directory separators.");
            return;
        }

        if (!fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".mp4";
        }

        var outputPath = Path.GetFullPath(Path.Combine(PathTextBox.Text, fileName));
        if (File.Exists(outputPath) && ReplaceExistingCheckBox.IsChecked != true)
        {
            ReplaceExistingCheckBox.Visibility = Visibility.Visible;
            ShowStatus("This file already exists. Check the replacement box to continue.");
            return;
        }

        OutputPath = outputPath;
        DialogResult = true;
    }

    private void UpdateOutputPathPreview()
    {
        if (PathTextBox is null || FileNameTextBox is null || OutputPathTextBlock is null)
        {
            return;
        }

        var fileName = FileNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            OutputPathTextBlock.Text = PathTextBox.Text;
            return;
        }

        if (!fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".mp4";
        }

        try
        {
            OutputPathTextBlock.Text = Path.Combine(PathTextBox.Text, fileName);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            OutputPathTextBlock.Text = exception.Message;
        }
    }

    private void ResetOverwriteApproval()
    {
        if (ReplaceExistingCheckBox is null)
        {
            return;
        }

        ReplaceExistingCheckBox.IsChecked = false;
        ReplaceExistingCheckBox.Visibility = Visibility.Collapsed;
    }

    private void ShowStatus(string message) => StatusTextBlock.Text = message;

    private static bool IsValidFileName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value is not "." and not ".." &&
        !value.EndsWith(' ') &&
        !value.EndsWith('.') &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !value.Contains(Path.DirectorySeparatorChar) &&
        !value.Contains(Path.AltDirectorySeparatorChar) &&
        !IsReservedWindowsName(value);

    private static bool IsReservedWindowsName(string value)
    {
        var stem = Path.GetFileNameWithoutExtension(value);
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (stem.Length == 4 &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                stem[3] is >= '1' and <= '9');
    }

    private sealed record FolderEntry(string Name, string FullPath);
}
