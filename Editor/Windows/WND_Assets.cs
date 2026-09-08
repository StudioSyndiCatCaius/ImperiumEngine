using Editor.Panels;
using Engine.Core;
using ImGuiNET;

namespace Editor.Windows;

public class WND_Assets : EdWindow
{
    public static WND_Assets? active;
    public static bool request_focus;

    public List<EUI_Asset> open_asset_tabs = new();
    public ImpAsset? current_asset;
    int _focus_tab = -1;

    public WND_Assets()
    {
        active = this;
    }

    public static void Open(ImpAsset? asset)
    {
        if (asset == null) return;
        if (active == null) active = new WND_Assets();

        for (int i = 0; i < active.open_asset_tabs.Count; i++)
        {
            if (ReferenceEquals(active.open_asset_tabs[i].asset, asset))
            {
                active._focus_tab = i;
                request_focus = true;
                return;
            }
        }

        active.open_asset_tabs.Add(new EUI_Asset { asset = asset });
        active._focus_tab = active.open_asset_tabs.Count - 1;
        request_focus = true;
    }

    public override void OnDraw()
    {
        base.OnDraw();

        if (open_asset_tabs.Count == 0)
        {
            current_asset = null;
            ImGui.TextDisabled("No asset open");
            return;
        }

        if (!ImGui.BeginTabBar("##asset_tabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs))
            return;

        ImpAsset? visible = current_asset;
        for (int i = 0; i < open_asset_tabs.Count; i++)
        {
            EUI_Asset tab = open_asset_tabs[i];
            bool open = true;
            ImpAsset? a = tab.asset;
            string name = a == null ? "Asset" : (string.IsNullOrEmpty(a.filepath)
                ? a.GetType().Name
                : Path.GetFileNameWithoutExtension(a.filepath));
            ImGuiTabItemFlags flags = a is { is_dity: true }
                ? ImGuiTabItemFlags.UnsavedDocument
                : ImGuiTabItemFlags.None;
            if (i == _focus_tab) flags |= ImGuiTabItemFlags.SetSelected;

            if (!ImGui.BeginTabItem(name + "###a" + tab.GetHashCode(), ref open, flags))
            {
                if (!open)
                {
                    open_asset_tabs.RemoveAt(i);
                    i--;
                }
                continue;
            }

            visible = a;
            tab.OnDraw();
            ImGui.EndTabItem();
            if (!open)
            {
                open_asset_tabs.RemoveAt(i);
                i--;
            }
        }

        current_asset = visible;
        _focus_tab = -1;
        ImGui.EndTabBar();
    }
}

public class EUI_Asset : EdUi
{
    public ImpAsset? asset;
    readonly PNL_Inspector _inspector = new() { title = "Asset" };

    public override void OnDraw()
    {
        _inspector.selected_object = asset;
        _inspector.OnDrawPanel();
    }
}
