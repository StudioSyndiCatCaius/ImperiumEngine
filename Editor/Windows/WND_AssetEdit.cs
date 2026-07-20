using ImGuiNET;

namespace Editor.Windows;

public class WND_AssetEdit : EditorWindow
{
    public override string Title => "Asset Editor";

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        ImGui.TextDisabled("TODO: asset editing");
    }
}
