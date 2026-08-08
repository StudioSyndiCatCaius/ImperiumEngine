using Editor.Panels;
using ImperiumEngine.Classes;

namespace Editor.Windows;

// Editor preferences. Edits the live EditorConfig (the same instance the app loads at startup and
// captures on exit) in the inspector, and writes its TOML file the instant any field changes.
// Unlike WND_SettingsProject there is only one editor config, so there's no list — just the inspector.
public class WND_SettingsEditor : EditorWindow
{
    public override string Title => "Settings: Editor";

    public PNL_Inspector ui_inspector = new();

    public WND_SettingsEditor()
    {
        panels = [ui_inspector];
        ui_inspector.on_changed = Save;   // any inspector edit -> write Editor.toml
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        ui_inspector.selected_objects = new List<object> { Program.config };
    }

    void Save() => Program.config.Save(EditorConfig.PathFor(ImpFile.s_projectDir));

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        ui_inspector.Draw(delta);
    }
}
