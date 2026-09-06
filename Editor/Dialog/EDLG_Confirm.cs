using ImGuiNET;

namespace Editor.Dialog;

public class EDLG_Confirm : EdDialog
{
    static string _msg = "";
    static Action<bool>? _on_close;
    static bool _want_open;

    public static void Run(string msg, Action<bool> on_close)
    {
        _msg = msg ?? "";
        _on_close = on_close;
        _want_open = true;
    }

    public static void DrawPending()
    {
        if (_want_open)
        {
            ImGui.OpenPopup("Confirm##ed_confirm");
            _want_open = false;
        }

        bool open = true;
        if (!ImGui.BeginPopupModal("Confirm##ed_confirm", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextWrapped(_msg);
        ImGui.Spacing();
        if (ImGui.Button("Yes", new System.Numerics.Vector2(80, 0)))
        {
            ImGui.CloseCurrentPopup();
            _on_close?.Invoke(true);
            _on_close = null;
        }
        ImGui.SameLine();
        if (ImGui.Button("No", new System.Numerics.Vector2(80, 0)) || !open)
        {
            ImGui.CloseCurrentPopup();
            _on_close?.Invoke(false);
            _on_close = null;
        }
        ImGui.EndPopup();
    }
}
