using System.Globalization;
using System.Numerics;
using System.Reflection;
using Editor.UI;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Sandbox;
using Engine.Structs;
using Engine.Vis.Nodes;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Panels;

/*
 *  Graph for editing a script (usually for an ImpScene, but will probably add support for other types later.
 * LAYOUT:
 * Left: properties (functions, vars, and signals)
 *  - Tabs: Custom (custom properties added to this script) | Native : (properties inherited from the script type/parent class)
 * Center: script node graph
 *  - right click opens placable properties avaialble for this class
 *  - drag of a node pin opens placable properties contextual to what is dragged (E.g drag off an object var, it shows funcs/vars that object can call)
 * Right: inspector tabs
 *  - tab 1: shows the config for whatever property (function, var, or singla) you have currently selected (like name, min/max, input/output arbs for functions, and other specifics)
 *  - tab 2: shows config for this script, such as parent type (note trying to change that should come with a big warning)
 * 
 */
public class PNL_ScriptGraph : EdPanel
{
    public A_Script? script;
    public Action? on_changed;

    const float TitleH = 26f;
    const float RowH = 20f;
    const float Split = 5f;

    readonly EUI_SearchBar _search = new() { hint = "Search" };
    float _left_w = 220f;
    float _right_w = 240f;
    Vector2 _pan = new(40, 40);
    TGuid64 _sel;
    TGuid64 _drag;
    Vector2 _drag_off;
    Vector2 _graph_size;
    string? _sel_native;

    public override void OnDrawPanel()
    {
        if (script == null)
        {
            ImGui.TextDisabled("No script");
            return;
        }

        float avail = ImGui.GetContentRegionAvail().X;
        _left_w = Math.Clamp(_left_w, 150f, MathF.Max(150f, avail - 280f));
        _right_w = Math.Clamp(_right_w, 170f, MathF.Max(170f, avail - _left_w - 120f));
        float center_w = MathF.Max(80f, avail - _left_w - _right_w - Split * 2f);

        ImGui.BeginChild("##vis_left", new Vector2(_left_w, 0), true);
        DrawMembers();
        ImGui.EndChild();

        Splitter("##vis_split_l", ref _left_w, 1);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.BeginChild("##vis_graph", new Vector2(center_w, 0), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar();
        DrawGraph();
        ImGui.EndChild();

        Splitter("##vis_split_r", ref _right_w, -1);

        ImGui.BeginChild("##vis_right", Vector2.Zero, true);
        DrawInspector();
        ImGui.EndChild();
    }

    static void Splitter(string id, ref float width, int sign)
    {
        ImGui.SameLine(0, 0);
        ImGui.InvisibleButton(id, new Vector2(Split, ImGui.GetContentRegionAvail().Y));
        if (ImGui.IsItemActive())
            width += ImGui.GetIO().MouseDelta.X * sign;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
        ImGui.SameLine(0, 0);
    }

    // ------------------------------------------------------------------------------------------
    // Left: Custom | Native
    // ------------------------------------------------------------------------------------------
    void DrawMembers()
    {
        _search.OnDraw();
        if (!ImGui.BeginTabBar("##vis_members"))
            return;

        if (ImGui.BeginTabItem("Custom"))
        {
            DrawCustom();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Native"))
        {
            DrawNative();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    bool Filter(string name)
    {
        string q = _search.search_text ?? "";
        return string.IsNullOrWhiteSpace(q) || name.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    void DrawCustom()
    {
        if (ImGui.TreeNodeEx("Functions", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int n = 0;
            foreach (VisNode node in script!.nodes.Values)
            {
                if (node is not VN_Hook_Enter enter) continue;
                if (!Filter(enter.hook_name)) continue;
                n++;
                bool sel = _sel == node.id;
                if (ImGui.Selectable(enter.hook_name, sel))
                {
                    _sel = node.id;
                    _sel_native = null;
                }
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    FrameTo(node);
            }
            foreach (var pair in script.funcs)
            {
                string name = pair.Value.name.Value;
                if (string.IsNullOrEmpty(name) || !Filter(name)) continue;
                n++;
                ImGui.Selectable(name);
            }
            if (n == 0) ImGui.TextDisabled("None");
            ImGui.TreePop();
        }
        if (ImGui.TreeNodeEx("Vars", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int n = 0;
            foreach (var pair in script!.vars)
            {
                string name = pair.Value.name.Value;
                if (string.IsNullOrEmpty(name) || !Filter(name)) continue;
                n++;
                ImGui.Selectable(name);
            }
            if (n == 0) ImGui.TextDisabled("None");
            ImGui.TreePop();
        }
        if (ImGui.TreeNodeEx("Signals", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int n = 0;
            foreach (var pair in script!.signals)
            {
                string name = pair.Value.name.Value;
                if (string.IsNullOrEmpty(name) || !Filter(name)) continue;
                n++;
                ImGui.Selectable(name);
            }
            if (n == 0) ImGui.TextDisabled("None");
            ImGui.TreePop();
        }
    }

    void DrawNative()
    {
        Type type = script!.class_type ?? typeof(ImpComp);
        if (ImGui.TreeNodeEx("Functions", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int n = 0;
            foreach (MethodInfo m in NativeHooks(type))
            {
                string title = m.GetCustomAttribute<TitleAttribute>()?.Name ?? m.Name;
                if (!Filter(title) && !Filter(m.Name)) continue;
                n++;
                VisNode? placed = FindHook(m.Name);
                if (ImGui.Selectable(title, _sel_native == m.Name || (placed != null && _sel == placed.id)))
                {
                    _sel_native = m.Name;
                    _sel = placed?.id ?? default;
                }
                if (placed != null && ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    FrameTo(placed);
            }
            if (n == 0) ImGui.TextDisabled("None");
            ImGui.TreePop();
        }
        if (ImGui.TreeNodeEx("Vars", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int n = 0;
            foreach (MemberInfo mem in NativeVars(type))
            {
                string title = mem.GetCustomAttribute<TitleAttribute>()?.Name ?? mem.Name;
                if (!Filter(title) && !Filter(mem.Name)) continue;
                n++;
                ImGui.Selectable(title);
            }
            if (n == 0) ImGui.TextDisabled("None");
            ImGui.TreePop();
        }
        if (ImGui.TreeNodeEx("Signals", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int n = 0;
            foreach (MemberInfo mem in NativeSignals(type))
            {
                string title = mem.GetCustomAttribute<TitleAttribute>()?.Name ?? mem.Name;
                if (!Filter(title) && !Filter(mem.Name)) continue;
                n++;
                ImGui.Selectable(title);
            }
            if (n == 0) ImGui.TextDisabled("None");
            ImGui.TreePop();
        }
    }

    VisNode? FindHook(string name)
    {
        foreach (VisNode n in script!.nodes.Values)
            if (n is VN_Hook_Enter e && e.hook_name == name) return n;
        return null;
    }

    static List<MethodInfo> NativeHooks(Type type)
    {
        List<MethodInfo> list = new();
        HashSet<string> seen = new();
        for (Type? t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.GetCustomAttribute<ScriptHookAttribute>() == null) continue;
                if (!seen.Add(m.Name)) continue;
                list.Add(m);
            }
        }
        return list;
    }

    static List<MemberInfo> NativeVars(Type type)
    {
        List<MemberInfo> list = new();
        for (Type? t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                ImpVarAttribute? a = f.GetCustomAttribute<ImpVarAttribute>();
                if (a == null || a.Hidden) continue;
                list.Add(f);
            }
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!p.CanRead || !p.CanWrite) continue;
                ImpVarAttribute? a = p.GetCustomAttribute<ImpVarAttribute>();
                if (a == null || a.Hidden) continue;
                list.Add(p);
            }
        }
        return list;
    }

    static List<MemberInfo> NativeSignals(Type type)
    {
        List<MemberInfo> list = new();
        for (Type? t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (f.GetCustomAttribute<ScriptSignalAttribute>() != null) list.Add(f);
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (p.GetCustomAttribute<ScriptSignalAttribute>() != null) list.Add(p);
        }
        return list;
    }

    // ------------------------------------------------------------------------------------------
    // Center: graph
    // ------------------------------------------------------------------------------------------
    void DrawGraph()
    {
        Vector2 avail = ImGui.GetContentRegionAvail();
        if (avail.X < 8) avail.X = 8;
        if (avail.Y < 8) avail.Y = 8;

        _graph_size = avail;
        ImGui.InvisibleButton("##vis_canvas", avail);
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        bool hover = ImGui.IsItemHovered();
        ImGuiIOPtr io = ImGui.GetIO();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        dl.AddRectFilled(min, max, 0xFF16161C);
        dl.PushClipRect(min, max, true);

        float step = 28f;
        uint grid = 0x22FFFFFF;
        float x0 = _pan.X % step; if (x0 < 0) x0 += step;
        float y0 = _pan.Y % step; if (y0 < 0) y0 += step;
        for (float x = min.X + x0; x < max.X; x += step)
            dl.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), grid);
        for (float y = min.Y + y0; y < max.Y; y += step)
            dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), grid);

        Vector2 Origin(VisNode n) => min + _pan + n.ed_pos;
        Vector2 PinScreen(VisNode n, bool output, int pin) => Origin(n) + PinPos(n, output, pin);

        foreach (VisWire w in script!.wires)
        {
            if (!script.nodes.TryGetValue(w.from, out VisNode? a)) continue;
            if (!script.nodes.TryGetValue(w.to, out VisNode? b)) continue;
            Vector2 pa = PinScreen(a, true, w.from_pin);
            Vector2 pb = PinScreen(b, false, w.to_pin);
            uint col = PinCol(PinAt(a, true, w.from_pin));
            float dx = MathF.Max(40f, MathF.Abs(pb.X - pa.X) * 0.45f);
            dl.AddBezierCubic(pa, pa + new Vector2(dx, 0), pb - new Vector2(dx, 0), pb, col, 2.4f);
        }

        foreach (VisNode n in script.nodes.Values)
        {
            Vector2 p = Origin(n);
            Vector2 sz = NodeSize(n);
            uint title = Pack(n.ED_GetTitleColor());
            bool selected = _sel == n.id;
            dl.AddRectFilled(p, p + new Vector2(sz.X, TitleH), title, 5f, ImDrawFlags.RoundCornersTop);
            dl.AddRectFilled(p + new Vector2(0, TitleH - 2), p + sz, 0xFF2A2A33, 5f, ImDrawFlags.RoundCornersBottom);
            dl.AddRect(p, p + sz, selected ? 0xFFFFFFFF : 0xFF000000, 5f, ImDrawFlags.None, selected ? 2f : 1f);
            dl.AddText(p + new Vector2(10, 5), 0xFFFFFFFF, n.ED_GetTitle());
            DrawPins(dl, n, p);
        }

        if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            TGuid64 hit = Hit(io.MousePos - min - _pan);
            _sel = hit;
            if (hit.IsNone) _sel_native = null;
            _drag = hit;
            if (!_drag.IsNone && script.nodes.TryGetValue(_drag, out VisNode? n))
                _drag_off = n.ed_pos - (io.MousePos - min - _pan);
        }
        if (!_drag.IsNone && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            if (script.nodes.TryGetValue(_drag, out VisNode? n))
            {
                n.ed_pos = io.MousePos - min - _pan + _drag_off;
                on_changed?.Invoke();
            }
        }
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            _drag = default;

        if (hover && _drag.IsNone &&
            (ImGui.IsMouseDragging(ImGuiMouseButton.Middle) || ImGui.IsMouseDragging(ImGuiMouseButton.Right)))
            _pan += io.MouseDelta;

        dl.PopClipRect();
    }

    void FrameTo(VisNode n)
    {
        Vector2 sz = NodeSize(n);
        Vector2 view = _graph_size.X > 1 ? _graph_size : new Vector2(400, 300);
        _pan = view * 0.5f - n.ed_pos - sz * 0.5f;
    }

    TGuid64 Hit(Vector2 local)
    {
        TGuid64 hit = default;
        foreach (VisNode n in script!.nodes.Values)
        {
            Vector2 sz = NodeSize(n);
            if (local.X >= n.ed_pos.X && local.Y >= n.ed_pos.Y &&
                local.X <= n.ed_pos.X + sz.X && local.Y <= n.ed_pos.Y + sz.Y)
                hit = n.id;
        }
        return hit;
    }

    Vector2 NodeSize(VisNode n)
    {
        int rows = Math.Max(DataCount(n.inputs), DataCount(n.outputs));
        float w = ImGui.CalcTextSize(n.ED_GetTitle()).X + 48f;
        void Fit(VisPin[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].IsExec) continue;
                string label = arr[i].name.Value;
                string prev = PinPreview(n, arr[i]);
                if (prev.Length > 0) label += "  " + prev;
                w = MathF.Max(w, ImGui.CalcTextSize(label).X + 36f);
            }
        }
        Fit(n.inputs);
        Fit(n.outputs);
        return new Vector2(Math.Clamp(w, 168f, 280f), TitleH + Math.Max(1, rows) * RowH + 6f);
    }

    Vector2 PinPos(VisNode n, bool output, int pin)
    {
        Vector2 sz = NodeSize(n);
        VisPin[] arr = output ? n.outputs : n.inputs;
        float x = output ? sz.X : 0;
        if ((uint)pin >= (uint)arr.Length || arr[pin].IsExec)
            return new Vector2(x, TitleH * 0.5f);
        int di = 0;
        for (int i = 0; i <= pin; i++)
            if (!arr[i].IsExec) di++;
        return new Vector2(x, TitleH + (di - 0.5f) * RowH);
    }

    static VisPin PinAt(VisNode n, bool output, int pin)
    {
        VisPin[] arr = output ? n.outputs : n.inputs;
        return (uint)pin < (uint)arr.Length ? arr[pin] : default;
    }

    static int DataCount(VisPin[] arr)
    {
        int c = 0;
        for (int i = 0; i < arr.Length; i++)
            if (!arr[i].IsExec) c++;
        return c;
    }

    void DrawPins(ImDrawListPtr dl, VisNode n, Vector2 p)
    {
        DrawPinSet(dl, n, p, n.inputs, false);
        DrawPinSet(dl, n, p, n.outputs, true);
    }

    void DrawPinSet(ImDrawListPtr dl, VisNode n, Vector2 p, VisPin[] arr, bool output)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            Vector2 c = p + PinPos(n, output, i);
            uint col = PinCol(arr[i]);
            if (arr[i].IsExec)
            {
                Vector2 a = c + new Vector2(-4, -6);
                Vector2 b = c + new Vector2(-4, 6);
                Vector2 d = c + new Vector2(7, 0);
                dl.AddTriangleFilled(a, b, d, col);
                dl.AddTriangle(a, b, d, 0xFF000000);
                continue;
            }
            dl.AddCircleFilled(c, 5.5f, col);
            dl.AddCircle(c, 5.5f, 0xFF000000);
            string label = arr[i].name.Value;
            string prev = PinPreview(n, arr[i]);
            if (prev.Length > 0) label += "  " + prev;
            Vector2 ts = ImGui.CalcTextSize(label);
            Vector2 tp = output
                ? c + new Vector2(-10 - ts.X, -ts.Y * 0.5f)
                : c + new Vector2(10, -ts.Y * 0.5f);
            dl.AddText(tp, 0xFFCCCCCC, label);
        }
    }

    static string PinPreview(VisNode n, VisPin pin)
    {
        string name = pin.name.Value;
        if (n is VN_Delay d && name == "duration")
            return d.duration.ToString("0.##", CultureInfo.InvariantCulture);
        if (n.literals.TryGetValue(name, out object? v) && v != null)
            return v.ToString() ?? "";
        return "";
    }

    static uint PinCol(VisPin p)
    {
        if (p.IsExec || p.type == null) return 0xFFFFFFFF;
        Type t = p.type;
        if (t == typeof(bool)) return Pack(new Color(220, 70, 70, 255));
        if (t == typeof(string)) return Pack(new Color(220, 90, 180, 255));
        if (t == typeof(float) || t == typeof(double) || t == typeof(int))
            return Pack(new Color(80, 200, 140, 255));
        return Pack(new Color(80, 170, 230, 255));
    }

    // ------------------------------------------------------------------------------------------
    // Right: Details | Script
    // ------------------------------------------------------------------------------------------
    void DrawInspector()
    {
        if (!ImGui.BeginTabBar("##vis_inspect"))
            return;
        if (ImGui.BeginTabItem("Details"))
        {
            DrawDetails();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Script"))
        {
            DrawScriptSettings();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    void DrawDetails()
    {
        if (!_sel.IsNone && script!.nodes.TryGetValue(_sel, out VisNode? n))
        {
            ImGui.TextUnformatted(n.ED_GetTitle());
            ImGui.TextDisabled(n.GetType().Name);
            ImGui.Separator();
            if (n is VN_Delay delay)
            {
                float dur = delay.duration;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat("##duration", ref dur, 0.05f, 0f, 3600f, "Duration: %.2f s"))
                {
                    delay.duration = dur;
                    Changed();
                }
            }
            else if (n is VN_Hook_Enter enter)
            {
                ImGui.BeginDisabled();
                string hook = enter.hook_name ?? "";
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##hook", ref hook, 64);
                ImGui.EndDisabled();
            }
            else
            {
                bool any = false;
                for (int i = 0; i < n.inputs.Length; i++)
                {
                    VisPin pin = n.inputs[i];
                    if (pin.IsExec) continue;
                    any = true;
                    DrawLiteral(n, pin);
                }
                if (!any) ImGui.TextDisabled("No properties");
            }
            return;
        }

        if (!string.IsNullOrEmpty(_sel_native))
        {
            ImGui.TextUnformatted(_sel_native);
            ImGui.TextDisabled("Native function");
            ImGui.Separator();
            ImGui.TextWrapped("Double-click in the Native list to focus a placed node.");
            return;
        }

        ImGui.TextDisabled("Nothing selected");
    }

    void DrawLiteral(VisNode n, VisPin pin)
    {
        string name = pin.name.Value;
        object? cur = n.literals.TryGetValue(name, out object? v) ? v : null;
        ImGui.PushID(name);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(name);
        ImGui.SetNextItemWidth(-1);
        Type t = pin.type ?? typeof(string);
        if (t == typeof(bool))
        {
            bool b = cur is bool bb && bb;
            if (ImGui.Checkbox("##v", ref b))
            {
                n.literals[name] = b;
                Changed();
            }
        }
        else if (t == typeof(float) || t == typeof(double))
        {
            float f = 0f;
            try { if (cur != null) f = Convert.ToSingle(cur, CultureInfo.InvariantCulture); } catch { }
            if (ImGui.DragFloat("##v", ref f, 0.05f))
            {
                n.literals[name] = t == typeof(double) ? (object)(double)f : f;
                Changed();
            }
        }
        else if (t == typeof(int))
        {
            int i = 0;
            try { if (cur != null) i = Convert.ToInt32(cur, CultureInfo.InvariantCulture); } catch { }
            if (ImGui.DragInt("##v", ref i))
            {
                n.literals[name] = i;
                Changed();
            }
        }
        else
        {
            string s = cur?.ToString() ?? "";
            if (ImGui.InputText("##v", ref s, 256))
            {
                n.literals[name] = s;
                Changed();
            }
        }
        ImGui.PopID();
    }

    void DrawScriptSettings()
    {
        ImGui.TextUnformatted("Parent type");
        string name = script!.class_type?.Name ?? "ImpComp";
        ImGui.SetNextItemWidth(-1);
        ImGui.BeginDisabled();
        ImGui.InputText("##parent", ref name, 64);
        ImGui.EndDisabled();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.75f, 0.25f, 1f));
        ImGui.TextWrapped("Changing the parent type can break existing nodes, wires, and overrides.");
        ImGui.PopStyleColor();
    }

    void Changed()
    {
        script?.Invalidate();
        on_changed?.Invoke();
    }

    static uint Pack(Color c) => (uint)(c.R | (c.G << 8) | (c.B << 16) | (c.A << 24));
}
