using System.Numerics;
using Editor.Panels;
using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Config;
using R3D_cs;

namespace Editor.Windows;

// Project settings. Every concrete non-editor ImpConfig (EditorConfig lives in the editor
// assembly and is excluded) is listed down the left; selecting one edits it in the inspector on
// the right. Each field edit is written straight back to that config's own TOML file in the
// project's Config folder, so settings persist the instant they change.
public class WND_SettingsProject : EditorWindow
{
    public override string Title => "Settings: Project";

    public PNL_Inspector ui_inspector = new();

    // one live instance per discovered config type, loaded from disk and kept for the window's life
    readonly List<ImpConfig> _configs = new();
    ImpConfig? _selected;

    float _list_width = 200f;

    public WND_SettingsProject()
    {
        panels = [ui_inspector];
        ui_inspector.on_changed = SaveSelected;   // any inspector edit -> write config file
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        LoadConfigs();
    }

    // Discovers every concrete, default-constructible ImpConfig in the engine assembly and loads
    // each from its file. The engine assembly holds only the shared project configs (CFG_*);
    // EditorConfig, being editor-only, lives elsewhere and is never picked up here.
    void LoadConfigs()
    {
        _configs.Clear();
        var types = typeof(ImpConfig).Assembly.GetTypes()
            .Where(t => typeof(ImpConfig).IsAssignableFrom(t) && !t.IsAbstract
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name);

        foreach (var t in types)
        {
            var cfg = (ImpConfig)Activator.CreateInstance(t)!;
            cfg.Load(cfg.FilePath(ImpFile.s_projectDir));
            _configs.Add(cfg);
        }

        Select(_configs.FirstOrDefault());
    }

    void Select(ImpConfig? cfg)
    {
        _selected = cfg;
        ui_inspector.selected_objects = cfg != null ? new List<object> { cfg } : new();
    }

    // Persists the selected config immediately — the inspector reports on the same frame a field
    // is edited, so the file always reflects the latest value. Graphics AA is also pushed into
    // R3D so the level viewport updates without restarting the editor.
    void SaveSelected()
    {
        if (_selected == null) return;
        _selected.Save(_selected.FilePath(ImpFile.s_projectDir));
        if (_selected is CFG_Graphics gfx)
            R3D.SetAntiAliasingMode(gfx.antialiasing_mode);
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        // --- config list (left) ---
        ImGui.BeginChild("config_list", new Vector2(_list_width, 0), ImGuiChildFlags.Borders);
        foreach (var cfg in _configs)
            if (ImGui.Selectable(cfg.ConfigName, cfg == _selected))
                Select(cfg);
        ImGui.EndChild();

        ImGui.SameLine();

        // --- inspector for the selected config (right) ---
        ImGui.BeginChild("config_inspector", new Vector2(0, 0));
        if (_selected == null) ImGui.TextDisabled("No configs found");
        else ui_inspector.Draw(delta);
        ImGui.EndChild();
    }
}
