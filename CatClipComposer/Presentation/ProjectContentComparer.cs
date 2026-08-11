using System.Text.Json;
using System.Text.Json.Nodes;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Presentation;

internal static class ProjectContentComparer
{
    public static bool EqualsIgnoringPersistenceMetadata(EditorProject first, EditorProject second) =>
        JsonNode.DeepEquals(CreateComparableNode(first), CreateComparableNode(second));

    private static JsonNode CreateComparableNode(EditorProject project)
    {
        var node = JsonSerializer.SerializeToNode(project)?.AsObject() ??
                   throw new InvalidOperationException("The project could not be compared with its recovery data.");
        node.Remove(nameof(EditorProject.SchemaVersion));
        node.Remove(nameof(EditorProject.ModifiedUtc));
        node.Remove(nameof(EditorProject.ProjectFilePath));
        return node;
    }
}
