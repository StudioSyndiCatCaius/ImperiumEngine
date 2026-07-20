using System.Numerics;
using ImGuiNET;

namespace Editor;

// a popup dialog, hogging all focus and input until it is closed.
//
// Dialogs are modal: while one is open the rest of the editor is click-blocked. Call Show()
// from anywhere (a menu, the inspector...) to queue it; the editor loop draws every active
// dialog once per frame via DrawActive(). A dialog dismisses itself with Dismiss() (usually
// from a button in OnDraw), or the user can close it with the window's X.
public class EditorDialog : EditorWidget
{
    // active modals, drawn each frame by the editor loop. Last = most recently shown.
    static readonly List<EditorDialog> s_active = new();

    public virtual string Title => GetType().Name;

    // true while any modal dialog owns input — used by the editor to suppress global hotkeys
    public static bool AnyActive => s_active.Any(d => d.is_open);

    bool _needs_open;   // request ImGui.OpenPopup on the next draw

    // stable popup id: the visible title plus an instance tag, so two dialogs with the same
    // title don't collide on ImGui's popup stack
    string PopupId => $"{Title}##dlg{GetHashCode()}";

    // Queues this dialog as a modal popup. No-op if already open.
    public void Show()
    {
        if (is_open) return;
        Open();               // sets is_open + fires OnOpen
        _needs_open = true;
        if (!s_active.Contains(this)) s_active.Add(this);
    }

    // Dismisses this dialog from inside OnDraw (closes the popup and fires OnClose).
    protected void Dismiss()
    {
        ImGui.CloseCurrentPopup();
        Close();
    }

    // Draws every active modal; call once per frame inside the ImGui pass.
    public static void DrawActive(double delta)
    {
        // copy — a dialog may open another (or close itself) while drawing
        foreach (var d in s_active.ToArray())
            d.DrawModal(delta);
        s_active.RemoveAll(d => !d.is_open);
    }

    void DrawModal(double delta)
    {
        if (!is_open) return;

        if (_needs_open)
        {
            ImGui.OpenPopup(PopupId);
            _needs_open = false;
        }

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        bool keep = true;
        if (ImGui.BeginPopupModal(PopupId, ref keep, ImGuiWindowFlags.AlwaysAutoResize))
        {
            has_focus = true;
            OnDraw(delta, EEditorWidgetDrawFlags.NONE);
            ImGui.EndPopup();
        }

        // user closed it via the title-bar X
        if (!keep && is_open) Close();
    }
}
