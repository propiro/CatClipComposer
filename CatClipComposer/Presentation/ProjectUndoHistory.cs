using System.Text.Json;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Presentation;

internal sealed class ProjectUndoHistory
{
    private const int MaximumEntries = 100;
    private readonly List<byte[]> _undo = [];
    private readonly List<byte[]> _redo = [];
    private byte[] _current = [];
    private byte[]? _savePoint;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool IsAtSavePoint => _savePoint is not null && _current.AsSpan().SequenceEqual(_savePoint);

    public void Reset(EditorProject project, bool isSaved)
    {
        _undo.Clear();
        _redo.Clear();
        _current = Serialize(project);
        _savePoint = isSaved ? [.. _current] : null;
    }

    public bool Capture(EditorProject project)
    {
        var next = Serialize(project);
        if (_current.AsSpan().SequenceEqual(next))
        {
            return false;
        }

        _undo.Add(_current);
        if (_undo.Count > MaximumEntries)
        {
            _undo.RemoveAt(0);
        }

        _current = next;
        _redo.Clear();
        return true;
    }

    public void MarkSaved(EditorProject project)
    {
        _current = Serialize(project);
        _savePoint = [.. _current];
    }

    public EditorProject? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        _redo.Add(_current);
        _current = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return Deserialize(_current);
    }

    public EditorProject? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        _undo.Add(_current);
        _current = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        return Deserialize(_current);
    }

    private static byte[] Serialize(EditorProject project) =>
        JsonSerializer.SerializeToUtf8Bytes(project);

    private static EditorProject Deserialize(byte[] snapshot) =>
        JsonSerializer.Deserialize<EditorProject>(snapshot) ??
        throw new InvalidOperationException("The undo snapshot could not be restored.");
}
