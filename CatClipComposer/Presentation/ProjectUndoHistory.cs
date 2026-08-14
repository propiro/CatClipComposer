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

    public IReadOnlyList<ProjectHistoryNavigationEntry> GetUndoChoices()
    {
        var choices = new List<ProjectHistoryNavigationEntry>(_undo.Count);
        for (var step = 1; step <= _undo.Count; step++)
        {
            var description = step == 1
                ? _current.Description
                : _undo[_undo.Count - step + 1].Description;
            choices.Add(new ProjectHistoryNavigationEntry(step, description));
        }

        return choices;
    }

    public IReadOnlyList<ProjectHistoryNavigationEntry> GetRedoChoices()
    {
        var choices = new List<ProjectHistoryNavigationEntry>(_redo.Count);
        for (var step = 1; step <= _redo.Count; step++)
        {
            choices.Add(new ProjectHistoryNavigationEntry(step, _redo[^step].Description));
        }

        return choices;
    }

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

    public EditorProject? Undo(int steps = 1)
    {
        if (steps < 1 || steps > _undo.Count)
        {
            return null;
        }

        var choices = GetUndoChoices();
        LastActionDescription = choices[steps - 1].Description;
        for (var step = 0; step < steps; step++)
        {
            _redo.Add(_current);
            _current = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
        }

        return Deserialize(_current.Data);
    }

    public EditorProject? Redo(int steps = 1)
    {
        if (steps < 1 || steps > _redo.Count)
        {
            return null;
        }

        var choices = GetRedoChoices();
        LastActionDescription = choices[steps - 1].Description;
        for (var step = 0; step < steps; step++)
        {
            _undo.Add(_current);
            Trim(_undo);
            _current = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
        }

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

internal sealed record ProjectHistoryNavigationEntry(int StepCount, string Description);
