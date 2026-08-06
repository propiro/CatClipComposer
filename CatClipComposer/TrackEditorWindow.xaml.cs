using System.Windows;
using System.Windows.Controls;
using CatClipComposer.Core.Models;
using CatClipComposer.Desktop;

namespace CatClipComposer;

public partial class TrackEditorWindow : Window
{
    public TrackEditorWindow()
    {
        InitializeComponent();
        DesktopWindowTheme.Apply(this);
        TrackKindComboBox.ItemsSource = Enum.GetValues<ProjectTrackKind>();
        TrackKindComboBox.SelectedItem = ProjectTrackKind.Video;
    }

    public ProjectTrackKind ResultKind { get; private set; }

    public string? ResultName { get; private set; }

    private void TrackKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TrackKindComboBox.SelectedItem is ProjectTrackKind kind)
        {
            TrackNameTextBox.Text = kind switch
            {
                ProjectTrackKind.Video => "Video",
                ProjectTrackKind.Overlay => "Overlays",
                ProjectTrackKind.Audio => "Audio",
                ProjectTrackKind.Progress => "Progress",
                ProjectTrackKind.Background => "Background",
                _ => "Effects"
            };
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (TrackKindComboBox.SelectedItem is not ProjectTrackKind kind ||
            string.IsNullOrWhiteSpace(TrackNameTextBox.Text))
        {
            MessageBox.Show(this, "Choose a timeline type and enter a name.",
                "Invalid timeline", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultKind = kind;
        ResultName = TrackNameTextBox.Text.Trim();
        DialogResult = true;
    }
}
