namespace Editor;

[Flags]
public enum EEditorWidgetDrawFlags
{
    NONE = 0,
}

public class EditorWidget
{
    public bool has_focus;
    public bool is_open;

    public void Open()
    {
        if (is_open) return;
        is_open = true;
        OnOpen();
    }

    public void Close()
    {
        if (!is_open) return;
        is_open = false;
        OnClose();
    }

    public void Update(double delta)
    {
        if (is_open) OnUpdate(delta);
    }

    public void Draw(double delta, EEditorWidgetDrawFlags flags = EEditorWidgetDrawFlags.NONE)
    {
        if (is_open) OnDraw(delta, flags);
    }

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
