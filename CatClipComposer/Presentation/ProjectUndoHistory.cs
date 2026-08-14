using System.Text.Json;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Presentation;

internal sealed class ProjectUndoHistory
{
    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];
    private Snapshot _current = new([], "Project loaded");
    private byte[]? _savePoint;
    private int _maximumEntries;

    private sealed record Snapshot(byte[] Data, string Description);

    public ProjectUndoHistory(int maximumEntries = 32)
    {
        _maximumEntries = Math.Clamp(maximumEntries, 1, 256);
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool IsAtSavePoint => _savePoint is not null && _current.Data.AsSpan().SequenceEqual(_savePoint);

    public string LastActionDescription { get; private set; } = "Project change";

    public void SetMaximumEntries(int maximumEntries)
    {
        _maximumEntries = Math.Clamp(maximumEntries, 1, 256);
        Trim(_undo);
        Trim(_redo);
    }

    public void Reset(EditorProject project, bool isSaved)
    {
        _undo.Clear();
        _redo.Clear();
        _current = new Snapshot(Serialize(project), "Project loaded");
        _savePoint = isSaved ? [.. _current.Data] : null;
    }

    public bool Capture(EditorProject project, string description = "Project edit")
    {
        var next = Serialize(project);
        if (_current.Data.AsSpan().SequenceEqual(next))
        {
            return false;
        }

        _undo.Add(_current);
        Trim(_undo);

        _current = new Snapshot(next, description);
        _redo.Clear();
        LastActionDescription = description;
        return true;
    }

    public void MarkSaved(EditorProject project)
    {
        _current = _current with { Data = Serialize(project) };
        _savePoint = [.. _current.Data];
    }

    public EditorProject? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        LastActionDescription = _current.Description;
        _redo.Add(_current);
        _current = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return Deserialize(_current.Data);
    }

    public EditorProject? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        _undo.Add(_current);
        Trim(_undo);
        _current = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        LastActionDescription = _current.Description;
        return Deserialize(_current.Data);
    }

    private void Trim(List<Snapshot> entries)
    {
        if (entries.Count > _maximumEntries)
        {
            entries.RemoveRange(0, entries.Count - _maximumEntries);
        }
    }

    private static byte[] Serialize(EditorProject project) =>
        JsonSerializer.SerializeToUtf8Bytes(project);

    private static EditorProject Deserialize(byte[] snapshot) =>
        JsonSerializer.Deserialize<EditorProject>(snapshot) ??
        throw new InvalidOperationException("The undo snapshot could not be restored.");
}
