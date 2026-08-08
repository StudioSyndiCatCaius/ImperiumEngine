using System.Numerics;
using Editor.Panels;
using ImGuiNET;
using ImperiumEngine.Classes;

namespace Editor.Windows;

// Multi-document asset editor: a tab bar of every open asset across the top, each tab showing that
// asset's property inspector (always) plus any asset-specific editor elements (a texture preview, a
// 3D entity view + component tree, a shader graph...). Those type-specific panels are stubbed for
// now — see BuildEditorPanels — but the inspector path is fully live.
public class WND_AssetEdit : EditorWindow
{
    // one open asset and everything the editor keeps alongside it
    class AssetTab
    {
        public required ImpAsset asset;
        public required string id;                    // stable ImGui id (survives dirty "*" retitling)
        public PNL_Inspector inspector = new();
        public UndoHistory history = new();           // this asset's own undo/redo stack
        public List<EditorPanel> panels = new();      // asset-specific editor panels (empty for now)

        public string Name => asset.is_reference
            ? Path.GetFileNameWithoutExtension(asset.file_link)
            : $"{asset.GetType().Name} (unsaved)";
    }

    readonly List<AssetTab> _tabs = new();
    AssetTab? _active;
    AssetTab? _focus_tab;     // set to programmatically bring a tab to front for one frame
    int _next_id;

    float _side_width = 340f; // inspector column width when asset-specific panels are shown

    public override string Title => "Asset Editor";
    public override string WindowId => "Asset Editor";   // fixed id: retitling never resets docking

    // the active tab's asset is this window's document, so the tab title's "*", the Save hotkeys and
    // the undo history all follow whichever asset is in front.
    public override ImpAsset? DocumentAsset => _active?.asset;

    // ---------------------------------------------------------------------------------------------
    // Opening assets
    // ---------------------------------------------------------------------------------------------

    // Opens an asset file in the (single) asset editor window, creating the window if needed and
    // focusing an already-open tab instead of loading it twice. Called from the content browser.
    public static void OpenAsset(string filepath)
    {
        var win = Program.windows_open.OfType<WND_AssetEdit>().FirstOrDefault();
        if (win == null)
        {
            win = new WND_AssetEdit();
            win.Open();
            Program.windows_open.Add(win);
        }
        win.focus_next = true;
        win.OpenTab(filepath);
    }

    // Loads (or re-focuses) a single asset as a tab. No-op if the file can't be read as an asset.
    void OpenTab(string filepath)
    {
        // already open? just bring its tab to front
        var existing = _tabs.FirstOrDefault(t =>
            string.Equals(t.asset.file_link, filepath, StringComparison.OrdinalIgnoreCase));
        if (existing != null) { _focus_tab = existing; return; }

        var asset = ImpAsset.LoadFile(filepath);
        if (asset == null)
        {
            Console.WriteLine($"[AssetEditor] Could not open asset: {filepath}");
            return;
        }
        AddTab(asset);
    }

    void AddTab(ImpAsset asset)
    {
        var tab = new AssetTab { asset = asset, id = $"tab{_next_id++}" };
        tab.inspector.selected_objects = [asset];
        tab.inspector.on_changed = () => asset.is_dirty = true;
        tab.inspector.on_action = a => tab.history.Push(a);

        tab.panels = BuildEditorPanels(asset);

        asset.OnEditorWindow_Open();
        tab.inspector.Open();
        foreach (var p in tab.panels) p.Open();

        _tabs.Add(tab);
        _focus_tab = tab;
    }

    void CloseTab(AssetTab tab)
    {
        tab.asset.OnEditorWindow_Close();
        tab.inspector.Close();
        foreach (var p in tab.panels) p.Close();
        _tabs.Remove(tab);
        if (_active == tab) _active = null;
    }

    // Remaps file_link / file_source on every open tab (and nested asset slots) after a content
    // folder rename. Called from the File Explorer.
    public void RemapPaths(string old_dir, string new_dir)
    {
        foreach (var tab in _tabs)
            ImpFile.RemapAssetPathsInMemory(tab.asset, old_dir, new_dir);
    }

    // Closes any tab whose backing file lives under `dir` (used when that folder is deleted).
    public void CloseTabsUnder(string dir)
    {
        foreach (var tab in _tabs.ToArray())
            if (ImpFile.Path_IsUnder(tab.asset.file_link, dir))
                CloseTab(tab);
    }

    // Factory for an asset's type-specific editor panels. Fleshed out per asset type later (texture
    // preview, entity viewport + component tree, shader graph...); for now every asset is inspector-only.
    static List<EditorPanel> BuildEditorPanels(ImpAsset asset) => new();

    // ---------------------------------------------------------------------------------------------
    // Lifetime
    // ---------------------------------------------------------------------------------------------

    protected override void OnUpdate(double delta)
    {
        foreach (var tab in _tabs)
        {
            tab.inspector.Update(delta);
            foreach (var p in tab.panels) p.Update(delta);
        }
    }

    protected override void OnClose()
    {
        foreach (var tab in _tabs.ToArray()) CloseTab(tab);
    }

    // ---------------------------------------------------------------------------------------------
    // Draw
    // ---------------------------------------------------------------------------------------------

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        if (_tabs.Count == 0)
        {
            ImGui.TextDisabled("No asset open.");
            ImGui.TextDisabled("Double-click an asset in the File Explorer to edit it.");
            return;
        }

        var to_close = new List<AssetTab>();

        if (ImGui.BeginTabBar("asset_tabs",
                ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs |
                ImGuiTabBarFlags.FittingPolicyScroll))
        {
            foreach (var tab in _tabs)
            {
                bool open = true;
                string label = $"{(tab.asset.is_dirty ? "* " : "")}{tab.Name}###{tab.id}";
                var tab_flags = tab == _focus_tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;

                if (ImGui.BeginTabItem(label, ref open, tab_flags))
                {
                    _active = tab;
                    DrawTabBody(tab, delta);
                    ImGui.EndTabItem();
                }

                if (!open) to_close.Add(tab);
            }
            ImGui.EndTabBar();
        }
        _focus_tab = null;

        foreach (var tab in to_close) CloseTab(tab);

        // route this window's Save / Undo / Redo (handled in Program after the draw) at the active tab
        if (_active != null) history = _active.history;
    }

    void DrawTabBody(AssetTab tab, double delta)
    {
        // no asset-specific panels yet: the inspector gets the whole tab
        if (tab.panels.Count == 0)
        {
            DrawInspector(tab, delta);
            return;
        }

        // otherwise the asset-specific panels fill the left, the inspector docks to a resizable right column
        var avail = ImGui.GetContentRegionAvail();
        _side_width = Math.Clamp(_side_width, 200f, Math.Max(200f, avail.X - 200f));
        float splitter_w = 4f;
        float left_w = avail.X - _side_width - splitter_w - ImGui.GetStyle().ItemSpacing.X * 2;

        ImGui.BeginChild("asset_elements", new Vector2(left_w, 0), ImGuiChildFlags.Borders);
        foreach (var p in tab.panels)
        {
            ImGui.SeparatorText(p.Title);
            p.Draw(delta);
        }
        ImGui.EndChild();

        ImGui.SameLine();
        SplitterVertical("asset_splitter", splitter_w, ref _side_width);
        ImGui.SameLine();

        ImGui.BeginChild("asset_inspector", new Vector2(0, 0), ImGuiChildFlags.Borders);
        DrawInspector(tab, delta);
        ImGui.EndChild();
    }

    static void DrawInspector(AssetTab tab, double delta)
    {
        ImGui.SeparatorText("Properties");
        tab.inspector.Draw(delta);
    }

    // draggable vertical bar — dragging left grows the inspector column
    static void SplitterVertical(string id, float thickness, ref float side_width)
    {
        ImGui.InvisibleButton(id, new Vector2(thickness, -1));
        if (ImGui.IsItemActive())
            side_width -= ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
    }
}
