using Editor.Dialog;
using Editor.Panel;
using Editor.Scenes;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace Editor.Windows;

public class WND_Flow : EdWindow
{
    public static WND_Flow active;

    public C2_TabBox tab_flows = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
            size_min = new(0, 120),
        },
    };

    public WND_Flow()
    {
        active = this;
        name = "Flow";
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;

        tab_flows.show_close_tab_button = true;
        tab_flows.request_close_tab = Flow_Close;
        Child_Add(tab_flows);
    }

    public void Flow_Add(A_Flow flow)
    {
        if (flow == null)
        {
            return;
        }
        int page = 0;
        for (int i = 0; i < tab_flows.children.Count; i++)
        {
            if (tab_flows.children[i] == tab_flows.list_tabs)
            {
                continue;
            }
            if (tab_flows.children[i] is PNL_FlowGraph ed && SameAsset(ed.flow, flow))
            {
                tab_flows.selected_tab = page;
                return;
            }
            page++;
        }

        PNL_FlowGraph editor = new()
        {
            name = flow.GetName(),
            layout = TLayout2.FULL,
        };
        editor.Bind(flow);
        tab_flows.Child_Add(editor);
        tab_flows.selected_tab = page;
    }

    public void Flow_Close(int page)
    {
        int i_page = 0;
        for (int i = 0; i < tab_flows.children.Count; i++)
        {
            ImpComp c = tab_flows.children[i];
            if (c == tab_flows.list_tabs)
            {
                continue;
            }
            if (i_page != page)
            {
                i_page++;
                continue;
            }

            bool was_sel = tab_flows.selected_tab == page;
            bool before = page < tab_flows.selected_tab;
            c.Destroy();
            if (before)
            {
                tab_flows.selected_tab--;
            }
            else if (was_sel)
            {
                int n = 0;
                for (int k = 0; k < tab_flows.children.Count; k++)
                {
                    if (tab_flows.children[k] == tab_flows.list_tabs)
                    {
                        continue;
                    }
                    n++;
                }
                if (tab_flows.selected_tab >= n)
                {
                    tab_flows.selected_tab = Math.Max(0, n - 1);
                }
            }
            return;
        }
    }

    public void Flow_CloseAll()
    {
        for (int i = tab_flows.children.Count - 1; i >= 0; i--)
        {
            ImpComp c = tab_flows.children[i];
            if (c == tab_flows.list_tabs)
            {
                continue;
            }
            c.Destroy();
        }
        tab_flows.selected_tab = 0;
    }

    public void State_Capture(EdStateData data)
    {
        if (data == null)
        {
            return;
        }
        data.active_flow = tab_flows.selected_tab;
        data.flows.Clear();
        for (int i = 0; i < tab_flows.children.Count; i++)
        {
            if (tab_flows.children[i] is not PNL_FlowGraph ed || ed.flow == null)
            {
                continue;
            }
            if (!ed.flow.File_CanWrite())
            {
                continue;
            }
            data.flows.Add(EdState.Path_Store(ed.flow.filepath));
        }
    }

    public void State_Apply(EdStateData data)
    {
        if (data == null)
        {
            return;
        }
        if (data.flows.Count == 0)
        {
            return;
        }
        Flow_CloseAll();
        for (int i = 0; i < data.flows.Count; i++)
        {
            string path = EdState.Path_Load(data.flows[i]);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }
            ImpAsset asset = ImpAsset.Load(path);
            A_Flow flow = asset as A_Flow;
            if (flow == null)
            {
                continue;
            }
            Flow_Add(flow);
        }
        if (data.active_flow >= 0)
        {
            tab_flows.selected_tab = data.active_flow;
        }
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        TabNames_Sync();
        PNL_FlowGraph ed = ActiveEditor();
        if (ed != null && IsVisibleInTree())
        {
            undo = ed.undo;
            ImpUndo.active = ed.undo;
        }
    }

    public override void OnTrySave()
    {
        A_Flow flow = ActiveEditor()?.flow;
        if (flow == null)
        {
            return;
        }
        if (flow.File_CanWrite())
        {
            flow.File_Write();
            AfterSaved();
            return;
        }
        OnTrySaveAs();
    }

    public override void OnTrySaveAs()
    {
        A_Flow flow = ActiveEditor()?.flow;
        if (flow == null)
        {
            return;
        }
        string folder = SaveFolder();
        DLG_SaveFile.Run(flow, path =>
        {
            flow.File_SaveTo(path);
            AfterSaved();
        }, folder);
    }

    void AfterSaved()
    {
        TabNames_Sync();
        PNL_FileBrowser.Browsers_Notify();
    }

    public PNL_FlowGraph ActiveEditor()
    {
        int page = 0;
        for (int i = 0; i < tab_flows.children.Count; i++)
        {
            if (tab_flows.children[i] == tab_flows.list_tabs)
            {
                continue;
            }
            if (tab_flows.children[i] is not PNL_FlowGraph ed)
            {
                continue;
            }
            if (page == tab_flows.selected_tab)
            {
                return ed;
            }
            page++;
        }
        return null;
    }

    void TabNames_Sync()
    {
        for (int i = 0; i < tab_flows.children.Count; i++)
        {
            if (tab_flows.children[i] is not PNL_FlowGraph ed)
            {
                continue;
            }
            if (ed.flow == null)
            {
                continue;
            }
            string n = ed.flow.GetName();
            if (ed.flow.is_dirty && ed.flow.File_IsValid() && !n.EndsWith("*"))
            {
                n += "*";
            }
            if (ed.name != n)
            {
                ed.name = n;
            }
        }
    }

    static string SaveFolder()
    {
        return Scene_Editor.active?.file_browser.CurrentDir ?? "";
    }

    static bool SameAsset(ImpAsset a, ImpAsset b)
    {
        if (a == null || b == null)
        {
            return false;
        }
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (string.IsNullOrEmpty(a.filepath) || string.IsNullOrEmpty(b.filepath))
        {
            return false;
        }
        try
        {
            return string.Equals(Path.GetFullPath(a.filepath), Path.GetFullPath(b.filepath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a.filepath, b.filepath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
