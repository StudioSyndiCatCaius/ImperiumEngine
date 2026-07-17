namespace Editor;

[Flags]
public enum EEditorWidgetDrawFlags
{
    
}

public class EditorWidget
{
    public bool has_focus;
    
    protected virtual void OnOpen()
    {
        
    }
    
    protected virtual void OnClose()
    {
        
    }
    
    protected virtual void OnUpdate(double delta)
    {
        
    }
    
    protected virtual void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        
    }
}