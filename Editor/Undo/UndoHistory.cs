namespace Editor;

// One reversible edit. Actions are recorded *after* they have already been applied once, so
// Redo() re-applies the same change and Undo() reverses it. Implementations must be idempotent
// across repeated undo/redo cycles (restore from captured state rather than assuming a delta).
public interface IUndoable
{
    void Undo();
    void Redo();
    string Label { get; }   // shown in the Edit menu ("Undo Move", ...)
}

// A lambda-backed action. Most edit sites have all the context to write their own do/undo
// closures inline, so a single relay type saves a class-per-edit-kind explosion.
public sealed class RelayUndoable : IUndoable
{
    readonly Action _undo, _redo;
    public string Label { get; }

    public RelayUndoable(string label, Action undo, Action redo)
    {
        Label = label;
        _undo = undo;
        _redo = redo;
    }

    public void Undo() => _undo();
    public void Redo() => _redo();
}

// A per-document (per-window) undo/redo stack. Push records an already-applied action and clears
// the redo stack; Undo/Redo move actions between the two stacks, invoking the reversal.
public sealed class UndoHistory
{
    const int k_max = 256;   // cap memory; oldest actions fall off the bottom

    readonly List<IUndoable> _undo = new();
    readonly List<IUndoable> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string? NextUndoLabel => CanUndo ? _undo[^1].Label : null;
    public string? NextRedoLabel => CanRedo ? _redo[^1].Label : null;

    // Records an action the caller has already performed. Invalidates the redo stack.
    public void Push(IUndoable action)
    {
        _undo.Add(action);
        if (_undo.Count > k_max) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var a = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        a.Undo();
        _redo.Add(a);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var a = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        a.Redo();
        _undo.Add(a);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
