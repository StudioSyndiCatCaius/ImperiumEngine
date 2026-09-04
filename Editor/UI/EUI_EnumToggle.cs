using ImGuiNET;

namespace Editor.UI;

public class EUI_EnumToggle : EdUi
{
    public Enum value = null;
    public Action<Enum> on_changed = null;

    public override void OnDraw()
    {
        base.OnDraw();
        if (value == null) return;

        Array values = Enum.GetValues(value.GetType());
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) ImGui.SameLine();

            Enum v = (Enum)values.GetValue(i)!;
            bool selected = value.Equals(v);

            if (selected)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);

            if (ImGui.Button(v.ToString()) && !selected)
            {
                value = v;
                on_changed?.Invoke(v);
            }

            if (selected)
                ImGui.PopStyleColor();
        }
    }
}
