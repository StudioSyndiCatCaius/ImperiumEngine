namespace Editor;

//a dockable panel within a window. removed when its owning window is closed
public class EditorPanel : EditorWidget
{
    public virtual string Title => GetType().Name;
}
