using System.Numerics;
using Editor.Dialog;
using Editor.Panel;
using Editor.Scenes;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
namespace Editor.Windows;

public class WND_Asset : EdWindow
{
    public static WND_Asset active;

    public C2_TabBox tab_assets = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
            size_min = new(0, 120),
        },
    };

    public WND_Asset()
    {
        active = this;
        name = "Assets";
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;

        Child_Add(tab_assets);

        tab_assets.show_close_tab_button = true;
        tab_assets.request_close_tab = Asset_Close;
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

        EdAssetEditor editor = EdAssetEditor.Create(asset);
        editor.asset = asset;
        editor.name = asset.GetName();
        editor.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        };
        editor.Rebuild();
        tab_assets.Child_Add(editor);
        tab_assets.selected_tab = page;
    }

    public void Asset_Close(int page)
    {
        int i_page = 0;
        for (int i = 0; i < tab_assets.children.Count; i++)
        {
            ImpComp c = tab_assets.children[i];
            if (c == tab_assets.list_tabs)
            {
                continue;
            }
            if (i_page != page)
            {
                i_page++;
                continue;
            }

            bool was_sel = tab_assets.selected_tab == page;
            bool before = page < tab_assets.selected_tab;
            c.Destroy();
            if (before)
            {
                tab_assets.selected_tab--;
            }
            else if (was_sel)
            {
                int n = 0;
                for (int k = 0; k < tab_assets.children.Count; k++)
                {
                    if (tab_assets.children[k] == tab_assets.list_tabs)
                    {
                        continue;
                    }
                    n++;
                }
                if (tab_assets.selected_tab >= n)
                {
                    tab_assets.selected_tab = Math.Max(0, n - 1);
                }
            }
            return;
        }
    }

    public void Asset_CloseAll()
    {
        for (int i = tab_assets.children.Count - 1; i >= 0; i--)
        {
            ImpComp c = tab_assets.children[i];
            if (c == tab_assets.list_tabs)
            {
                continue;
            }
            c.Destroy();
        }
        tab_assets.selected_tab = 0;
    }

    public void State_Capture(EdStateData data)
    {
        if (data == null)
        {
            return;
        }
        data.active_asset = tab_assets.selected_tab;
        data.assets.Clear();
        for (int i = 0; i < tab_assets.children.Count; i++)
        {
            if (tab_assets.children[i] is not EdAssetEditor ed || ed.asset == null)
            {
                continue;
            }
            if (!ed.asset.File_CanWrite())
            {
                continue;
            }
            data.assets.Add(EdState.Path_Store(ed.asset.filepath));
        }
    }

    public void State_Apply(EdStateData data)
    {
        if (data == null)
        {
            return;
        }
        if (data.assets.Count == 0)
        {
            return;
        }
        Asset_CloseAll();
        for (int i = 0; i < data.assets.Count; i++)
        {
            string path = EdState.Path_Load(data.assets[i]);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }
            ImpAsset asset = ImpAsset.Load(path);
            if (asset == null)
            {
                continue;
            }
            Asset_Add(asset);
        }
        if (data.active_asset >= 0)
        {
            tab_assets.selected_tab = data.active_asset;
        }
    }

    static bool SameAsset(ImpAsset a, ImpAsset b)
    {
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;
        if (string.IsNullOrEmpty(a.filepath) || string.IsNullOrEmpty(b.filepath)) return false;
        try { return string.Equals(Path.GetFullPath(a.filepath), Path.GetFullPath(b.filepath), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a.filepath, b.filepath, StringComparison.OrdinalIgnoreCase); }
    }
    
    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        TabNames_Sync();
    }

    public override void OnTrySave()
    {
        ImpAsset asset = ActiveEditor()?.asset;
        if (asset == null) return;
        if (asset.File_CanWrite())
        {
            asset.File_Write();
            AfterSaved();
            return;
        }
        OnTrySaveAs();
    }

    public override void OnTrySaveAs()
    {
        ImpAsset asset = ActiveEditor()?.asset;
        if (asset == null) return;
        string folder = Scene_Editor.active?.file_browser.CurrentDir;
        DLG_SaveFile.Run(asset, path =>
        {
            asset.File_SaveTo(path);
            AfterSaved();
        }, folder);
    }

    void AfterSaved()
    {
        TabNames_Sync();
        PNL_FileBrowser.Browsers_Notify();
    }

    EdAssetEditor ActiveEditor()
    {
        int page = 0;
        for (int i = 0; i < tab_assets.children.Count; i++)
        {
            if (tab_assets.children[i] == tab_assets.list_tabs) continue;
            if (tab_assets.children[i] is not EdAssetEditor ed) continue;
            if (page == tab_assets.selected_tab) return ed;
            page++;
        }
        return null;
    }

    void TabNames_Sync()
    {
        for (int i = 0; i < tab_assets.children.Count; i++)
        {
            if (tab_assets.children[i] is not EdAssetEditor ed) continue;
            if (ed.asset == null) continue;
            string n = ed.asset.GetName();
            if (ed.asset.is_dirty && ed.asset.File_IsValid() && !n.EndsWith("*"))
            {
                n += "*";
            }
            if (ed.name != n)
            {
                ed.name = n;
            }
        }
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

    public static EdAssetEditor Create(ImpAsset asset)
    {
        if (asset is A_Texture)
        {
            return new EdAssetEditor_Texture();
        }
        if (asset is A_Mesh)
        {
            return new EdAssetEditor_Mesh();
        }
        if (asset is A_Material)
        {
            return new EdAssetEditor_Material();
        }
        return new EdAssetEditor();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!IsVisibleInTree()) return;
        undo.asset = asset;
        ImpUndo.active = undo;
    }

    public virtual void Rebuild()
    {
        Child_RemoveAll();
        inspector = new C2_Inspector
        {
            layout = TLayout2.FULL,
        };
        Child_Add(inspector);
        BindAsset();
    }

    protected void BindAsset()
    {
        if (asset == null)
        {
            return;
        }
        name = asset.GetName();
        inspector.Object_Add(asset, true);
    }

    protected void Layout_Split(Imp2D extra)
    {
        Child_RemoveAll();
        inspector = new C2_Inspector
        {
            layout = new TLayout2
            {
                size = new Vector2(280, 0),
                size_min = new Vector2(180, 0),
                orient_V = EUIViewportAlignment.Fill,
            },
        };
        extra.layout = TLayout2.FULL;

        C2_List row = new()
        {
            orentation = EUIOrentation.H,
            layout = TLayout2.FULL,
        };
        row.Child_Add(inspector);
        row.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        row.Child_Add(extra);
        Child_Add(row);
        BindAsset();
    }
}
