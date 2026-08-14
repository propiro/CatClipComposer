using System.Windows.Controls;
using CatClipComposer.Presentation;

namespace CatClipComposer.Controls;

internal static class TagQuickButtonBuilder
{
    public static void Populate(
        WrapPanel panel,
        TextBox editor,
        IReadOnlyList<string> popularTags)
    {
        panel.Children.Clear();
        if (popularTags.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No library tags yet.",
                Opacity = 0.72,
                Margin = new System.Windows.Thickness(0, 2, 0, 3)
            });
            return;
        }

        foreach (var tag in popularTags)
        {
            var button = new Button
            {
                Content = tag,
                Margin = new System.Windows.Thickness(0, 0, 5, 5),
                Padding = new System.Windows.Thickness(7, 2, 7, 2),
                MinHeight = 24,
                ToolTip = $"Add the frequently used tag '{tag}' without replacing text already entered."
            };
            button.Click += (_, _) =>
            {
                editor.Text = AppendTag(editor.Text, tag);
                editor.CaretIndex = editor.Text.Length;
                editor.Focus();
            };
            panel.Children.Add(button);
        }
    }

    internal static string AppendTag(string existing, string tag)
    {
        if (LibraryTagStatistics.Parse(existing).Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return tag;
        }

        var separator = existing.TrimEnd().EndsWith(',') || existing.TrimEnd().EndsWith(';')
            ? " "
            : ", ";
        return existing + separator + tag;
    }
}
