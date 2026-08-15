using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace Editor;



[ImpClass(Hidden = true)]
public class EdWindow : C2_Box
{
    // Undo history for whatever this window edits. Windows that hold documents in tabs (scenes,
    // assets) hand this over to the open tab's own history when it updates, just after this.
    public ImpUndo undo = new();

    public EdWindow() { layout=TLayout2.FULL; }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        // The window on screen owns the history, so Ctrl+Z hits what the user is looking at.
        if (IsVisibleInTree()) ImpUndo.active = undo;
    }

    public void Asset_OnOpen(ImpAsset asset)
    {
        
    }

    // File menu / toolbar / hotkeys. The frontmost window decides what "this document" is.
    public virtual void OnTrySave()
    {
    }

    public virtual void OnTrySaveAs()
    {
    }
}