using System.Numerics;
using Editor.Dialog;
using Editor.Panel;
using ImperiumEngine;
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

    public PNL_FileBrowser file_browser = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
            size_min = new(0, 200),
        },
    };

    C2_Expandable file_browser_wrap;

    public WND_Asset()
    {
        active = this;
        name = "Assets";
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;

        C2_List list_asset_file = new()
        {
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
            orentation = EUIOrentation.V,
        };

        file_browser_wrap = new()
        {
            name = "File Browser",
            is_expanded = true,
            bar_height = 22,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
                size_min = new(0, 22),
            },
            stretch_ratio = 0.5f,
        };
        file_browser_wrap.Child_Add(file_browser);

        list_asset_file.Child_Add(tab_assets);
        list_asset_file.Child_Add(new C2_Seperator { orentation = EUIOrentation.V });
        list_asset_file.Child_Add(file_browser_wrap);
        Child_Add(list_asset_file);

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

        EdAssetEditor editor = new()
        {
            asset = asset,
            name = asset.GetName(),
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
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
        data.file_browser_asset_expanded = file_browser_wrap == null || file_browser_wrap.is_expanded;
        if (file_browser_wrap != null)
        {
            data.browser_asset_stretch = file_browser_wrap.stretch_ratio;
        }
        data.asset_tabs_stretch = tab_assets.stretch_ratio;
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
        if (file_browser_wrap != null)
        {
            file_browser_wrap.is_expanded = data.file_browser_asset_expanded;
            if (EdState.Stretch_IsWeight(data.browser_asset_stretch))
            {
                file_browser_wrap.stretch_ratio = data.browser_asset_stretch;
            }
        }
        if (EdState.Stretch_IsWeight(data.asset_tabs_stretch))
        {
            tab_assets.stretch_ratio = data.asset_tabs_stretch;
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
        string folder = file_browser.CurrentDir;
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
            layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Fill,
                    orient_V = EUIViewportAlignment.Fill,
                },
        };
        Child_Add(inspector);
        if (asset != null)
        {
            name = asset.GetName();
            inspector.Object_Add(asset, true);
        }
    }
    
    
}
