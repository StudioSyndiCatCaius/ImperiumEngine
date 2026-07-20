using ImGuiNET;

namespace Editor;

//a dockable window, and the main window class for the editor. Can had subpanels bound to its lifetime
public class EditorWindow : EditorWidget
{
    public EditorPanel[] panels = [];

    public virtual string Title => GetType().Name;

    //windows that can never be closed (like the level editor) keep the program alive
    public virtual bool CanClose => true;

    protected override void OnOpen()
    {
        foreach (var p in panels) p.Open();
    }

    protected override void OnClose()
    {
        foreach (var p in panels) p.Close();
    }

    protected override void OnUpdate(double delta)
    {
        foreach (var p in panels) p.Update(delta);
    }

    //draws the ImGui window docked into the main dockspace, then the window contents
    public void DrawWindow(double delta, uint dock_id)
    {
        if (!is_open) return;

        ImGui.SetNextWindowDockID(dock_id, ImGuiCond.FirstUseEver);

        bool visible;
        if (CanClose)
        {
            bool keep_open = true;
            visible = ImGui.Begin(Title, ref keep_open);
            if (!keep_open)
            {
                ImGui.End();
                Close();
                return;
            }
        }
        else
        {
            visible = ImGui.Begin(Title);
        }

        has_focus = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (visible) OnDraw(delta, EEditorWidgetDrawFlags.NONE);
        ImGui.End();
    }
}
