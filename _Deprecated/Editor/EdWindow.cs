using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;

namespace Editor;

public class EdWindow : C2_Box
{
    // Undo history for whatever this window edits. Windows that hold documents in tabs (scenes,
    // assets) hand this over to the open tab's own history when it updates, just after this.
    public ImpUndo undo = new();

    public EdWindow()
    {
        view_alighnment_H = EUIViewportAlignment.Fill;
        view_alighnment_V = EUIViewportAlignment.Fill;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        // The window on screen owns the history, so Ctrl+Z hits what the user is looking at.
        if (IsVisibleInTree()) ImpUndo.active = undo;
    }

    public void Asset_OnOpen(ImpAsset asset)
    {
        
    }
}