namespace Editor;

public struct TEdPopupOption
{
    public string text;
    public Action on_select;
    public List<TEdPopupOption> suboptions;
}

public class EdUi
{
    public virtual void OnDraw() { }
    
    public virtual List<TEdPopupOption> Popup_GetOptions() { return null; }
    
    public virtual bool DragAndDrop_Enabled() { return false; }
    public virtual void DragAndDrop_OnDrop() { } //probably should have an arg for what dropped on
}