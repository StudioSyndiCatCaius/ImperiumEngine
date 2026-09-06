using ImGuiNET;

namespace Editor.UI;

public class EUI_SearchBar : EdUi
{
    public string search_text = "";
    public string hint = "Search";
    public bool focus_next;

    public Action<string> on_search = null; //Only when string is changed
    public override void OnDraw()
    {
        base.OnDraw();
        ImGui.SetNextItemWidth(-1);
        if (focus_next)
        {
            ImGui.SetKeyboardFocusHere();
            focus_next = false;
        }
        string v = search_text ?? "";
        if (ImGui.InputTextWithHint("##search", hint, ref v, 256))
        {
            search_text = v;
            on_search?.Invoke(v);
        }
    }
}
