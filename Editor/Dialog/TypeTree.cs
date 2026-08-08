using System.Numerics;
using ImGuiNET;

namespace Editor.Dialog;

// A searchable tree of types that mirrors the class inheritance: the caller supplies the set of
// selectable "leaf" types, and their base classes are grown in as dimmed, non-selectable group
// headers (exactly like DLG_SelectComponent's component picker). Shared by the New Asset / New
// Entity dialogs. Type icons: EditorIcons (name-matched PNGs under {engine}/icons/components).
class TypeTree
{
    // a node in the inheritance tree. Base classes are present as headers (selectable == false).
    class Node
    {
        public Type type = null!;
        public bool selectable;
        public readonly List<Node> children = new();
    }

    readonly List<Node> _roots = new();
    readonly List<Type> _flat;   // all selectable types, for the filtered flat view

    public Type? SelectedType;

    // Builds the inheritance tree from `leaves`, growing upward and stopping at `stop`
    // (exclusive — `stop` itself is the invisible container whose children are the roots).
    public TypeTree(IEnumerable<Type> leaves, Type stop)
    {
        _flat = leaves.OrderBy(t => t.Name).ToList();
        var leafSet = new HashSet<Type>(_flat);

        var nodes = new Dictionary<Type, Node>();
        Node Get(Type t)
        {
            if (!nodes.TryGetValue(t, out var n))
                nodes[t] = n = new Node { type = t, selectable = leafSet.Contains(t) };
            return n;
        }

        foreach (var t in _flat)
            for (var x = t; x != null && x != stop && x.BaseType != null; x = x.BaseType)
            {
                var child = Get(x);
                var parent = Get(x.BaseType);
                if (!parent.children.Contains(child)) parent.children.Add(child);
            }

        foreach (var n in nodes.Values)
            n.children.Sort((a, b) => string.CompareOrdinal(a.type.Name, b.type.Name));

        if (nodes.TryGetValue(stop, out var root)) _roots = root.children;
    }

    // Draws the search box + tree (the tree filling a child region of `size`). Returns true on a
    // "commit" gesture — a selectable type double-clicked — so the caller can confirm immediately.
    public bool Draw(ref string filter, Vector2 size)
    {
        bool commit = false;

        ImGui.SetNextItemWidth(size.X);
        ImGui.InputTextWithHint("##filter", "Search…", ref filter, 64);

        ImGui.BeginChild("type_tree", size, ImGuiChildFlags.Borders);
        string f = filter.Trim();
        if (f.Length > 0)
            commit = DrawFiltered(f);
        else
            foreach (var n in _roots) commit |= DrawNode(n);
        ImGui.EndChild();

        return commit;
    }

    bool DrawNode(Node n)
    {
        bool commit = false;

        var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                  | ImGuiTreeNodeFlags.OpenOnArrow
                  | ImGuiTreeNodeFlags.OpenOnDoubleClick
                  | ImGuiTreeNodeFlags.DefaultOpen;

        if (n.children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (n.selectable && SelectedType == n.type) flags |= ImGuiTreeNodeFlags.Selected;

        // headers (abstract/base classes) are dimmed and can't be picked
        if (!n.selectable) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        bool open = ImGui.TreeNodeEx($"{EditorIcons.LabelPad}{n.type.Name}##{n.type.FullName}", flags);
        EditorIcons.DrawOnLastItem(n.type, tree: true);
        if (!n.selectable) ImGui.PopStyleColor();

        if (n.selectable && ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            SelectedType = n.type;
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) commit = true;
        }

        if (open)
        {
            foreach (var c in n.children) commit |= DrawNode(c);
            ImGui.TreePop();
        }
        return commit;
    }

    bool DrawFiltered(string filter)
    {
        bool commit = false;
        foreach (var t in _flat)
        {
            if (t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (ImGui.Selectable($"{EditorIcons.LabelPad}{t.Name}##{t.FullName}", SelectedType == t,
                    ImGuiSelectableFlags.AllowDoubleClick))
            {
                SelectedType = t;
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) commit = true;
            }
            EditorIcons.DrawOnLastItem(t, tree: false);
        }
        return commit;
    }
}
