using System.Windows;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public enum UnsavedProjectChoice
{
    Cancel,
    DontSave,
    Save
}

public partial class UnsavedChangesWindow : Window
{
    public UnsavedChangesWindow(string projectName)
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        ProjectNameText.Text = string.IsNullOrWhiteSpace(projectName)
            ? "The current project has unsaved changes."
            : $"{projectName} has unsaved changes.";
    }

    public UnsavedProjectChoice Choice { get; private set; } = UnsavedProjectChoice.Cancel;

    private void DontSave_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedProjectChoice.DontSave;
        DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedProjectChoice.Save;
        DialogResult = true;
    }
}
