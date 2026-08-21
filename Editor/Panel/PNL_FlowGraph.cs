using System.Numerics;
using Editor;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Nodes.Common;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Panel;

public class PNL_FlowGraph : EdPanel
{
    public C2_Inspector inspector = new();
    public C2_GraphEdit graph_editor = new();
    public C2_Tree node_tree = new();
    public C2_SearchBar node_search = new();
    public ImpUndo undo = new();

    A_Flow _flow;
    bool _loading;
    object _inspect_target;

    C2_GraphNode _drop_from;
    int _drop_slot;
    bool _drop_from_out;
    Vector2 _place_graph;

    public PNL_FlowGraph()
    {
        name = "Flow";
        layout = TLayout2.FULL;
        cursor_filter = ECursorFilter.Pass;

        inspector.name = "Inspect";
        inspector.layout = new TLayout2
        {
            size = new(240, 0),
            size_min = new(160, 0),
            orient_V = EUIViewportAlignment.Fill,
        };

        C2_List right = new()
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

        node_search.placeholder = "Search nodes";
        node_search.layout = new TLayout2
        {
            size = new(0, 24),
            size_min = new(0, 24),
            orient_H = EUIViewportAlignment.Fill,
        };
        node_search.on_search = _ => RebuildPalette();

        node_tree.layout = TLayout2.FULL;
        node_tree.row_height = 22f;
        node_tree.item_drag_payload = item => item.data as Type;
        node_tree.on_item_double_click = OnPaletteActivate;
        node_tree.on_item_click = null;

        right.Child_Add(node_search);
        right.Child_Add(node_tree);

        graph_editor.name = "Graph";
        graph_editor.layout = TLayout2.FULL;
        graph_editor.on_context_empty = pos => OpenContext(pos, null, -1, false);
        graph_editor.on_connect_drop = (pos, node, slot, from_out) => OpenContext(pos, node, slot, from_out);
        graph_editor.on_connection = _ => WriteConnections();
        graph_editor.on_disconnection = _ => WriteConnections();
        graph_editor.on_node_removed = OnGraphNodeRemoved;
        graph_editor.on_drop = OnGraphDrop;

        C2_List row = new()
        {
            orentation = EUIOrentation.H,
            is_scrollable = false,
            spacing = 0,
            layout = TLayout2.FULL,
        };
        row.Child_Add(inspector);
        row.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        row.Child_Add(graph_editor);
        row.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        row.Child_Add(right);
        Child_Add(row);
    }

    public A_Flow flow
    {
        get { return _flow; }
        set { Bind(value); }
    }

    public void Bind(A_Flow next)
    {
        if (next == _flow)
        {
            return;
        }
        FlushPositions();
        _flow = next;
        RebuildAll();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!IsVisibleInTree())
        {
            return;
        }
        undo.asset = _flow;
        ImpUndo.active = undo;
        if (_flow == null)
        {
            return;
        }
        FlushPositions();
        SyncInspect();
    }

    void RebuildAll()
    {
        _loading = true;
        graph_editor.Nodes_Clear();
        _inspect_target = null;
        if (_flow == null)
        {
            inspector.Select();
            node_tree.Tree_SetItems(Array.Empty<TTreeItem>());
            _loading = false;
            return;
        }
        if (_flow.Flow.nodes == null)
        {
            _flow.Flow.nodes = new List<ImpFlowNode>();
        }
        if (_flow.Flow.connections == null)
        {
            _flow.Flow.connections = new List<TFlowConnection>();
        }
        for (int i = 0; i < _flow.Flow.nodes.Count; i++)
        {
            ImpFlowNode n = _flow.Flow.nodes[i];
            if (n == null)
            {
                continue;
            }
            n._owner = _flow;
        }
        EnsureStart();
        for (int i = 0; i < _flow.Flow.nodes.Count; i++)
        {
            ImpFlowNode n = _flow.Flow.nodes[i];
            if (n == null)
            {
                continue;
            }
            C2_GraphNode gn = BuildWidget(n);
            if (gn != null)
            {
                graph_editor.Child_Add(gn);
            }
        }
        for (int i = 0; i < _flow.Flow.connections.Count; i++)
        {
            TFlowConnection c = _flow.Flow.connections[i];
            C2_GraphNode from = WidgetOf(c.from_node);
            C2_GraphNode to = WidgetOf(c.to_node);
            if (from != null && to != null)
            {
                graph_editor.Connect(from, c.from_pin, to, c.to_pin);
            }
        }
        _loading = false;
        inspector.Select(_flow);
        _inspect_target = _flow;
        RebuildPalette();
    }

    void EnsureStart()
    {
        if (_flow == null)
        {
            return;
        }
        List<ImpFlowNode> starts = _flow.GetNodes_OfType(typeof(Node_C_Start));
        if (starts.Count > 0)
        {
            return;
        }
        Node_C_Start start = new Node_C_Start();
        start.position = new Vector2(80f, 80f);
        start._owner = _flow;
        _flow.Flow.nodes.Add(start);
        MarkDirty();
    }

    void RebuildPalette()
    {
        if (_flow == null)
        {
            node_tree.Tree_SetItems(Array.Empty<TTreeItem>());
            return;
        }
        string q = node_search.Query ?? "";
        q = q.Trim();
        List<ImpFlowNode> protos = ImpFlowNode.Nodes_Addable(_flow);
        Dictionary<string, List<TTreeItem>> groups = new(StringComparer.OrdinalIgnoreCase);
        List<string> order = new();
        for (int i = 0; i < protos.Count; i++)
        {
            ImpFlowNode proto = protos[i];
            string title = proto.GetNode_Title();
            if (q.Length > 0 && title.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
            {
                string cat = proto.GetNode_Category();
                if (cat.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }
            string category = proto.GetNode_Category();
            if (!groups.TryGetValue(category, out List<TTreeItem> kids))
            {
                kids = new List<TTreeItem>();
                groups[category] = kids;
                order.Add(category);
            }
            kids.Add(new TTreeItem
            {
                sections = new[]
                {
                    new TTreeItemSection { text = title, color = proto.GetNode_Color() },
                },
                data = proto.GetType(),
            });
        }
        order.Sort((a, b) =>
        {
            int ia = CategoryRank(a);
            int ib = CategoryRank(b);
            if (ia != ib)
            {
                return ia.CompareTo(ib);
            }
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });
        List<TTreeItem> items = new();
        for (int i = 0; i < order.Count; i++)
        {
            string cat = order[i];
            items.Add(new TTreeItem
            {
                sections = new[]
                {
                    new TTreeItemSection { text = cat, color = new Color(180, 180, 190, 255) },
                },
                children = groups[cat].ToArray(),
            });
        }
        node_tree.Tree_SetItems(items);
        node_tree.Tree_ExpandAll(true);
    }

    static int CategoryRank(string cat)
    {
        if (string.Equals(cat, "Common", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        if (string.Equals(cat, "Dialogue", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        if (string.Equals(cat, "Quest", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }
        return 10;
    }

    void OnPaletteActivate(TTreeItem item)
    {
        if (item.data is not Type t)
        {
            return;
        }
        Vector2 pos = PlaceInView();
        Spawn(t, pos, null, -1, false);
    }

    void OnGraphDrop(Vector2 graph_pos, object payload)
    {
        if (payload is not Type t)
        {
            return;
        }
        if (!typeof(ImpFlowNode).IsAssignableFrom(t) || t.IsAbstract)
        {
            return;
        }
        Spawn(t, Snap(graph_pos), null, -1, false);
    }

    void OpenContext(Vector2 screen, C2_GraphNode from, int slot, bool from_out)
    {
        if (_flow == null)
        {
            return;
        }
        _drop_from = from;
        _drop_slot = slot;
        _drop_from_out = from_out;
        _place_graph = graph_editor.ScreenToGraph(screen);

        List<TPopupMenuOption> opts = new();
        List<ImpFlowNode> protos = ImpFlowNode.Nodes_Addable(_flow);
        string category = "";
        for (int i = 0; i < protos.Count; i++)
        {
            ImpFlowNode proto = protos[i];
            string cat = proto.GetNode_Category();
            if (cat != category)
            {
                category = cat;
                opts.Add(new TPopupMenuOption { text = category, is_separator = true });
            }
            Type captured = proto.GetType();
            opts.Add(new TPopupMenuOption
            {
                text = proto.GetNode_Title(),
                on_press = () => Spawn(captured, _place_graph, _drop_from, _drop_slot, _drop_from_out),
            });
        }
        if (opts.Count == 0)
        {
            opts.Add(new TPopupMenuOption { text = "(none)", is_disabled = true });
        }
        ImpPlayer.Popup_Run(this, new A_PopupConfig { searchable = true, options = opts }, null, screen);
    }

    void Spawn(Type type, Vector2 pos, C2_GraphNode from, int from_slot, bool from_out)
    {
        if (_flow == null || type == null)
        {
            return;
        }
        if (!typeof(ImpFlowNode).IsAssignableFrom(type) || type.IsAbstract)
        {
            return;
        }
        if (type == typeof(Node_C_Start))
        {
            List<ImpFlowNode> existing = _flow.GetNodes_OfType(typeof(Node_C_Start));
            if (existing.Count > 0)
            {
                C2_GraphNode w = WidgetOf(existing[0].guid);
                if (w != null)
                {
                    graph_editor.Node_Focus(w);
                }
                return;
            }
        }
        ImpFlowNode proto = Activator.CreateInstance(type) as ImpFlowNode;
        if (proto == null)
        {
            return;
        }
        if (!proto.Node_CanUseInFlow(_flow))
        {
            return;
        }
        proto.position = Snap(pos);
        proto._owner = _flow;
        _flow.Flow.nodes.Add(proto);
        C2_GraphNode gn = BuildWidget(proto);
        graph_editor.Child_Add(gn);
        graph_editor.Node_Focus(gn);
        TryWireNew(gn, from, from_slot, from_out);
        MarkDirty();
    }

    C2_GraphNode BuildWidget(ImpFlowNode node)
    {
        C2_GraphNode gn = new()
        {
            user_data = node,
            graph_position = node.position,
            title = node.GetNode_Title(),
            title_color = node.GetNode_Color(),
        };
        int in_n = 0;
        int out_n = 0;
        if (node.inputs != null)
        {
            in_n = node.inputs.Count;
        }
        if (node.outputs != null)
        {
            out_n = node.outputs.Count;
        }
        int rows = in_n;
        if (out_n > rows)
        {
            rows = out_n;
        }
        if (rows < 1)
        {
            rows = 1;
        }
        Color exec = Pulse.TypeColor(null);
        int exec_id = Pulse.ExecId;
        for (int i = 0; i < rows; i++)
        {
            bool left = i < in_n;
            bool right = i < out_n;
            string name_left = "";
            string name_right = "";
            if (left && node.inputs != null)
            {
                name_left = node.inputs[i].name ?? "";
            }
            if (right && node.outputs != null)
            {
                name_right = node.outputs[i].name ?? "";
            }
            gn.Slot_Set(i, left, exec_id, exec, name_left, right, exec_id, exec, name_right);
            if (left && i < gn.slots.Count)
            {
                gn.slots[i].allow_multi_in = true;
            }
        }
        gn.RefreshSize();
        return gn;
    }

    void TryWireNew(C2_GraphNode created, C2_GraphNode from, int from_slot, bool from_out)
    {
        if (created == null || from == null || from_slot < 0)
        {
            return;
        }
        if (from_out)
        {
            int dest = FirstPin(created, true);
            if (dest >= 0)
            {
                graph_editor.Connect(from, from_slot, created, dest);
            }
        }
        else
        {
            int src = FirstPin(created, false);
            if (src >= 0)
            {
                graph_editor.Connect(created, src, from, from_slot);
            }
        }
    }

    static int FirstPin(C2_GraphNode node, bool input)
    {
        if (node == null)
        {
            return -1;
        }
        for (int i = 0; i < node.slots.Count; i++)
        {
            TGraphSlot s = node.slots[i];
            if (input && s.enable_left)
            {
                return i;
            }
            if (!input && s.enable_right)
            {
                return i;
            }
        }
        return -1;
    }

    void OnGraphNodeRemoved(C2_GraphNode node)
    {
        if (_loading || _flow == null || node == null)
        {
            return;
        }
        if (node.user_data is ImpFlowNode fn)
        {
            _flow.Flow.nodes.Remove(fn);
        }
        EnsureStart();
        bool has_start = false;
        for (int i = 0; i < graph_editor.children.Count; i++)
        {
            if (graph_editor.children[i] is not C2_GraphNode gn)
            {
                continue;
            }
            if (gn == node)
            {
                continue;
            }
            if (gn.user_data is Node_C_Start)
            {
                has_start = true;
                break;
            }
        }
        if (!has_start)
        {
            List<ImpFlowNode> starts = _flow.GetNodes_OfType(typeof(Node_C_Start));
            for (int i = 0; i < starts.Count; i++)
            {
                if (WidgetOf(starts[i].guid) != null)
                {
                    continue;
                }
                C2_GraphNode gn = BuildWidget(starts[i]);
                graph_editor.Child_Add(gn);
            }
        }
        MarkDirty();
    }

    void WriteConnections()
    {
        if (_loading || _flow == null)
        {
            return;
        }
        _flow.Flow.connections.Clear();
        for (int i = 0; i < graph_editor.connections.Count; i++)
        {
            TGraphLink l = graph_editor.connections[i];
            if (l.from == null || l.to == null)
            {
                continue;
            }
            if (l.from.user_data is not ImpFlowNode a || l.to.user_data is not ImpFlowNode b)
            {
                continue;
            }
            _flow.Flow.connections.Add(new TFlowConnection
            {
                from_node = a.guid,
                from_pin = (byte)l.from_slot,
                to_node = b.guid,
                to_pin = (byte)l.to_slot,
            });
        }
        MarkDirty();
    }

    void FlushPositions()
    {
        if (_flow == null)
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
            if (n.user_data is not ImpFlowNode fn)
            {
                continue;
            }
            if (fn.position != n.graph_position)
            {
                fn.position = n.graph_position;
                moved = true;
            }
        }
        if (moved)
        {
            MarkDirty();
        }
    }

    void SyncInspect()
    {
        ImpFlowNode selected = null;
        int count = 0;
        for (int i = 0; i < graph_editor.children.Count; i++)
        {
            if (graph_editor.children[i] is not C2_GraphNode n || !n.selected)
            {
                continue;
            }
            if (n.user_data is ImpFlowNode fn)
            {
                selected = fn;
                count++;
            }
        }
        object want = _flow;
        if (count == 1 && selected != null)
        {
            want = selected;
        }
        if (want == _inspect_target)
        {
            return;
        }
        _inspect_target = want;
        if (want != null)
        {
            inspector.Select(want);
        }
        else
        {
            inspector.Select();
        }
    }

    C2_GraphNode WidgetOf(Guid id)
    {
        for (int i = 0; i < graph_editor.children.Count; i++)
        {
            if (graph_editor.children[i] is C2_GraphNode n && n.user_data is ImpFlowNode fn && fn.guid == id)
            {
                return n;
            }
        }
        return null;
    }

    Vector2 PlaceInView()
    {
        TDimensions2 dim = graph_editor.Dimensions_Get();
        Vector2 center = dim.position + dim.size * 0.5f;
        return Snap(graph_editor.ScreenToGraph(center));
    }

    Vector2 Snap(Vector2 pos)
    {
        if (!graph_editor.snap_enabled || graph_editor.snap <= 0.001f)
        {
            return pos;
        }
        float s = graph_editor.snap;
        pos.X = MathF.Round(pos.X / s) * s;
        pos.Y = MathF.Round(pos.Y / s) * s;
        return pos;
    }

    void MarkDirty()
    {
        if (_flow != null)
        {
            _flow.is_dirty = true;
        }
    }
}
