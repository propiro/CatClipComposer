using System.Windows;

namespace CatClipComposer;

public partial class BulkTagsWindow : Window
{
    public BulkTagsWindow(int clipCount, string initialTags)
    {
        InitializeComponent();
        Desktop.DesktopWindowTheme.Apply(this);
        SummaryText.Text = clipCount == 1 ? "Editing one clip" : $"Editing {clipCount} selected clips";
        TagsTextBox.Text = initialTags;
        Loaded += (_, _) =>
        {
            TagsTextBox.Focus();
            TagsTextBox.SelectAll();
        };
    }

    public string Tags => TagsTextBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
