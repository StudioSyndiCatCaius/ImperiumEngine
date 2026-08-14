using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;

/// <summary>Where a comp sits in the hierarchy - enough to put it back exactly.</summary>
public struct TCompPlace
{
    public ImpComp parent;
    public int index;
    public object transform; //boxed TTransform2 / TTransform3, null on a plain ImpComp
}

// One reversible edit. Undo and redo are the same shape - a list of closures - so anything
// that can be said as "call this to go back, call that to go forward" fits without needing a
// class per kind of operation.
public class TUndoStep
{
    public string label = "";
    //identifies the thing being edited, so repeats of the same edit can collapse. null never merges.
    public object merge_key;
    public double time;

    public readonly List<Action> undo = new();
    public readonly List<Action> redo = new();

    public void Undo() { for (int i = undo.Count - 1; i >= 0; i--) undo[i](); }
    public void Redo() { for (int i = 0; i < redo.Count; i++) redo[i](); }
}

/// <summary>
/// Undo history for one document. Edits record into <see cref="active"/>, which the editor
/// points at whichever document is on screen - so each scene tab and each asset tab keeps its
/// own history, and undo never reaches into a scene the user is not looking at. A null active
/// history means nothing is recording and edits simply apply, which is what the game gets.
/// </summary>
public class ImpUndo
{
    // #################################################################################
    // Static
    // #################################################################################

    public static ImpUndo active;

    // True while a step is being undone or redone, so the setters those closures drive don't
    // record themselves back into the history as fresh edits.
    public static bool is_applying;

    // Edits to the same thing inside this window collapse into one step, so dragging a slider
    // or typing a name is one undo rather than one per frame / keystroke.
    public const double MergeSeconds = 0.6;

    public static void Push(string label, Action undo, Action redo, object merge_key = null)
    {
        if (is_applying || undo == null || redo == null) return;
        active?.Step_Push(label, undo, redo, merge_key);
    }

    /// <summary>Collects everything pushed until Group_End into a single step.</summary>
    public static void Group_Begin(string label)
    {
        if (is_applying) return;
        active?.Group_Open(label);
    }

    public static void Group_End()
    {
        if (is_applying) return;
        active?.Group_Close();
    }

    // #################################################################################
    // Class
    // #################################################################################

    public int max_steps = 128;
    public ImpAsset asset; //flagged dirty by every edit recorded here
    public Action on_changed;

    readonly List<TUndoStep> _steps = new();
    int _index; //steps below this are applied, steps at or above it have been undone
    TUndoStep _group;
    int _group_depth;

    public bool CanUndo => _index > 0;
    public bool CanRedo => _index < _steps.Count;
    public string UndoLabel => CanUndo ? _steps[_index - 1].label : "";
    public string RedoLabel => CanRedo ? _steps[_index].label : "";
    public int Count => _steps.Count;

    public void Undo()
    {
        if (!CanUndo || is_applying) return;
        is_applying = true;
        try { _steps[--_index].Undo(); }
        finally { is_applying = false; }
        Changed();
    }

    public void Redo()
    {
        if (!CanRedo || is_applying) return;
        is_applying = true;
        try { _steps[_index++].Redo(); }
        finally { is_applying = false; }
        Changed();
    }

    public void Clear()
    {
        _steps.Clear();
        _index = 0;
        _group = null;
        _group_depth = 0;
    }

    void Step_Push(string label, Action undo, Action redo, object merge_key)
    {
        if (_group != null)
        {
            _group.undo.Add(undo);
            _group.redo.Add(redo);
            return;
        }
        TUndoStep step = new() { label = label ?? "", merge_key = merge_key, time = Raylib.GetTime() };
        step.undo.Add(undo);
        step.redo.Add(redo);
        Step_Commit(step);
    }

    void Group_Open(string label)
    {
        _group_depth++;
        if (_group_depth == 1)
            _group = new TUndoStep { label = label ?? "", time = Raylib.GetTime() };
    }

    void Group_Close()
    {
        if (_group_depth == 0) return;
        _group_depth--;
        if (_group_depth > 0) return;
        TUndoStep step = _group;
        _group = null;
        Step_Commit(step);
    }

    void Step_Commit(TUndoStep step)
    {
        if (step == null || step.undo.Count == 0) return; //an edit that changed nothing

        // A redo branch stops being reachable the moment a new edit lands on top of it.
        if (_index < _steps.Count) _steps.RemoveRange(_index, _steps.Count - _index);

        TUndoStep last = _index > 0 ? _steps[_index - 1] : null;
        if (last != null && step.merge_key != null && Equals(last.merge_key, step.merge_key)
            && step.time - last.time < MergeSeconds)
        {
            // Keep the older undo - the value from before the whole gesture - and take the newer redo.
            last.redo.Clear();
            last.redo.AddRange(step.redo);
            last.time = step.time;
            Changed();
            return;
        }

        _steps.Add(step);
        if (_steps.Count > max_steps) _steps.RemoveRange(0, _steps.Count - max_steps);
        _index = _steps.Count;
        Changed();
    }

    void Changed()
    {
        if (asset != null) asset.is_dirty = true;
        on_changed?.Invoke();
    }

    // ------------------------------------
    // Values
    // ------------------------------------

    /// <summary>
    /// Applies a value through every bind and records the lot as one step. This is the funnel
    /// every inspector edit goes through, whatever the widget or the type behind it.
    /// </summary>
    public static void Bind_Set(List<TPropertyBind> binds, object value, string label, object merge_key = null)
    {
        if (binds == null || binds.Count == 0) return;
        if (is_applying || active == null)
        {
            for (int i = 0; i < binds.Count; i++) binds[i].Set(value);
            return;
        }

        object[] before = new object[binds.Count];
        bool changed = false;
        for (int i = 0; i < binds.Count; i++)
        {
            before[i] = binds[i].Get();
            if (!Equals(before[i], value)) changed = true;
        }
        if (!changed) return;

        TPropertyBind[] targets = binds.ToArray();
        for (int i = 0; i < targets.Length; i++) targets[i].Set(value);

        Push(label,
            () => { for (int i = 0; i < targets.Length; i++) targets[i].Set(before[i]); },
            () => { for (int i = 0; i < targets.Length; i++) targets[i].Set(value); },
            merge_key);
    }

    // ------------------------------------
    // Comps
    // ------------------------------------

    public static object Transform_Get(ImpComp comp) => comp switch
    {
        ImpComp3D c3 => c3.transform,
        ImpComp2D c2 => c2.transform,
        _ => null,
    };

    public static void Transform_Set(ImpComp comp, object value)
    {
        if (comp is ImpComp3D c3 && value is TTransform3 t3) c3.transform = t3;
        else if (comp is ImpComp2D c2 && value is TTransform2 t2) c2.transform = t2;
    }

    public static TCompPlace Place_Get(ImpComp comp)
    {
        if (comp == null) return new TCompPlace { index = -1 };
        return new TCompPlace
        {
            parent = comp.parent,
            index = comp.parent?.children.IndexOf(comp) ?? -1,
            transform = Transform_Get(comp),
        };
    }

    public static void Place_Set(ImpComp comp, TCompPlace place)
    {
        if (comp == null) return;
        if (place.parent == null) comp.Detach();
        else if (place.index >= 0) place.parent.Child_Insert(place.index, comp);
        else place.parent.Child_Add(comp);
        // Reparenting keeps the comp where it looks like it is by rewriting its local transform,
        // so putting it back has to restore that too or it drifts.
        Transform_Set(comp, place.transform);
    }

    /// <summary>
    /// Records a comp arriving where it now is, from wherever <paramref name="before"/> says it
    /// was. Add, delete and reparent are all this one move - they only differ in which end is empty.
    /// </summary>
    public static void Comp_Moved(ImpComp comp, TCompPlace before, string label)
    {
        if (comp == null || is_applying || active == null) return;
        TCompPlace after = Place_Get(comp);
        Push(label, () => Place_Set(comp, before), () => Place_Set(comp, after));
    }

    /// <summary>Records a 2D comp's local transform and pivot as one step (gizmo drags).</summary>
    public static void Comp_Transformed(ImpComp2D comp, TTransform2 before, Vector2 before_pivot, string label)
    {
        if (comp == null) return;
        TTransform2 after = comp.transform;
        Vector2 after_pivot = comp.pivot;
        if (before.Equals(after) && before_pivot == after_pivot) return;
        Push(label,
            () => { comp.transform = before; comp.pivot = before_pivot; },
            () => { comp.transform = after; comp.pivot = after_pivot; });
    }

    /// <summary>Records a 3D comp's local transform as one step (gizmo drags).</summary>
    public static void Comp_Transformed(ImpComp3D comp, TTransform3 before, string label)
    {
        if (comp == null) return;
        TTransform3 after = comp.transform;
        if (before.Equals(after)) return;
        Push(label, () => comp.transform = before, () => comp.transform = after);
    }
}
