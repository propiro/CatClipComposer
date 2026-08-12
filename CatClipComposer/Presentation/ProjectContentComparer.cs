using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using CatClipComposer.Core;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Presentation;

internal static class ProjectContentComparer
{
    public static bool EqualsIgnoringPersistenceMetadata(EditorProject first, EditorProject second) =>
        JsonNode.DeepEquals(CreateComparableNode(first), CreateComparableNode(second));

    public static string CreateContentFingerprint(EditorProject project)
    {
        var content = new StringBuilder(CreateComparableNode(project).ToJsonString());
        content.Append("|app:").Append(ProductInfo.DisplayVersion);
        foreach (var path in project.Tracks
                     .SelectMany(track => track.Items)
                     .SelectMany(item => new[] { item.SourcePath, item.FontPath })
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            content.Append("|file:").Append(path);
            try
            {
                var file = new FileInfo(path);
                content.Append(':').Append(file.Exists ? file.Length : -1)
                    .Append(':').Append(file.Exists ? file.LastWriteTimeUtc.Ticks : -1);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                content.Append(":unavailable");
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString())));
    }

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
