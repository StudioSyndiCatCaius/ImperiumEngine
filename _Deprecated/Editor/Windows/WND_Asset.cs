using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;

namespace Editor.Windows;

public class WND_Asset : EdWindow
{
    public static WND_Asset active;

    public C2_TabBox tab_assets = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };

    public WND_Asset()
    {
        active = this;
        name = "Assets";
        Child_Add(tab_assets);
    }

    public void Asset_Add(ImpAsset asset)
    {
        if (asset == null) return;
        int page = 0;
        for (int i = 0; i < tab_assets.children.Count; i++)
        {
            if (tab_assets.children[i] == tab_assets.list_tabs) continue;
            if (tab_assets.children[i] is EdAssetEditor ed && SameAsset(ed.asset, asset))
            {
                tab_assets.selected_tab = page;
                return;
            }
            page++;
        }

        EdAssetEditor editor = new()
        {
            asset = asset,
            name = asset.GetName(),
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
        };
        editor.Rebuild();
        tab_assets.Child_Add(editor);
        tab_assets.selected_tab = page;
    }

    static bool SameAsset(ImpAsset a, ImpAsset b)
    {
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;
        if (string.IsNullOrEmpty(a.filepath) || string.IsNullOrEmpty(b.filepath)) return false;
        try { return string.Equals(Path.GetFullPath(a.filepath), Path.GetFullPath(b.filepath), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a.filepath, b.filepath, StringComparison.OrdinalIgnoreCase); }
    }
}

public class EdAssetEditor : EdPanel
{
    public ImpAsset asset;
    public C2_Inspector inspector;
    public ImpUndo undo = new();

    public EdAssetEditor()
    {
        name = "Asset Editor";
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!IsVisibleInTree()) return;
        undo.asset = asset;
        ImpUndo.active = undo;
    }

    public void Rebuild()
    {
        Child_RemoveAll();
        inspector = new C2_Inspector
        {
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
        };
        Child_Add(inspector);
        if (asset != null)
        {
            name = asset.GetName();
            inspector.Object_Add(asset, true);
        }
    }
}
