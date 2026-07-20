using ImGuiNET;
using ImperiumEngine.Classes;

namespace Editor;

//a dockable window, and the main window class for the editor. Can had subpanels bound to its lifetime
public class EditorWindow : EditorWidget
{
    public EditorPanel[] panels = [];

    public virtual string Title => GetType().Name;

    //stable ImGui/docking identity, kept separate from the (possibly dynamic) visible Title so
    //renaming the tab — e.g. when the open level changes — doesn't reset the docked layout.
    public virtual string WindowId => Title;

    //windows that can never be closed (like the level editor) keep the program alive
    public virtual bool CanClose => true;

    //the asset this window edits (its "document"), if any. Drives the dirty "*" in the tab
    //title and is the target of the editor's Save / Save As hotkeys while this window is focused.
    public virtual ImpAsset? DocumentAsset => null;

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

        // "* " prefix marks unsaved changes; the "###Title" suffix keeps a stable ImGui id
        // (and docking identity) even as the visible label gains/loses the asterisk.
        bool dirty = DocumentAsset is { is_dirty: true };
        string label = $"{(dirty ? "* " : "")}{Title}###{WindowId}";

        bool visible;
        if (CanClose)
        {
            bool keep_open = true;
            visible = ImGui.Begin(label, ref keep_open);
            if (!keep_open)
            {
                ImGui.End();
                Close();
                return;
            }
        }
        else
        {
            visible = ImGui.Begin(label);
        }

        has_focus = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (visible) OnDraw(delta, EEditorWidgetDrawFlags.NONE);
        ImGui.End();
    }
}
