using Editor.Panel;
using ImperiumEngine;
using ImperiumEngine.Enums;

namespace Editor;

public class EdEditMode
{
    public PNL_SceneView view;

    public virtual bool IsBusy => false;
    public virtual bool BlocksCamera => IsBusy;
    public virtual bool BlocksWheel => false;

    public virtual void OnBegin() { }
    public virtual void OnEnd() { }
    public virtual void OnHidden() { }
    public virtual void OnUpdate(double dt) { }
    public virtual void OnDraw2DForeground(double dt, EDrawFlags flags) { }
    public virtual void OnCursorUpdate(ImpPlayer player, double dt) { }
    public virtual void OnGrabDrop(ImpPlayer player, ENotifyGrabTarget notify, ImpComp other, double dt) { }
    public virtual void DuplicateSelected() { }
    public virtual void DeleteSelected() { }
}
