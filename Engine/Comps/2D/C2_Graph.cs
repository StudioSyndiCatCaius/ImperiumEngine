using System.Globalization;
using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public enum EGraphNodeChrome
{
    Regular,
    Var,
}

public class TGraphSlot
{
    public bool enable_left;
    public bool enable_right;
    public int type_left;
    public int type_right;
    public Type data_left;
    public Type data_right;
    public Color color_left = Color.White;
    public Color color_right = Color.White;
    public string name_left = "";
    public string name_right = "";
    // Unconnected data input: host sets edit_left + value, widget lives in edit.
    public bool edit_left;
    public object value;
    public Imp2D edit;
}

public class TGraphLink
{
    public C2_GraphNode from;
    public int from_slot;
    public C2_GraphNode to;
    public int to_slot;
}

[ImpClass(Hidden = true)]
public class C2_GraphEdit : Imp2D
{
    public Vector2 scroll_offset;
    public float zoom = 1f;
    public float zoom_min = 0.25f;
    public float zoom_max = 2.5f;
    public float zoom_step = 1.2f;
    public bool snap_enabled = true;
    public float snap = 20f;
    public bool show_grid = true;
    public bool right_disconnects = true;
    public float lines_curvature = 0.5f;
    public float lines_thickness = 3.5f;
    public UI_Graph style = UI_Graph.DEFAULT;

    public List<TGraphLink> connections = new();

    public Action<TGraphLink> on_connection;
    public Action<TGraphLink> on_disconnection;
    public Action<Vector2> on_context_empty;
    public Action<Vector2, C2_GraphNode, int, bool> on_connect_drop;
    public Action<C2_GraphNode> on_node_removed;

    public TGraphLink selected_link;
    public TGraphLink hovered_link;

    enum EDrag { None, Pan, Node, Box, Connect }

    EDrag _drag;
    Vector2 _drag_cursor;
    Vector2 _box_from;
    Vector2 _box_to;
    bool _box_add;
    List<C2_GraphNode> _box_prev = new();

    bool _connect_from_out;
    C2_GraphNode _connect_node;
    int _connect_slot;
    int _connect_type;
    Color _connect_color;
    bool _connect_valid;
    bool _connect_target_ok;
    C2_GraphNode _connect_target;
    int _connect_target_slot;
    bool _just_disconnected;

    Dictionary<C2_GraphNode, Vector2> _drag_from = new();

    const float TitleH = 26f;
    const float SlotH = 22f;
    const float PortR = 6f;
    const float HotR = 12f;

    public C2_GraphEdit()
    {
        cursor_filter = ECursorFilter.Hit;
        clip_children = true;
        layout = TLayout2.FULL;
    }
    
    

    public Vector2 GraphToScreen(Vector2 graph)
    {
        TDimensions2 dim = Dimensions_Get();
        float z = zoom;
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        return dim.position + (graph - scroll_offset) * z;
    }

    public Vector2 ScreenToGraph(Vector2 screen)
    {
        TDimensions2 dim = Dimensions_Get();
        float z = zoom;
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        return scroll_offset + (screen - dim.position) / z;
    }

    public Vector2 PortGraph(C2_GraphNode node, int slot, bool left)
    {
        if (node == null)
        {
            return Vector2.Zero;
        }
        Vector2 sz = node.GraphSize();
        float y = TitleH + 6f + slot * SlotH + SlotH * 0.5f;
        if (left)
        {
            return node.graph_position + new Vector2(0f, y);
        }
        return node.graph_position + new Vector2(sz.X, y);
    }

    public bool Connect(C2_GraphNode from, int from_slot, C2_GraphNode to, int to_slot)
    {
        if (!CanConnect(from, from_slot, to, to_slot))
        {
            return false;
        }
        for (int i = connections.Count - 1; i >= 0; i--)
        {
            TGraphLink old = connections[i];
            if (old.to == to && old.to_slot == to_slot)
            {
                connections.RemoveAt(i);
                if (selected_link == old)
                {
                    selected_link = null;
                }
                on_disconnection?.Invoke(old);
            }
        }
        TGraphLink link = new()
        {
            from = from,
            from_slot = from_slot,
            to = to,
            to_slot = to_slot,
        };
        connections.Add(link);
        on_connection?.Invoke(link);
        return true;
    }

    public bool IsInputConnected(C2_GraphNode node, int slot)
    {
        if (node == null)
        {
            return false;
        }
        for (int i = 0; i < connections.Count; i++)
        {
            TGraphLink l = connections[i];
            if (l.to == node && l.to_slot == slot)
            {
                return true;
            }
        }
        return false;
    }

    public void Disconnect(TGraphLink link)
    {
        if (link == null)
        {
            return;
        }
        if (!connections.Remove(link))
        {
            return;
        }
        if (selected_link == link)
        {
            selected_link = null;
        }
        if (hovered_link == link)
        {
            hovered_link = null;
        }
        on_disconnection?.Invoke(link);
    }

    public void Disconnect(C2_GraphNode from, int from_slot, C2_GraphNode to, int to_slot)
    {
        for (int i = connections.Count - 1; i >= 0; i--)
        {
            TGraphLink l = connections[i];
            if (l.from == from && l.from_slot == from_slot && l.to == to && l.to_slot == to_slot)
            {
                Disconnect(l);
            }
        }
    }

    public bool CanConnect(C2_GraphNode from, int from_slot, C2_GraphNode to, int to_slot)
    {
        if (from == null || to == null || from == to)
        {
            return false;
        }
        if (from_slot < 0 || to_slot < 0)
        {
            return false;
        }
        if (from_slot >= from.slots.Count || to_slot >= to.slots.Count)
        {
            return false;
        }
        TGraphSlot fs = from.slots[from_slot];
        TGraphSlot ts = to.slots[to_slot];
        if (!fs.enable_right || !ts.enable_left)
        {
            return false;
        }
        if (fs.type_right != ts.type_left)
        {
            Type from_t = fs.data_right;
            Type to_t = ts.data_left;
            if (from_t == null || to_t == null || !to_t.IsAssignableFrom(from_t))
            {
                return false;
            }
        }
        for (int i = 0; i < connections.Count; i++)
        {
            TGraphLink l = connections[i];
            if (l.from == from && l.from_slot == from_slot && l.to == to && l.to_slot == to_slot)
            {
                return false;
            }
        }
        return true;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);

        float z = zoom;
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not C2_GraphNode node)
            {
                continue;
            }
            node.RefreshSize();
            Vector2 sz = node.GraphSize();
            node.layout = new TLayout2
            {
                size = sz * z,
                size_min = Vector2.Zero,
                orient_H = EUIViewportAlignment.Start,
                orient_V = EUIViewportAlignment.Start,
            };
            node.transform.position = (node.graph_position - scroll_offset) * z;
            node.SlotEdits_Layout(z);
        }

        if (!IsVisibleInTree())
        {
            if (ImpPlayer.players.Count > 0 && ImpPlayer.players[0].input_hog == this)
            {
                ImpPlayer.players[0].input_hog = null;
            }
            _drag = EDrag.None;
            return;
        }

        if (ImpPlayer.players.Count == 0)
        {
            return;
        }
        ImpPlayer player = ImpPlayer.players[0];
        TDimensions2 dim = Dimensions_Get();
        bool over = player.Cursor_IsInDimensions(dim);
        bool hogged = player.input_hog != null && player.input_hog != this;
        if (hogged && _drag == EDrag.None)
        {
            return;
        }

        Vector2 cursor = player.cursor.position;
        bool lmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left);
        bool rmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Right);
        bool mmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Middle);
        bool lmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left);
        bool rmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Right);
        bool mmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Middle);
        bool space = ImpPlayer.Key_IsHeld(EInputKey.Key_Space);
        bool shift = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftShift) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightShift);
        bool ctrl = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);

        if (_drag == EDrag.None && over && !hogged)
        {
            hovered_link = ClosestLink(cursor, 8f);
        }

        if (_drag == EDrag.Pan)
        {
            if (!mmb && !rmb && !(space && lmb))
            {
                EndDrag(player);
            }
            else
            {
                Vector2 d = cursor - _drag_cursor;
                scroll_offset -= d / z;
                _drag_cursor = cursor;
            }
            return;
        }

        if (_drag == EDrag.Connect)
        {
            _connect_target_ok = false;
            _connect_target = null;
            _connect_valid = _just_disconnected || Vector2.Distance(_drag_cursor, cursor) > 8f;
            if (_connect_valid)
            {
                if (PickPort(cursor, !_connect_from_out, out C2_GraphNode hit, out int slot))
                {
                    C2_GraphNode from_n = _connect_node;
                    int from_s = _connect_slot;
                    C2_GraphNode to_n = hit;
                    int to_s = slot;
                    if (!_connect_from_out)
                    {
                        from_n = hit;
                        from_s = slot;
                        to_n = _connect_node;
                        to_s = _connect_slot;
                    }
                    if (CanConnect(from_n, from_s, to_n, to_s))
                    {
                        _connect_target_ok = true;
                        _connect_target = hit;
                        _connect_target_slot = slot;
                    }
                }
            }
            if (!lmb)
            {
                if (_connect_valid && _connect_target_ok && _connect_target != null)
                {
                    if (_connect_from_out)
                    {
                        Connect(_connect_node, _connect_slot, _connect_target, _connect_target_slot);
                    }
                    else
                    {
                        Connect(_connect_target, _connect_target_slot, _connect_node, _connect_slot);
                    }
                }
                else if (_connect_valid && on_connect_drop != null && _connect_node != null)
                {
                    on_connect_drop.Invoke(cursor, _connect_node, _connect_slot, _connect_from_out);
                }
                EndDrag(player);
            }
            return;
        }

        if (_drag == EDrag.Node)
        {
            Vector2 now = ScreenToGraph(cursor);
            Vector2 start = ScreenToGraph(_drag_cursor);
            Vector2 delta = now - start;
            bool do_snap = snap_enabled;
            if (ctrl)
            {
                do_snap = !do_snap;
            }
            foreach (var kv in _drag_from)
            {
                C2_GraphNode n = kv.Key;
                if (n == null || !n.draggable)
                {
                    continue;
                }
                Vector2 p = kv.Value + delta;
                if (do_snap && snap > 0.001f)
                {
                    p.X = MathF.Round(p.X / snap) * snap;
                    p.Y = MathF.Round(p.Y / snap) * snap;
                }
                n.graph_position = p;
            }
            if (!lmb)
            {
                EndDrag(player);
            }
            return;
        }

        if (_drag == EDrag.Box)
        {
            _box_to = cursor;
            ApplyBoxSelect();
            if (!lmb)
            {
                EndDrag(player);
            }
            return;
        }

        if (over && (player.target_focus == this || IsOurs(player.target_focus)))
        {
            bool pin_busy = PinEdit_IsBusy();
            if (!pin_busy && (ImpPlayer.Key_IsPressed(EInputKey.Key_Delete) || ImpPlayer.Key_IsPressed(EInputKey.Key_Backspace)))
            {
                DeleteSelection();
            }
            if (!pin_busy && ctrl && ImpPlayer.Key_IsPressed(EInputKey.Key_A))
            {
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is C2_GraphNode n && n.selectable)
                    {
                        n.selected = true;
                    }
                }
                selected_link = null;
            }
        }

        if (!over)
        {
            return;
        }

        if (over && (lmb_p || rmb_p || mmb_p))
        {
            player.target_focus = this;
        }

        if (over && !hogged)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                Vector2 before = ScreenToGraph(cursor);
                zoom = Math.Clamp(zoom * MathF.Pow(zoom_step, wheel), zoom_min, zoom_max);
                Vector2 after = ScreenToGraph(cursor);
                scroll_offset += before - after;
            }
        }

        if (mmb_p || (space && lmb_p))
        {
            BeginDrag(player, EDrag.Pan, cursor);
            return;
        }

        if (rmb_p && hovered_link != null)
        {
            Disconnect(hovered_link);
            hovered_link = null;
            return;
        }

        if (rmb_p && hovered_link == null && !PickPort(cursor, true, out _, out _) && !PickPort(cursor, false, out _, out _) && HitNode(cursor) == null)
        {
            if (on_context_empty != null)
            {
                on_context_empty.Invoke(cursor);
                return;
            }
            BeginDrag(player, EDrag.Pan, cursor);
            return;
        }

        if (!lmb_p)
        {
            return;
        }

        if (PickPort(cursor, true, out C2_GraphNode out_n, out int out_s))
        {
            BeginConnect(player, cursor, out_n, out_s, true);
            return;
        }
        if (PickPort(cursor, false, out C2_GraphNode in_n, out int in_s))
        {
            if (right_disconnects)
            {
                TGraphLink existing = null;
                for (int i = 0; i < connections.Count; i++)
                {
                    if (connections[i].to == in_n && connections[i].to_slot == in_s)
                    {
                        existing = connections[i];
                        break;
                    }
                }
                if (existing != null)
                {
                    C2_GraphNode src = existing.from;
                    int src_s = existing.from_slot;
                    Disconnect(existing);
                    BeginConnect(player, cursor, src, src_s, true);
                    _just_disconnected = true;
                    return;
                }
            }
            BeginConnect(player, cursor, in_n, in_s, false);
            return;
        }

        C2_GraphNode hit_node = HitNode(cursor);
        if (hit_node != null && hit_node.selectable)
        {
            selected_link = null;
            if (!hit_node.selected && !shift && !ctrl)
            {
                ClearNodeSelection();
            }
            hit_node.selected = true;
            Raise(hit_node);
            if (hit_node.draggable && !hit_node.SlotEdit_Hit(cursor))
            {
                _drag_from.Clear();
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is C2_GraphNode n && n.selected && n.draggable)
                    {
                        _drag_from[n] = n.graph_position;
                    }
                }
                BeginDrag(player, EDrag.Node, cursor);
            }
            return;
        }

        if (hovered_link != null)
        {
            if (!shift)
            {
                ClearNodeSelection();
            }
            selected_link = hovered_link;
            return;
        }

        if (!shift)
        {
            ClearNodeSelection();
            selected_link = null;
        }
        _box_from = cursor;
        _box_to = cursor;
        _box_add = shift;
        _box_prev.Clear();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is C2_GraphNode n && n.selected)
            {
                _box_prev.Add(n);
            }
        }
        BeginDrag(player, EDrag.Box, cursor);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 1f || dim.size.Y <= 1f)
        {
            return;
        }

        Imp2D.Clip_Push(dim);
        Raylib.DrawRectangleV(dim.position, dim.size, new Color(28, 30, 34, 255));
        if (show_grid)
        {
            DrawGrid(dim);
        }

        for (int i = 0; i < connections.Count; i++)
        {
            TGraphLink l = connections[i];
            if (l.from == null || l.to == null)
            {
                continue;
            }
            Vector2 a = GraphToScreen(PortGraph(l.from, l.from_slot, false));
            Vector2 b = GraphToScreen(PortGraph(l.to, l.to_slot, true));
            Color ca = PortColor(l.from, l.from_slot, false);
            Color cb = PortColor(l.to, l.to_slot, true);
            bool hot = l == hovered_link || l == selected_link;
            float thick = lines_thickness;
            if (hot)
            {
                thick += 1.5f;
                ca = Tint(ca, 1.25f);
                cb = Tint(cb, 1.25f);
            }
            DrawLink(a, b, ca, cb, thick);
        }
        Imp2D.Clip_Pop();
    }

    public override void OnDraw2DForeground(double dt, EDrawFlags flags)
    {
        base.OnDraw2DForeground(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 1f || dim.size.Y <= 1f)
        {
            return;
        }
        Imp2D.Clip_Push(dim);

        if (_drag == EDrag.Connect && _connect_node != null)
        {
            Vector2 a = GraphToScreen(PortGraph(_connect_node, _connect_slot, !_connect_from_out));
            Vector2 b;
            if (_connect_target_ok && _connect_target != null)
            {
                b = GraphToScreen(PortGraph(_connect_target, _connect_target_slot, _connect_from_out));
            }
            else if (ImpPlayer.players.Count > 0)
            {
                b = ImpPlayer.players[0].cursor.position;
            }
            else
            {
                b = a;
            }
            if (!_connect_from_out)
            {
                Vector2 tmp = a;
                a = b;
                b = tmp;
            }
            Color col = _connect_color;
            if (_connect_target_ok)
            {
                col = Tint(col, 1.3f);
            }
            DrawLink(a, b, col, col, lines_thickness);
        }

        if (_drag == EDrag.Box)
        {
            Vector2 min = Vector2.Min(_box_from, _box_to);
            Vector2 size = Vector2.Max(_box_from, _box_to) - min;
            Raylib.DrawRectangleV(min, size, new Color(70, 160, 255, 40));
            Raylib.DrawRectangleLinesEx(new Rectangle(min.X, min.Y, size.X, size.Y), 1f, new Color(120, 200, 255, 220));
        }

        Imp2D.Clip_Pop();
    }

    void DrawGrid(TDimensions2 dim)
    {
        float z = zoom;
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        float step = snap;
        if (step < 2f)
        {
            step = 20f;
        }
        float major = step * 5f;
        Vector2 g0 = ScreenToGraph(dim.position);
        Vector2 g1 = ScreenToGraph(dim.position + dim.size);
        float x0 = MathF.Floor(g0.X / step) * step;
        float y0 = MathF.Floor(g0.Y / step) * step;
        Color minor = new(40, 42, 48, 255);
        Color maj = new(52, 56, 64, 255);
        for (float x = x0; x <= g1.X; x += step)
        {
            Vector2 a = GraphToScreen(new Vector2(x, g0.Y));
            Vector2 b = GraphToScreen(new Vector2(x, g1.Y));
            bool is_maj = MathF.Abs(x / major - MathF.Round(x / major)) < 0.001f;
            Raylib.DrawLineEx(a, b, 1f, is_maj ? maj : minor);
        }
        for (float y = y0; y <= g1.Y; y += step)
        {
            Vector2 a = GraphToScreen(new Vector2(g0.X, y));
            Vector2 b = GraphToScreen(new Vector2(g1.X, y));
            bool is_maj = MathF.Abs(y / major - MathF.Round(y / major)) < 0.001f;
            Raylib.DrawLineEx(a, b, 1f, is_maj ? maj : minor);
        }
    }

    void DrawLink(Vector2 from, Vector2 to, Color ca, Color cb, float thick)
    {
        float x_diff = to.X - from.X;
        float cp = x_diff * lines_curvature;
        if (x_diff < 0f)
        {
            cp = -cp;
        }
        Vector2 p0 = from;
        Vector2 p1 = from + new Vector2(cp, 0f);
        Vector2 p2 = to - new Vector2(cp, 0f);
        Vector2 p3 = to;
        const int steps = 24;
        Vector2 prev = p0;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = 1f - t;
            Vector2 p =
                u * u * u * p0
                + 3f * u * u * t * p1
                + 3f * u * t * t * p2
                + t * t * t * p3;
            Color c = new(
                (byte)(ca.R + (cb.R - ca.R) * t),
                (byte)(ca.G + (cb.G - ca.G) * t),
                (byte)(ca.B + (cb.B - ca.B) * t),
                (byte)(ca.A + (cb.A - ca.A) * t));
            Raylib.DrawLineEx(prev, p, thick, c);
            prev = p;
        }
    }

    TGraphLink ClosestLink(Vector2 screen, float max_dist)
    {
        TGraphLink best = null;
        float best_d = max_dist;
        for (int i = 0; i < connections.Count; i++)
        {
            TGraphLink l = connections[i];
            if (l.from == null || l.to == null)
            {
                continue;
            }
            Vector2 a = GraphToScreen(PortGraph(l.from, l.from_slot, false));
            Vector2 b = GraphToScreen(PortGraph(l.to, l.to_slot, true));
            float x_diff = b.X - a.X;
            float cp = x_diff * lines_curvature;
            if (x_diff < 0f)
            {
                cp = -cp;
            }
            Vector2 p0 = a;
            Vector2 p1 = a + new Vector2(cp, 0f);
            Vector2 p2 = b - new Vector2(cp, 0f);
            Vector2 p3 = b;
            Vector2 prev = p0;
            const int steps = 24;
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                float u = 1f - t;
                Vector2 p =
                    u * u * u * p0
                    + 3f * u * u * t * p1
                    + 3f * u * t * t * p2
                    + t * t * t * p3;
                float d = DistPointSeg(screen, prev, p);
                if (d < best_d)
                {
                    best_d = d;
                    best = l;
                }
                prev = p;
            }
        }
        return best;
    }

    static float DistPointSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        if (len2 < 1e-8f)
        {
            return Vector2.Distance(p, a);
        }
        float t = Vector2.Dot(p - a, ab) / len2;
        if (t < 0f)
        {
            t = 0f;
        }
        else if (t > 1f)
        {
            t = 1f;
        }
        return Vector2.Distance(p, a + ab * t);
    }

    bool PickPort(Vector2 screen, bool outputs, out C2_GraphNode node, out int slot)
    {
        node = null;
        slot = -1;
        float best = HotR * HotR;
        if (zoom > 1f)
        {
            best *= zoom * zoom;
        }
        for (int i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is not C2_GraphNode n || !n.is_visible)
            {
                continue;
            }
            for (int s = 0; s < n.slots.Count; s++)
            {
                TGraphSlot sl = n.slots[s];
                bool en;
                if (outputs)
                {
                    en = sl.enable_right;
                }
                else
                {
                    en = sl.enable_left;
                }
                if (!en)
                {
                    continue;
                }
                Vector2 p = GraphToScreen(PortGraph(n, s, !outputs));
                float d = Vector2.DistanceSquared(screen, p);
                if (d <= best)
                {
                    best = d;
                    node = n;
                    slot = s;
                }
            }
        }
        return node != null;
    }

    C2_GraphNode HitNode(Vector2 screen)
    {
        for (int i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is not C2_GraphNode n || !n.is_visible)
            {
                continue;
            }
            if (n.Dimensions_Get().Contains(screen))
            {
                return n;
            }
        }
        return null;
    }

    void ApplyBoxSelect()
    {
        Vector2 min = Vector2.Min(_box_from, _box_to);
        Vector2 max = Vector2.Max(_box_from, _box_to);
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not C2_GraphNode n || !n.selectable)
            {
                continue;
            }
            TDimensions2 nd = n.Dimensions_Get();
            bool hit = nd.position.X < max.X && nd.position.X + nd.size.X > min.X
                && nd.position.Y < max.Y && nd.position.Y + nd.size.Y > min.Y;
            if (hit)
            {
                n.selected = true;
            }
            else if (_box_add)
            {
                n.selected = _box_prev.Contains(n);
            }
            else
            {
                n.selected = false;
            }
        }
    }

    void ClearNodeSelection()
    {
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is C2_GraphNode n)
            {
                n.selected = false;
            }
        }
    }

    void DeleteSelection()
    {
        if (selected_link != null)
        {
            bool any_node = false;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is C2_GraphNode n && n.selected)
                {
                    any_node = true;
                    break;
                }
            }
            if (!any_node)
            {
                Disconnect(selected_link);
                return;
            }
        }
        List<C2_GraphNode> kill = new();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is C2_GraphNode n && n.selected)
            {
                kill.Add(n);
            }
        }
        for (int i = 0; i < kill.Count; i++)
        {
            C2_GraphNode n = kill[i];
            for (int c = connections.Count - 1; c >= 0; c--)
            {
                TGraphLink l = connections[c];
                if (l.from == n || l.to == n)
                {
                    Disconnect(l);
                }
            }
            on_node_removed?.Invoke(n);
            n.Destroy();
        }
    }

    public void Nodes_Clear()
    {
        connections.Clear();
        selected_link = null;
        hovered_link = null;
        for (int i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is C2_GraphNode n)
            {
                n.Destroy();
            }
        }
    }

    public void Node_Focus(C2_GraphNode node)
    {
        ClearNodeSelection();
        selected_link = null;
        if (node == null)
        {
            return;
        }
        node.selected = true;
        Raise(node);
        TDimensions2 dim = Dimensions_Get();
        float z = zoom;
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        Vector2 view = dim.size / z;
        Vector2 p = node.graph_position;
        if (p.X < scroll_offset.X || p.X > scroll_offset.X + view.X - 80f)
        {
            scroll_offset.X = p.X - view.X * 0.3f;
        }
        if (p.Y < scroll_offset.Y || p.Y > scroll_offset.Y + view.Y - 80f)
        {
            scroll_offset.Y = p.Y - view.Y * 0.3f;
        }
    }

    public List<C2_GraphNode> Nodes_Get()
    {
        List<C2_GraphNode> list = new();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is C2_GraphNode n)
            {
                list.Add(n);
            }
        }
        return list;
    }

    void Raise(C2_GraphNode node)
    {
        Child_Insert(children.Count, node);
    }

    void BeginConnect(ImpPlayer player, Vector2 cursor, C2_GraphNode node, int slot, bool from_out)
    {
        _connect_node = node;
        _connect_slot = slot;
        _connect_from_out = from_out;
        _connect_type = PortType(node, slot, !from_out);
        _connect_color = PortColor(node, slot, !from_out);
        _connect_valid = false;
        _connect_target_ok = false;
        _connect_target = null;
        _just_disconnected = false;
        selected_link = null;
        BeginDrag(player, EDrag.Connect, cursor);
    }

    void BeginDrag(ImpPlayer player, EDrag mode, Vector2 cursor)
    {
        _drag = mode;
        _drag_cursor = cursor;
        player.target_focus = this;
        player.input_hog = this;
    }

    void EndDrag(ImpPlayer player)
    {
        _drag = EDrag.None;
        _connect_node = null;
        _connect_target = null;
        _drag_from.Clear();
        if (player.input_hog == this)
        {
            player.input_hog = null;
        }
    }

    bool PinEdit_IsBusy()
    {
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is C2_GraphNode n && n.SlotEdit_IsBusy())
            {
                return true;
            }
        }
        return false;
    }

    bool IsOurs(ImpComp c)
    {
        for (ImpComp n = c; n != null; n = n.parent)
        {
            if (n == this)
            {
                return true;
            }
        }
        return false;
    }

    static int PortType(C2_GraphNode n, int slot, bool left)
    {
        if (n == null || slot < 0 || slot >= n.slots.Count)
        {
            return 0;
        }
        if (left)
        {
            return n.slots[slot].type_left;
        }
        return n.slots[slot].type_right;
    }

    static Color PortColor(C2_GraphNode n, int slot, bool left)
    {
        if (n == null || slot < 0 || slot >= n.slots.Count)
        {
            return Color.White;
        }
        if (left)
        {
            return n.slots[slot].color_left;
        }
        return n.slots[slot].color_right;
    }

    static Color Tint(Color c, float m)
    {
        int r = (int)(c.R * m);
        int g = (int)(c.G * m);
        int b = (int)(c.B * m);
        if (r > 255)
        {
            r = 255;
        }
        if (g > 255)
        {
            g = 255;
        }
        if (b > 255)
        {
            b = 255;
        }
        return new Color(r, g, b, (int)c.A);
    }

    internal static float TitleHeight => TitleH;
    internal static float SlotHeight => SlotH;
    internal static float PortRadius => PortR;
}

[ImpClass(Hidden = true)]
public class C2_GraphNode : Imp2D
{
    [ImpVar] public string title = "Node";
    [ImpVar] public Vector2 graph_position;
    [ImpVar] public Vector2 graph_size = new(180, 80);
    [ImpVar] public Color title_color = new(70, 120, 180, 255);
    [ImpVar] public bool selected;
    [ImpVar] public bool draggable = true;
    [ImpVar] public bool selectable = true;
    [ImpVar] public EGraphNodeChrome chrome = EGraphNodeChrome.Regular;

    public object user_data;
    public List<TGraphSlot> slots = new();
    public Action<C2_GraphNode, int> on_slot_value;

    public C2_GraphNode()
    {
        cursor_filter = ECursorFilter.Hit;
        layout.orient_H = EUIViewportAlignment.Start;
        layout.orient_V = EUIViewportAlignment.Start;
        layout.size = graph_size;
    }

    public Vector2 GraphSize()
    {
        return graph_size;
    }

    public void RefreshSize()
    {
        int n = slots.Count;
        if (n < 1)
        {
            n = 1;
        }
        float h = C2_GraphEdit.TitleHeight + 8f + n * C2_GraphEdit.SlotHeight + 6f;
        float w = 160f;
        bool any_edit = false;
        bool wide_edit = false;
        C2_GraphEdit ge = parent as C2_GraphEdit;
        for (int i = 0; i < slots.Count; i++)
        {
            TGraphSlot s = slots[i];
            if (!s.edit_left || s.data_left == null)
            {
                continue;
            }
            bool wired = ge != null && ge.IsInputConnected(this, i);
            if (s.edit != null)
            {
                s.edit.is_visible = !wired;
            }
            if (wired)
            {
                continue;
            }
            any_edit = true;
            Type t = s.data_left;
            if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Color))
            {
                wide_edit = true;
            }
        }
        if (any_edit)
        {
            w = 280f;
            if (wide_edit)
            {
                w = 340f;
            }
        }
        graph_size = new Vector2(w, h);
    }

    public bool SlotEdit_Hit(Vector2 screen)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            Imp2D ed = slots[i].edit;
            if (ed == null || !ed.is_visible)
            {
                continue;
            }
            if (ed.Dimensions_Get().Contains(screen))
            {
                return true;
            }
        }
        return false;
    }

    public void SlotEdits_Rebuild()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            TGraphSlot s = slots[i];
            if (s.edit != null)
            {
                s.edit.Destroy();
                s.edit = null;
            }
            if (!s.edit_left || s.data_left == null)
            {
                continue;
            }
            Type t = s.data_left;
            if (s.value == null)
            {
                if (t == typeof(string))
                {
                    s.value = "";
                }
                else if (t.IsValueType)
                {
                    s.value = Activator.CreateInstance(t);
                }
            }
            int idx = i;
            Imp2D w = null;
            if (t == typeof(string))
            {
                C2_TextEdit te = new();
                te.hog_input = false;
                te.text = s.value as string ?? "";
                te.text_placeholder = s.name_left ?? "";
                te.on_text_changed = txt =>
                {
                    s.value = txt ?? "";
                    on_slot_value?.Invoke(this, idx);
                };
                w = te;
            }
            else if (t == typeof(bool))
            {
                C2_CheckBox cb = new();
                cb.box_size = 14f;
                cb.is_checked = s.value is bool b && b;
                cb.on_changed = v =>
                {
                    s.value = v;
                    on_slot_value?.Invoke(this, idx);
                };
                w = cb;
            }
            else if (t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(byte)
                || t == typeof(float) || t == typeof(double))
            {
                bool is_int = t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(byte);
                C2_Slider sl = new()
                {
                    is_spinner = true,
                    min = 0f,
                    max = 0f,
                    step = is_int ? 1f : 0f,
                    value_text_decimals = is_int ? 0 : 3,
                    drag_sensitivity = is_int ? 0.15f : 0.05f,
                };
                if (s.value != null)
                {
                    sl.Value_SetQuiet(Convert.ToSingle(s.value, CultureInfo.InvariantCulture));
                }
                sl.on_changed = slider =>
                {
                    if (t == typeof(int))
                    {
                        s.value = (int)Math.Clamp(MathF.Round(slider.value), int.MinValue, int.MaxValue);
                    }
                    else if (t == typeof(float))
                    {
                        s.value = slider.value;
                    }
                    else if (t == typeof(double))
                    {
                        s.value = (double)slider.value;
                    }
                    else if (t == typeof(uint))
                    {
                        s.value = (uint)Math.Max(0, MathF.Round(slider.value));
                    }
                    else if (t == typeof(long))
                    {
                        s.value = (long)MathF.Round(slider.value);
                    }
                    else if (t == typeof(byte))
                    {
                        s.value = (byte)Math.Clamp(MathF.Round(slider.value), 0, 255);
                    }
                    on_slot_value?.Invoke(this, idx);
                };
                w = sl;
            }
            else if (t.IsEnum)
            {
                string[] names = Enum.GetNames(t);
                C2_Dropdown drop = new() { placeholder_text = "-" };
                drop.Options_Set(names);
                string cur = s.value != null ? s.value.ToString() : "";
                drop.Option_SetQuiet(Array.IndexOf(names, cur));
                drop.on_dropdown_change = (_, opt, _) =>
                {
                    s.value = Enum.Parse(t, opt.name);
                    on_slot_value?.Invoke(this, idx);
                };
                w = drop;
            }
            else if (t == typeof(Color))
            {
                C2_ColorPicker cp = new();
                if (s.value is Color col)
                {
                    cp.Color_SetQuiet(col);
                }
                cp.on_color_changed = c =>
                {
                    s.value = c.color;
                    on_slot_value?.Invoke(this, idx);
                };
                w = cp;
            }
            else if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4))
            {
                int count = 2;
                if (t == typeof(Vector3))
                {
                    count = 3;
                }
                else if (t == typeof(Vector4))
                {
                    count = 4;
                }
                C2_VectorEdit vec = new(count);
                if (s.value is Vector2 v2)
                {
                    vec.Values_SetQuiet(new[] { v2.X, v2.Y });
                }
                else if (s.value is Vector3 v3)
                {
                    vec.Values_SetQuiet(new[] { v3.X, v3.Y, v3.Z });
                }
                else if (s.value is Vector4 v4)
                {
                    vec.Values_SetQuiet(new[] { v4.X, v4.Y, v4.Z, v4.W });
                }
                vec.on_changed = v =>
                {
                    if (count == 2)
                    {
                        s.value = new Vector2(v.Value_Get(0), v.Value_Get(1));
                    }
                    else if (count == 3)
                    {
                        s.value = new Vector3(v.Value_Get(0), v.Value_Get(1), v.Value_Get(2));
                    }
                    else
                    {
                        s.value = new Vector4(v.Value_Get(0), v.Value_Get(1), v.Value_Get(2), v.Value_Get(3));
                    }
                    on_slot_value?.Invoke(this, idx);
                };
                w = vec;
            }
            if (w == null)
            {
                continue;
            }
            s.edit = w;
            Child_Add(w);
        }
    }

    public void SlotEdits_Layout(float z)
    {
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        C2_GraphEdit ge = parent as C2_GraphEdit;
        TDimensions2 dim = Dimensions_Get();
        float th = C2_GraphEdit.TitleHeight * z;
        float slot_h = C2_GraphEdit.SlotHeight * z;
        float y0 = th + 6f * z;
        float label_w = 54f * z;
        for (int i = 0; i < slots.Count; i++)
        {
            TGraphSlot s = slots[i];
            if (s.edit == null)
            {
                continue;
            }
            bool wired = ge != null && ge.IsInputConnected(this, i);
            s.edit.is_visible = !wired;
            if (wired)
            {
                continue;
            }
            float x = 10f * z + label_w;
            float w = dim.size.X - x - 8f * z;
            if (s.data_left == typeof(bool))
            {
                w = 18f * z;
            }
            if (w < 16f * z)
            {
                w = 16f * z;
            }
            float h = slot_h - 4f * z;
            if (h < 12f)
            {
                h = 12f;
            }
            s.edit.transform.position = new Vector2(x, y0 + i * slot_h + (slot_h - h) * 0.5f);
            s.edit.position = Vector2.Zero;
            s.edit.layout = new TLayout2
            {
                size = new Vector2(w, h),
                size_min = Vector2.Zero,
                orient_H = EUIViewportAlignment.Start,
                orient_V = EUIViewportAlignment.Start,
            };
        }
    }

    public bool SlotEdit_IsBusy()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            Imp2D ed = slots[i].edit;
            if (ed == null || !ed.is_visible)
            {
                continue;
            }
            if (ed is C2_TextEdit te && te.is_focused)
            {
                return true;
            }
            if (ed is C2_Slider sl && sl.IsBusy)
            {
                return true;
            }
            if (ed is C2_VectorEdit ve && ve.IsBusy())
            {
                return true;
            }
            if (ed is C2_Dropdown drop && drop.IsOpen)
            {
                return true;
            }
            if (ed is C2_ColorPicker cp && cp.IsOpen)
            {
                return true;
            }
        }
        return false;
    }

    public void Slot_Set(int index, bool enable_left, int type_left, Color color_left, string name_left,
        bool enable_right, int type_right, Color color_right, string name_right)
    {
        while (slots.Count <= index)
        {
            slots.Add(new TGraphSlot());
        }
        TGraphSlot s = slots[index];
        s.enable_left = enable_left;
        s.type_left = type_left;
        s.color_left = color_left;
        s.name_left = name_left;
        s.enable_right = enable_right;
        s.type_right = type_right;
        s.color_right = color_right;
        s.name_right = name_right;
        RefreshSize();
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 1f || dim.size.Y <= 1f)
        {
            return;
        }

        float z = 1f;
        UI_Graph ui = UI_Graph.DEFAULT;
        if (parent is C2_GraphEdit ge)
        {
            if (ge.zoom > 1e-6f)
            {
                z = ge.zoom;
            }
            if (ge.style != null)
            {
                ui = ge.style;
            }
        }

        bool is_var = chrome == EGraphNodeChrome.Var;
        A_Texture sh;
        A_Texture body_tex;
        A_Texture spill;
        A_Texture gloss;
        A_Texture hi;
        TMargins body_slice;
        TMargins shadow_slice;
        float shadow_pad;
        if (is_var)
        {
            if (selected)
            {
                sh = ui.var_shadow_selected;
            }
            else
            {
                sh = ui.var_shadow;
            }
            body_tex = ui.var_body;
            spill = ui.var_spill;
            gloss = ui.var_gloss;
            hi = null;
            body_slice = ui.var_body_slice;
            shadow_slice = ui.var_shadow_slice;
            shadow_pad = ui.var_shadow_pad;
        }
        else
        {
            if (selected)
            {
                sh = ui.node_shadow_selected;
            }
            else
            {
                sh = ui.node_shadow;
            }
            body_tex = ui.node_body;
            spill = ui.node_spill;
            gloss = ui.node_title_gloss;
            hi = ui.node_title_highlight;
            body_slice = ui.node_body_slice;
            shadow_slice = ui.node_shadow_slice;
            shadow_pad = ui.node_shadow_pad;
        }

        float pad = shadow_pad * z;
        UI_Graph.DrawSlice(sh, dim.position - new Vector2(pad, pad), dim.size + new Vector2(pad * 2f, pad * 2f),
            shadow_slice, Color.White, z);
        UI_Graph.DrawSlice(body_tex, dim.position, dim.size, body_slice, ui.body_tint, z);

        float th = C2_GraphEdit.TitleHeight * z;
        Vector2 title_pos = dim.position;
        Vector2 title_sz = new(dim.size.X, th);
        if (is_var)
        {
            title_sz = dim.size;
        }
        UI_Graph.DrawSlice(spill, title_pos, title_sz, ui.spill_slice, title_color, z);
        UI_Graph.DrawSlice(gloss, title_pos, title_sz, ui.gloss_slice, Color.White, z);
        if (hi != null)
        {
            UI_Graph.DrawSlice(hi, title_pos, new Vector2(dim.size.X, th), ui.gloss_slice, Color.White, z);
        }

        float fs = MathF.Max(10f, 13f * z);
        UI_Text.LIGHT.Draw(title, dim.position + new Vector2(10f * z, 0f), new Vector2(dim.size.X - 16f * z, th),
            (int)fs, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);

        float slot_h = C2_GraphEdit.SlotHeight * z;
        float y0 = dim.position.Y + th + 6f * z;
        float pr = C2_GraphEdit.PortRadius * z;
        if (pr < 3.5f)
        {
            pr = 3.5f;
        }
        for (int i = 0; i < slots.Count; i++)
        {
            TGraphSlot s = slots[i];
            float cy = y0 + i * slot_h + slot_h * 0.5f;
            float label_h = slot_h;
            if (s.enable_left)
            {
                Raylib.DrawCircleV(new Vector2(dim.position.X, cy), pr + 1.2f, new Color(20, 20, 22, 255));
                Raylib.DrawCircleV(new Vector2(dim.position.X, cy), pr, s.color_left);
                if (!string.IsNullOrEmpty(s.name_left))
                {
                    float label_w = dim.size.X * 0.5f - 12f * z;
                    if (s.edit != null && s.edit.is_visible)
                    {
                        label_w = 54f * z;
                    }
                    UI_Text.LIGHT.Draw(s.name_left,
                        new Vector2(dim.position.X + 10f * z, cy - label_h * 0.5f),
                        new Vector2(label_w, label_h),
                        (int)MathF.Max(9f, 11f * z), ETextWrap.None,
                        EUIPositionAlignment.Center, EUIPositionAlignment.Start);
                }
            }
            if (s.enable_right)
            {
                Raylib.DrawCircleV(new Vector2(dim.position.X + dim.size.X, cy), pr + 1.2f, new Color(20, 20, 22, 255));
                Raylib.DrawCircleV(new Vector2(dim.position.X + dim.size.X, cy), pr, s.color_right);
                if (!string.IsNullOrEmpty(s.name_right))
                {
                    UI_Text.LIGHT.Draw(s.name_right,
                        new Vector2(dim.position.X + dim.size.X * 0.5f, cy - label_h * 0.5f),
                        new Vector2(dim.size.X * 0.5f - 10f * z, label_h),
                        (int)MathF.Max(9f, 11f * z), ETextWrap.None,
                        EUIPositionAlignment.Center, EUIPositionAlignment.End);
                }
            }
        }
    }

    public void TriggerOutput(int pin, int connections = -1)
    {
        // if connections <0, actiavte all nodes connected to the output pin
    }
    
    public virtual void OnNode_Enter(int pin, C2_GraphNode previous) {}
    public virtual void OnNode_Exit(int pin) {}
}

public class UI_Graph : ImpAsset
{
    public static UI_Graph DEFAULT = new();

    [ImpVar] public A_Texture node_body = A_Texture.GRAPH_NODE_BODY;
    [ImpVar] public A_Texture node_spill = A_Texture.GRAPH_NODE_SPILL;
    [ImpVar] public A_Texture node_title_gloss = A_Texture.GRAPH_NODE_GLOSS;
    [ImpVar] public A_Texture node_title_highlight = A_Texture.GRAPH_NODE_HIGHLIGHT;
    [ImpVar] public A_Texture node_shadow = A_Texture.GRAPH_NODE_SHADOW;
    [ImpVar] public A_Texture node_shadow_selected = A_Texture.GRAPH_NODE_SHADOW_SEL;
    [ImpVar] public A_Texture var_body = A_Texture.GRAPH_VAR_BODY;
    [ImpVar] public A_Texture var_spill = A_Texture.GRAPH_VAR_SPILL;
    [ImpVar] public A_Texture var_gloss = A_Texture.GRAPH_VAR_GLOSS;
    [ImpVar] public A_Texture var_shadow = A_Texture.GRAPH_VAR_SHADOW;
    [ImpVar] public A_Texture var_shadow_selected = A_Texture.GRAPH_VAR_SHADOW_SEL;

    [ImpVar] public Color body_tint = new(40, 42, 48, 255);
    [ImpVar] public TMargins node_body_slice = new(16, 16, 16, 16);
    [ImpVar] public TMargins node_shadow_slice = new(18, 18, 18, 18);
    [ImpVar] public TMargins var_body_slice = new(16, 16, 12, 12);
    [ImpVar] public TMargins var_shadow_slice = new(26, 26, 26, 26);
    [ImpVar] public TMargins spill_slice = new(4, 4, 4, 4);
    [ImpVar] public TMargins gloss_slice = new(12, 12, 12, 12);
    [ImpVar] public float node_shadow_pad = 18f;
    [ImpVar] public float var_shadow_pad = 20f;

    public static void DrawSlice(A_Texture tex, Vector2 pos, Vector2 size, TMargins slice, Color tint, float z)
    {
        if (size.X <= 0f || size.Y <= 0f)
        {
            return;
        }
        if (tex == null || tex.texture.Id == 0)
        {
            Raylib.DrawRectangleV(pos, size, tint);
            return;
        }
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        Texture2D t = tex.texture;
        float l = slice.left * z;
        float r = slice.right * z;
        float top = slice.top * z;
        float b = slice.bottom * z;
        float max_l = size.X * 0.45f;
        float max_t = size.Y * 0.45f;
        if (l > max_l)
        {
            l = max_l;
        }
        if (r > max_l)
        {
            r = max_l;
        }
        if (top > max_t)
        {
            top = max_t;
        }
        if (b > max_t)
        {
            b = max_t;
        }
        float tw = t.Width;
        float th = t.Height;
        float sl = slice.left;
        float sr = slice.right;
        float st = slice.top;
        float sb = slice.bottom;
        if (sl + sr > tw)
        {
            sl = tw * 0.45f;
            sr = tw * 0.45f;
        }
        if (st + sb > th)
        {
            st = th * 0.45f;
            sb = th * 0.45f;
        }
        float cx = MathF.Max(0f, tw - sl - sr);
        float cy = MathF.Max(0f, th - st - sb);
        float dx = MathF.Max(0f, size.X - l - r);
        float dy = MathF.Max(0f, size.Y - top - b);
        float px = pos.X;
        float py = pos.Y;

        void Patch(float sx, float sy, float sw, float sh, float dx_, float dy_, float dw, float dh)
        {
            if (sw <= 0f || sh <= 0f || dw <= 0f || dh <= 0f)
            {
                return;
            }
            Raylib.DrawTexturePro(t, new Rectangle(sx, sy, sw, sh), new Rectangle(dx_, dy_, dw, dh), Vector2.Zero, 0f, tint);
        }

        Patch(0, 0, sl, st, px, py, l, top);
        Patch(tw - sr, 0, sr, st, px + size.X - r, py, r, top);
        Patch(0, th - sb, sl, sb, px, py + size.Y - b, l, b);
        Patch(tw - sr, th - sb, sr, sb, px + size.X - r, py + size.Y - b, r, b);
        Patch(sl, 0, cx, st, px + l, py, dx, top);
        Patch(sl, th - sb, cx, sb, px + l, py + size.Y - b, dx, b);
        Patch(0, st, sl, cy, px, py + top, l, dy);
        Patch(tw - sr, st, sr, cy, px + size.X - r, py + top, r, dy);
        Patch(sl, st, cx, cy, px + l, py + top, dx, dy);
    }
}
