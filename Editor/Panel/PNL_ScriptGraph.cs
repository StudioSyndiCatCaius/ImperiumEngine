using System.Numerics;
using Editor;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Script;
using ImperiumEngine.Script.Node;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Panel;

public class PNL_ScriptGraph : EdPanel
{
    public C2_Inspector inspector_defaults = new();
    public C2_GraphEdit graph_editor = new();
    public C2_Tree func_tree = new();
    public C2_SearchBar func_search = new();
    public C2_Text source_label = new();
    public C2_Button compile_button = new();
    public C2_Text compile_label = new();

    ImpScene _scene;
    A_Script _script;
    string _parent_sig = "";
    bool _loading;

    C2_GraphNode _drop_from;
    int _drop_slot;
    bool _drop_from_out;
    Vector2 _place_graph;

    public PNL_ScriptGraph()
    {
        name = "Imp";
        layout = TLayout2.FULL;
        cursor_filter = ECursorFilter.Pass;

        C2_List left = new()
        {
            orentation = EUIOrentation.V,
            is_scrollable = false,
            spacing = 2,
            layout = new TLayout2
            {
                size = new(220, 0),
                size_min = new(160, 0),
                orient_V = EUIViewportAlignment.Fill,
            },
        };

        source_label.text = "Builtin";
        source_label.style = UI_Text.MUTED;
        source_label.layout = new TLayout2
        {
            size = new(0, 20),
            size_min = new(0, 20),
            orient_H = EUIViewportAlignment.Fill,
        };

        func_search.placeholder = "Search";
        func_search.layout = new TLayout2
        {
            size = new(0, 24),
            size_min = new(0, 24),
            orient_H = EUIViewportAlignment.Fill,
        };
        func_search.on_search = _ => RebuildFuncList();

        func_tree.layout = TLayout2.FULL;
        func_tree.row_height = 22f;
        func_tree.on_item_click = OnFuncClick;
        func_tree.on_item_double_click = OnFuncClick;

        compile_button.text = "Compile";
        compile_button.on_click = OnCompile;
        compile_button.layout = new TLayout2
        {
            size = new(0, 24),
            size_min = new(0, 24),
            orient_H = EUIViewportAlignment.Fill,
        };

        compile_label.text = "";
        compile_label.style = UI_Text.MUTED;
        compile_label.layout = new TLayout2
        {
            size = new(0, 18),
            size_min = new(0, 18),
            orient_H = EUIViewportAlignment.Fill,
        };

        left.Child_Add(source_label);
        left.Child_Add(compile_button);
        left.Child_Add(compile_label);
        left.Child_Add(func_search);
        left.Child_Add(func_tree);

        inspector_defaults.name = "Defaults";
        inspector_defaults.layout = new TLayout2
        {
            size = new(240, 0),
            size_min = new(160, 0),
            orient_V = EUIViewportAlignment.Fill,
        };

        graph_editor.name = "Graph";
        graph_editor.layout = TLayout2.FULL;
        graph_editor.on_context_empty = pos => OpenContext(pos, null, -1, false);
        graph_editor.on_connect_drop = (pos, node, slot, from_out) => OpenContext(pos, node, slot, from_out);
        graph_editor.on_connection = _ => WriteConnections();
        graph_editor.on_disconnection = _ => WriteConnections();
        graph_editor.on_node_removed = OnGraphNodeRemoved;

        C2_List row = new()
        {
            orentation = EUIOrentation.H,
            is_scrollable = false,
            spacing = 0,
            layout = TLayout2.FULL,
        };
        row.Child_Add(left);
        row.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        row.Child_Add(graph_editor);
        row.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        row.Child_Add(inspector_defaults);
        Child_Add(row);
    }

    public void Bind(ImpScene scene)
    {
        _scene = scene;
        A_Script next = null;
        if (scene != null)
        {
            next = scene.Script_Get();
        }
        if (next == _script)
        {
            return;
        }
        FlushPositions();
        _script = next;
        RebuildAll();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (_script == null)
        {
            return;
        }

        string parent = "";
        if (_script.parent_type.class_name != null)
        {
            parent = _script.parent_type.class_name;
        }
        if (parent != _parent_sig)
        {
            _parent_sig = parent;
            RebuildFuncList();
        }

        string ctx_name = _script.ParentType_Get().Name;
        if (_script == _scene?.script_override && _script != null && !string.IsNullOrEmpty(_script.filepath))
        {
            source_label.text = "Override: " + ImpAsset.Name_ForPath(_script.filepath) + " (" + ctx_name + ")";
        }
        else
        {
            source_label.text = "Builtin (" + ctx_name + ")";
        }

        FlushPositions();
    }

    void RebuildAll()
    {
        _loading = true;
        graph_editor.Nodes_Clear();
        if (_script == null)
        {
            inspector_defaults.Select();
            func_tree.Tree_SetItems(Array.Empty<TTreeItem>());
            _loading = false;
            return;
        }
        inspector_defaults.Select(_script);
        if (_script.nodes == null)
        {
            _script.nodes = new List<TPulseNode>();
        }
        if (_script.connections == null)
        {
            _script.connections = new List<TGraphConnection>();
        }
        for (int i = 0; i < _script.nodes.Count; i++)
        {
            TPulseNode pn = _script.nodes[i];
            if (pn == null)
            {
                continue;
            }
            C2_GraphNode gn = BuildWidget(pn);
            if (gn != null)
            {
                graph_editor.Child_Add(gn);
            }
        }
        for (int i = 0; i < _script.connections.Count; i++)
        {
            TGraphConnection c = _script.connections[i];
            C2_GraphNode from = WidgetOf(c.from_node);
            C2_GraphNode to = WidgetOf(c.to_node);
            if (from != null && to != null)
            {
                graph_editor.Connect(from, c.from_pin, to, c.to_pin);
            }
        }
        _loading = false;
        RebuildFuncList();
    }

    void RebuildFuncList()
    {
        if (_script == null)
        {
            func_tree.Tree_SetItems(Array.Empty<TTreeItem>());
            return;
        }
        Type parent = _script.ParentType_Get();
        string q = func_search.Query ?? "";
        q = q.Trim();

        List<TTreeItem> ov_kids = new();
        List<TPulseFunc> ovs = Pulse.Overrides(parent);
        for (int i = 0; i < ovs.Count; i++)
        {
            TPulseFunc f = ovs[i];
            if (q.Length > 0 && f.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            bool added = _script.Node_FindOverride(f.name) != null;
            Color col = added ? new Color(120, 200, 140, 255) : new Color(220, 220, 220, 255);
            ov_kids.Add(new TTreeItem
            {
                sections = new[]
                {
                    new TTreeItemSection { text = f.name, color = col },
                },
                data = f,
            });
        }

        List<TTreeItem> items = new();
        items.Add(new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection { text = "Overrides", color = new Color(180, 180, 190, 255) },
            },
            children = ov_kids.ToArray(),
        });
        func_tree.Tree_SetItems(items);
        func_tree.Tree_ExpandAll(true);
    }

    void OnFuncClick(TTreeItem item)
    {
        if (item.data is not TPulseFunc f || !f.is_override)
        {
            return;
        }
        AddOrFocusOverride(f);
    }

    void AddOrFocusOverride(TPulseFunc f)
    {
        if (_script == null || f == null)
        {
            return;
        }
        TPulseNode existing = _script.Node_FindOverride(f.name);
        if (existing != null)
        {
            C2_GraphNode w = WidgetOf(existing.id);
            if (w != null)
            {
                graph_editor.Node_Focus(w);
            }
            return;
        }
        Vector2 pos = graph_editor.scroll_offset + new Vector2(80f, 80f);
        TScriptNodeMenu entry = new()
        {
            text = f.name,
            node_class = nameof(SN_Event),
            kind = EScriptNodeType.VoidOverride,
            member = f.name,
        };
        Spawn(entry, _script.ParentType_Get(), pos, null, -1, false);
    }

    void OpenContext(Vector2 screen, C2_GraphNode from, int slot, bool from_out)
    {
        if (_script == null)
        {
            return;
        }
        _drop_from = from;
        _drop_slot = slot;
        _drop_from_out = from_out;
        _place_graph = graph_editor.ScreenToGraph(screen);

        Type ctx = _script.ParentType_Get();
        bool self_ctx = true;
        if (from != null && slot >= 0 && slot < from.slots.Count)
        {
            TGraphSlot sl = from.slots[slot];
            Type pin;
            if (from_out)
            {
                pin = sl.data_right;
            }
            else
            {
                pin = sl.data_left;
            }
            if (Pulse.IsObjectType(pin))
            {
                ctx = pin;
                self_ctx = false;
            }
        }

        List<TPopupMenuOption> opts = new();
        List<TScriptNodeMenu> entries = ScriptNodes.Menu(ctx, self_ctx, _script);
        string category = "";
        for (int i = 0; i < entries.Count; i++)
        {
            TScriptNodeMenu e = entries[i];
            if (e.category != category)
            {
                category = e.category;
                opts.Add(new TPopupMenuOption { text = category, is_separator = true });
            }
            TScriptNodeMenu captured = e;
            opts.Add(new TPopupMenuOption
            {
                text = e.text,
                is_disabled = e.is_disabled,
                on_press = () => Spawn(captured, ctx, _place_graph, _drop_from, _drop_slot, _drop_from_out),
            });
        }

        if (opts.Count == 0)
        {
            opts.Add(new TPopupMenuOption { text = "(none)", is_disabled = true });
        }
        ImpPlayer.Popup_Run(this, new A_PopupConfig { searchable = true, options = opts }, null, screen);
    }

    void Spawn(TScriptNodeMenu e, Type ctx, Vector2 pos, C2_GraphNode from, int from_slot, bool from_out)
    {
        if (_script == null || e == null)
        {
            return;
        }
        // One override node per member: focus the existing one instead of adding a second.
        if (e.kind == EScriptNodeType.VoidOverride)
        {
            TPulseNode already = _script.Node_FindOverride(e.member);
            if (already != null)
            {
                C2_GraphNode w = WidgetOf(already.id);
                if (w != null)
                {
                    graph_editor.Node_Focus(w);
                }
                return;
            }
        }
        TPulseNode pn = new()
        {
            id = Guid.NewGuid(),
            kind = e.kind,
            node_class = e.node_class,
            member = e.member,
            target_type = TypeName(ctx),
            is_set = e.is_set,
            position = pos,
        };
        _script.nodes.Add(pn);
        C2_GraphNode gn = BuildWidget(pn);
        graph_editor.Child_Add(gn);
        graph_editor.Node_Focus(gn);
        TryWireNew(gn, from, from_slot, from_out);
        MarkDirty();
        RebuildFuncList();
    }

    C2_GraphNode BuildWidget(TPulseNode pn)
    {
        C2_GraphNode gn = new()
        {
            user_data = pn,
            graph_position = pn.position,
        };
        Type ctx = TClass<Object>.Resolve(pn.target_type);
        if (ctx == null)
        {
            if (_script != null)
            {
                ctx = _script.ParentType_Get();
            }
            else
            {
                ctx = typeof(object);
            }
        }

        // The SN_* node owns its title / color / chrome / pin rows. This only maps them onto the widget.
        ScriptNode sn = ScriptNodes.Create(pn, ctx, _script);
        if (sn == null)
        {
            gn.title = pn.member;
        }
        else
        {
            gn.title = sn.GetNode_Title();
            gn.title_color = sn.GetNode_Color();
            gn.chrome = sn.GetNode_Chrome();
            List<TScriptSlot> rows = new();
            sn.Slots_Build(rows);
            for (int i = 0; i < rows.Count; i++)
            {
                TScriptSlot r = rows[i];
                ApplySlot(gn, i, r.enable_left, r.type_left, r.name_left, r.enable_right, r.type_right, r.name_right);
            }
        }
        for (int i = 0; i < gn.slots.Count; i++)
        {
            TGraphSlot s = gn.slots[i];
            if (!s.edit_left || s.data_left == null)
            {
                continue;
            }
            s.value = pn.Pin_Get(s.name_left, s.data_left);
        }
        gn.on_slot_value = OnSlotValue;
        gn.SlotEdits_Rebuild();
        gn.RefreshSize();
        return gn;
    }

    void ApplySlot(C2_GraphNode node, int index, bool enable_left, Type type_left, string name_left,
        bool enable_right, Type type_right, string name_right)
    {
        node.Slot_Set(index, enable_left, Pulse.TypeId(type_left), Pulse.TypeColor(type_left), name_left,
            enable_right, Pulse.TypeId(type_right), Pulse.TypeColor(type_right), name_right);
        if (index >= 0 && index < node.slots.Count)
        {
            TGraphSlot s = node.slots[index];
            s.data_left = type_left;
            s.data_right = type_right;
            if (enable_left && Pulse.CanEditDefault(type_left))
            {
                s.edit_left = true;
            }
        }
    }

    void OnSlotValue(C2_GraphNode node, int slot)
    {
        if (_loading || _script == null || node == null)
        {
            return;
        }
        if (node.user_data is not TPulseNode pn)
        {
            return;
        }
        if (slot < 0 || slot >= node.slots.Count)
        {
            return;
        }
        TGraphSlot s = node.slots[slot];
        pn.Pin_Set(s.name_left, s.data_left, s.value);
        MarkDirty();
    }

    void TryWireNew(C2_GraphNode created, C2_GraphNode from, int from_slot, bool from_out)
    {
        if (created == null || from == null || from_slot < 0)
        {
            return;
        }
        if (from_out)
        {
            int dest = BestInput(created, from, from_slot);
            if (dest >= 0)
            {
                graph_editor.Connect(from, from_slot, created, dest);
            }
        }
        else
        {
            int src = BestOutput(created, from, from_slot);
            if (src >= 0)
            {
                graph_editor.Connect(created, src, from, from_slot);
            }
        }
    }

    int BestInput(C2_GraphNode created, C2_GraphNode from, int from_slot)
    {
        int want = 0;
        Type want_t = null;
        if (from_slot >= 0 && from_slot < from.slots.Count)
        {
            want = from.slots[from_slot].type_right;
            want_t = from.slots[from_slot].data_right;
        }
        int named = -1;
        int typed = -1;
        for (int i = 0; i < created.slots.Count; i++)
        {
            TGraphSlot s = created.slots[i];
            if (!s.enable_left)
            {
                continue;
            }
            if (s.type_left != want)
            {
                continue;
            }
            if (typed < 0)
            {
                typed = i;
            }
            if (want_t != null && Pulse.IsObjectType(want_t) && s.name_left == "Target")
            {
                named = i;
            }
        }
        if (named >= 0)
        {
            return named;
        }
        return typed;
    }

    int BestOutput(C2_GraphNode created, C2_GraphNode to, int to_slot)
    {
        int want = 0;
        if (to_slot >= 0 && to_slot < to.slots.Count)
        {
            want = to.slots[to_slot].type_left;
        }
        for (int i = 0; i < created.slots.Count; i++)
        {
            TGraphSlot s = created.slots[i];
            if (s.enable_right && s.type_right == want)
            {
                return i;
            }
        }
        return -1;
    }

    void OnGraphNodeRemoved(C2_GraphNode node)
    {
        if (_loading || _script == null || node == null)
        {
            return;
        }
        if (node.user_data is TPulseNode pn)
        {
            _script.nodes.Remove(pn);
        }
        MarkDirty();
        RebuildFuncList();
    }

    void WriteConnections()
    {
        if (_loading || _script == null)
        {
            return;
        }
        _script.connections.Clear();
        for (int i = 0; i < graph_editor.connections.Count; i++)
        {
            TGraphLink l = graph_editor.connections[i];
            if (l.from == null || l.to == null)
            {
                continue;
            }
            if (l.from.user_data is not TPulseNode a || l.to.user_data is not TPulseNode b)
            {
                continue;
            }
            _script.connections.Add(new TGraphConnection
            {
                from_node = a.id,
                from_pin = (byte)l.from_slot,
                to_node = b.id,
                to_pin = (byte)l.to_slot,
            });
        }
        MarkDirty();
    }

    void FlushPositions()
    {
        if (_script == null)
        {
            return;
        }
        bool moved = false;
        for (int i = 0; i < graph_editor.children.Count; i++)
        {
            if (graph_editor.children[i] is not C2_GraphNode n)
            {
                continue;
            }
            if (n.user_data is not TPulseNode pn)
            {
                continue;
            }
            if (pn.position != n.graph_position)
            {
                pn.position = n.graph_position;
                moved = true;
            }
        }
        if (moved)
        {
            MarkDirty();
        }
    }

    C2_GraphNode WidgetOf(Guid id)
    {
        for (int i = 0; i < graph_editor.children.Count; i++)
        {
            if (graph_editor.children[i] is C2_GraphNode n && n.user_data is TPulseNode pn && pn.id == id)
            {
                return n;
            }
        }
        return null;
    }

    void MarkDirty()
    {
        if (_script != null)
        {
            _script.compile_dirty = true;
            if (!string.IsNullOrEmpty(_script.filepath))
            {
                _script.is_dirty = true;
            }
        }
        if (_scene != null)
        {
            _scene.is_dirty = true;
        }
    }

    void OnCompile()
    {
        if (_script == null)
        {
            return;
        }
        TScriptProgram p = _script.Compile();
        if (p.errors.Count == 0)
        {
            compile_label.text = "Compiled " + p.nodes.Count + " nodes";
            Console.WriteLine("[Pulse] compiled " + p.nodes.Count + " nodes, no errors.");
            return;
        }
        compile_label.text = p.errors.Count + " error(s) — see log";
        for (int i = 0; i < p.errors.Count; i++)
        {
            Console.WriteLine("[Pulse] " + p.errors[i]);
        }
    }

    static string TypeName(Type t)
    {
        if (t == null)
        {
            return "";
        }
        return t.Name;
    }
}
