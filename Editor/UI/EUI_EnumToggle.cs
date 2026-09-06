using System.Reflection;
using Engine;
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
            string label = v.GetType().GetField(v.ToString())?.GetCustomAttribute<TitleAttribute>()?.Name
                           ?? v.ToString();

            bool hit = EdIcons.Button("##et" + v.GetType().Name + "." + v, label, EdIcons.Enum(v), selected);
            if (hit && !selected)
            {
                value = v;
                on_changed?.Invoke(v);
            }
        }
    }
}
