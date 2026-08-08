using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ImperiumEngine.Classes;

namespace Editor.Dialog;

// Picks a component type to add to the level hierarchy. Presents every addable component as a
// tree mirroring the class inheritance (C3_Character under C3_Collider, the lights under
// C3_Light...). Only abstract types are blocked; concrete bases like ImpComponent / ImpComponent2D
// / ImpComponent3D are selectable so they can act as empty "null" placeholders.
//
// "Addable" requires: non-abstract class assignable to ImpComponent, default-constructible, name
// resolvable by A_Entity.ResolveComponentType (level/entity round-trip), and
// ImpComponent.Editor_AllowAdd() true. That gate is evaluated without running any constructor —
// some components (C3_Light) load GPU resources in theirs — via an uninitialized instance.
public class DLG_SelectComponent : EditorDialog
{
    // a node in the inheritance tree. Abstract bases stay as non-selectable group headers.
    class Node
    {
        public Type type = null!;
        public bool addable;
        public readonly List<Node> children = new();
    }

    // the tree is a pure function of the loaded assembly, so it is built once and shared.
    static List<Node>? s_roots;
    static List<Type>? s_flat;   // all addable types, for the filtered flat view

    string _filter = "";
    Type? _selected;
    Action<ImpComponent>? _on_selected;

    public override string Title => "Add Component";

    // The flat list of addable component types (same gate the picker uses), shared with other
    // dialogs that offer a component-type tree — e.g. DLG_NewEntity's root-type picker.
    public static IReadOnlyList<Type> AddableTypes()
    {
        Build();
        return s_flat!;
    }

    // Queues the picker. On confirm `on_selected` receives a freshly constructed component
    // instance of the chosen type (the constructor runs here, not while building the tree).
    public static void Show(Action<ImpComponent> on_selected)
    {
        Build();
        new DLG_SelectComponent { _on_selected = on_selected }.Show();
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        ImGui.SetNextItemWidth(360);
        ImGui.InputTextWithHint("##filter", "Search components…", ref _filter, 64);

        ImGui.BeginChild("component_tree", new Vector2(360, 340), ImGuiChildFlags.Borders);
        if (_filter.Trim().Length > 0)
            DrawFiltered(_filter.Trim());
        else
            foreach (var n in s_roots!)
                DrawNode(n);
        ImGui.EndChild();

        ImGui.TextDisabled(_selected != null ? $"Selected: {_selected.Name}" : "Select a component.");
        ImGui.Separator();

        ImGui.BeginDisabled(_selected == null);
        if (ImGui.Button("Add", new Vector2(120, 0)))
            Commit();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120, 0)))
            Dismiss();
    }

    // --- tree view ---

    void DrawNode(Node n)
    {
        var node_flags = ImGuiTreeNodeFlags.SpanAvailWidth
                       | ImGuiTreeNodeFlags.OpenOnArrow
                       | ImGuiTreeNodeFlags.OpenOnDoubleClick
                       | ImGuiTreeNodeFlags.DefaultOpen;

        if (n.children.Count == 0) node_flags |= ImGuiTreeNodeFlags.Leaf;
        if (n.addable && _selected == n.type) node_flags |= ImGuiTreeNodeFlags.Selected;

        // headers (abstract classes) are dimmed and can't be picked
        if (!n.addable) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        bool open = ImGui.TreeNodeEx($"{EditorIcons.LabelPad}{n.type.Name}##{n.type.FullName}", node_flags);
        EditorIcons.DrawOnLastItem(n.type, tree: true);
        if (!n.addable) ImGui.PopStyleColor();

        if (n.addable && ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            _selected = n.type;
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) Commit();
        }

        if (open)
        {
            foreach (var c in n.children) DrawNode(c);
            ImGui.TreePop();
        }
    }

    void DrawFiltered(string filter)
    {
        foreach (var t in s_flat!)
        {
            if (t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (ImGui.Selectable($"{EditorIcons.LabelPad}{t.Name}##{t.FullName}", _selected == t,
                    ImGuiSelectableFlags.AllowDoubleClick))
            {
                _selected = t;
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) Commit();
            }
            EditorIcons.DrawOnLastItem(t, tree: false);
        }
    }

    void Commit()
    {
        if (_selected == null) return;
        var inst = (ImpComponent)Activator.CreateInstance(_selected)!;   // runs the ctor now
        var cb = _on_selected;
        Dismiss();
        cb?.Invoke(inst);
    }

    // --- type discovery ---

    static void Build()
    {
        if (s_roots != null) return;

        var asm = typeof(ImpComponent).Assembly;
        var addable = asm.GetTypes().Where(IsAddable).OrderBy(t => t.Name).ToList();
        s_flat = addable;

        // grow the inheritance tree upward from each addable type, so intermediate base classes
        // (ImpComponent3D, C3_Light, ...) become shared nodes; abstract ones stay non-selectable.
        var nodes = new Dictionary<Type, Node>();
        Node Get(Type t)
        {
            if (!nodes.TryGetValue(t, out var n))
                nodes[t] = n = new Node { type = t, addable = IsAddable(t) };
            return n;
        }

        foreach (var t in addable)
        {
            for (var x = t; x != null && x != typeof(object) && x.BaseType != null
                           && x.BaseType != typeof(object); x = x.BaseType)
            {
                var child = Get(x);
                var parent = Get(x.BaseType);
                if (!parent.children.Contains(child)) parent.children.Add(child);
            }
        }

        foreach (var n in nodes.Values)
            n.children.Sort((a, b) => string.CompareOrdinal(a.type.Name, b.type.Name));

        // ImpComponent is the visible root (selectable when non-abstract / Editor_AllowAdd)
        s_roots = nodes.TryGetValue(typeof(ImpComponent), out var root)
            ? new List<Node> { root }
            : new List<Node>();
    }

    // Only abstract classes are blocked. Concrete bases (ImpComponent, ImpComponent2D/3D, …)
    // stay addable so they can act as empty placeholders. ImpLevel etc. still opt out via
    // Editor_AllowAdd().
    static bool IsAddable(Type t)
    {
        if (!t.IsClass || t.IsAbstract) return false;
        if (!typeof(ImpComponent).IsAssignableFrom(t)) return false;
        if (t.GetConstructor(Type.EmptyTypes) == null) return false;
        // must round-trip through level/entity load by simple name
        if (ImperiumEngine.Objects.Assets.A_Entity.ResolveComponentType(t.Name) != t) return false;
        return EditorAllowsAdd(t);
    }

    // reads ImpComponent.Editor_AllowAdd() (a protected virtual) without constructing the type —
    // an uninitialized instance is enough for the override to dispatch, and it skips ctors that
    // would otherwise touch the GPU / load assets.
    static readonly MethodInfo? s_allow_add = typeof(ImpComponent)
        .GetMethod("Editor_AllowAdd", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    static bool EditorAllowsAdd(Type t)
    {
        if (s_allow_add == null) return true;
        try { return (bool)s_allow_add.Invoke(RuntimeHelpers.GetUninitializedObject(t), null)!; }
        catch { return true; }
    }
}
