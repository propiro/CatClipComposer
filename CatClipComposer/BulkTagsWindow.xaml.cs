using System.Windows;
using CatClipComposer.Controls;

namespace CatClipComposer;

public partial class BulkTagsWindow : Window
{
    public BulkTagsWindow(
        int clipCount,
        string initialTags,
        IReadOnlyList<string> popularTags)
    {
        InitializeComponent();
        Desktop.DesktopWindowTheme.Apply(this);
        SummaryText.Text = clipCount == 1 ? "Editing one clip" : $"Editing {clipCount} selected clips";
        TagsTextBox.Text = initialTags;
        TagQuickButtonBuilder.Populate(PopularTagsPanel, TagsTextBox, popularTags);
        Loaded += (_, _) =>
        {
            TagsTextBox.Focus();
            TagsTextBox.SelectAll();
        };
    }

    public string Tags => TagsTextBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
