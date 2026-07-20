using System.Collections;
using System.Numerics;
using System.Reflection;
using Editor.Dialog;
using ImGuiNET;
using ImperiumEngine;
using ImperiumEngine.Classes;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Panels;

// a panel that displays the property inspector & editor for the selected objects
public class PNL_Inspector : EditorPanel
{
    public List<object> selected_objects = new();

    // fired when any field is edited this frame, so the owning window can mark its asset dirty
    public Action? on_changed;

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        if (selected_objects.Count == 0)
        {
            ImGui.TextDisabled("Nothing selected");
            return;
        }

        bool changed = false;
        foreach (var obj in selected_objects)
        {
            ImGui.PushID(obj.GetHashCode());
            ImGui.SeparatorText(obj.GetType().Name);
            if (DrawImpVarFields(obj, 0)) changed = true;
            ImGui.PopID();
        }

        if (changed) on_changed?.Invoke();
    }

    // Draws an edit row for every [ImpVar] field on obj, walking the type chain root-first so
    // base fields (guid, is_visible...) come first. Shared by the top-level object inspector
    // and by embedded/referenced asset slots. Returns true if any field was edited.
    static bool DrawImpVarFields(object obj, int depth)
    {
        var chain = new List<Type>();
        for (var t = obj.GetType(); t != null && t != typeof(object); t = t.BaseType)
            chain.Insert(0, t);

        bool changed = false;
        foreach (var type in chain)
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!field.IsDefined(typeof(ImpVarAttribute), false)) continue;

            object? value = field.GetValue(obj);
            ImGui.PushID(field.Name);
            if (DrawValue(field.Name, field.FieldType, ref value, depth))
            {
                field.SetValue(obj, value);
                changed = true;
            }
            ImGui.PopID();
        }
        return changed;
    }

    // draws an edit widget for a value of any supported type. Unrecognized structs
    // cascade recursively: each renders as a tree node over its public fields, so
    // nested struct hierarchies are editable to any depth.
    // Returns true if edited — value then holds the new (boxed) value.
    static bool DrawValue(string label, Type t, ref object? value, int depth = 0)
    {
        if (t == typeof(float))
        {
            float v = (float)value!;
            if (!ImGui.DragFloat(label, ref v, 0.1f)) return false;
            value = v;
            return true;
        }
        if (t == typeof(double))
        {
            double d = (double)value!;
            float v = (float)d;
            if (!ImGui.DragFloat(label, ref v, 0.1f)) return false;
            value = (double)v;
            return true;
        }
        if (t == typeof(int))
        {
            int v = (int)value!;
            if (!ImGui.DragInt(label, ref v)) return false;
            value = v;
            return true;
        }
        if (t == typeof(long))
        {
            int v = (int)(long)value!;
            if (!ImGui.DragInt(label, ref v)) return false;
            value = (long)v;
            return true;
        }
        if (t == typeof(bool))
        {
            bool v = (bool)value!;
            if (!ImGui.Checkbox(label, ref v)) return false;
            value = v;
            return true;
        }
        if (t == typeof(string))
        {
            string v = (string?)value ?? "";
            if (!ImGui.InputText(label, ref v, 512)) return false;
            value = v;
            return true;
        }
        if (t == typeof(Vector2))
        {
            Vector2 v = (Vector2)value!;
            if (!ImGui.DragFloat2(label, ref v, 0.1f)) return false;
            value = v;
            return true;
        }
        if (t == typeof(Vector3))
        {
            Vector3 v = (Vector3)value!;
            if (!ImGui.DragFloat3(label, ref v, 0.1f)) return false;
            value = v;
            return true;
        }
        if (t == typeof(Vector4))
        {
            Vector4 v = (Vector4)value!;
            if (!ImGui.DragFloat4(label, ref v, 0.1f)) return false;
            value = v;
            return true;
        }
        if (t == typeof(Color))
        {
            Color c = (Color)value!;
            Vector4 v = new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            if (!ImGui.ColorEdit4(label, ref v)) return false;
            value = new Color((byte)(v.X * 255f), (byte)(v.Y * 255f), (byte)(v.Z * 255f), (byte)(v.W * 255f));
            return true;
        }
        if (t.IsEnum)
        {
            string[] names = Enum.GetNames(t);
            int current = Array.IndexOf(names, value!.ToString());
            if (!ImGui.Combo(label, ref current, names, names.Length)) return false;
            value = Enum.Parse(t, names[current]);
            return true;
        }
        if (t == typeof(Guid))
        {
            ImGui.TextDisabled($"{label}: {value}");
            return false;
        }

        // ImpAsset slot: drop a file to reference it, or make a new inline instance
        if (ImpToml.IsAssetType(t))
            return DrawAssetSlot(label, t, ref value, depth);

        // TRef<T> class reference: a dropdown of the concrete subclasses of T
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TRef<>))
            return DrawTypeRef(label, t, ref value);

        // collections: an add/remove list, or a keyed dictionary — each element/value reuses
        // DrawValue recursively, so lists of TRef, structs, assets... all just work.
        if (ImpToml.IsList(t, out _))       return DrawList(label, t, ref value, depth);
        if (ImpToml.IsDict(t, out _, out _)) return DrawDictionary(label, t, ref value, depth);

        // any other struct: cascade into its public fields as a tree
        if (t.IsValueType && !t.IsPrimitive)
        {
            bool changed = false;
            if (ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen))
            {
                // subtle tint flip-flops light/dark per nesting depth so nested
                // struct blocks read as distinct layers
                bool light = depth % 2 == 0;
                ImGui.PushStyleColor(ImGuiCol.ChildBg, light
                    ? new Vector4(1f, 1f, 1f, 0.035f)
                    : new Vector4(0f, 0f, 0f, 0.12f));
                ImGui.PushStyleColor(ImGuiCol.Border, light
                    ? new Vector4(1f, 1f, 1f, 0.10f)
                    : new Vector4(0f, 0f, 0f, 0.25f));

                if (ImGui.BeginChild("struct_body", System.Numerics.Vector2.Zero,
                        ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders))
                {
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (f.IsInitOnly) continue;

                        object? fv = f.GetValue(value);
                        ImGui.PushID(f.Name);
                        if (DrawValue(f.Name, f.FieldType, ref fv, depth + 1))
                        {
                            f.SetValue(value, fv); // mutates the box — caller writes it back up
                            changed = true;
                        }
                        ImGui.PopID();
                    }
                }
                ImGui.EndChild();
                ImGui.PopStyleColor(2);
                ImGui.TreePop();
            }
            return changed;
        }

        ImGui.TextDisabled($"{label} ({t.Name})");
        return false;
    }

    // Godot-style resource slot for an ImpAsset-typed [ImpVar]. The slot is empty, a
    // reference (points at a file on disk, framed blue), or an inline instance (its data
    // saves into the owner's file, framed red). Drop a file from the content browser to
    // reference it, or use the button menu to make a new instance / clear / make-unique.
    static bool DrawAssetSlot(string label, Type slotType, ref object? value, int depth)
    {
        var asset = value as ImpAsset;
        bool changed = false;
        bool open = false;

        // header row: an expander (only when populated) + the label
        if (asset == null)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
        }
        else
        {
            open = ImGui.TreeNodeEx(label,
                ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap |
                ImGuiTreeNodeFlags.FramePadding);
        }

        // summary button — the drag-drop target and the actions-menu opener
        bool isRef = asset is { IsReference: true };
        string summary = asset == null
            ? "(empty)"
            : isRef ? Path.GetFileNameWithoutExtension(asset.file_link)
                    : $"{asset.GetType().Name} (instance)";

        ImGui.SameLine();
        if (asset != null)
            ImGui.PushStyleColor(ImGuiCol.Text, isRef
                ? new Vector4(0.5f, 0.75f, 1f, 1f)
                : new Vector4(1f, 0.55f, 0.55f, 1f));
        bool clicked = ImGui.SmallButton($"{summary}##slot");
        if (asset != null) ImGui.PopStyleColor();

        if (ImGui.BeginDragDropTarget())
        {
            if (EditorDragDrop.AcceptAsset(out string droppedPath))
            {
                var loaded = ImpAsset.LoadFile(droppedPath, slotType);
                if (loaded != null && slotType.IsInstanceOfType(loaded))
                {
                    value = loaded;
                    changed = true;
                }
            }
            ImGui.EndDragDropTarget();
        }

        if (clicked) ImGui.OpenPopup("slot_menu");
        if (ImGui.BeginPopup("slot_menu"))
        {
            if (ImGui.BeginMenu("New Instance"))
            {
                foreach (var ct in ImpToml.ConcreteAssetTypes(slotType))
                    if (ImGui.MenuItem(ct.Name))
                    {
                        value = Activator.CreateInstance(ct);   // file_link "" → instance
                        changed = true;
                    }
                ImGui.EndMenu();
            }
            if (asset != null)
            {
                ImGui.Separator();
                // instance → pick a file and persist it (becomes a reference in place)
                if (!isRef && ImGui.MenuItem("Save To File..."))
                    DLG_SaveAsset.Show(asset);
                if (isRef && ImGui.MenuItem("Save"))            // re-save to its existing file
                    asset.File_Save(asset.file_link);
                if (isRef && ImGui.MenuItem("Save As..."))
                    DLG_SaveAsset.Show(asset);
                if (isRef && ImGui.MenuItem("Make Unique"))     // detach into an editable instance
                {
                    value = CloneAsInstance(asset);
                    changed = true;
                }
                if (ImGui.MenuItem("Clear"))
                {
                    value = null;
                    changed = true;
                }
            }
            ImGui.EndPopup();
        }

        // expanded body — recursively editable, framed by state colour
        if (open)
        {
            asset = value as ImpAsset;   // value may have been replaced by the menu above
            if (asset != null)
            {
                bool refNow = asset.IsReference;
                ImGui.PushStyleColor(ImGuiCol.ChildBg, refNow
                    ? new Vector4(0.30f, 0.55f, 1f, 0.10f)     // light blue — reference
                    : new Vector4(1f, 0.35f, 0.35f, 0.10f));   // light red — instance
                ImGui.PushStyleColor(ImGuiCol.Border, refNow
                    ? new Vector4(0.4f, 0.7f, 1f, 0.6f)
                    : new Vector4(1f, 0.45f, 0.45f, 0.6f));

                if (ImGui.BeginChild("asset_body", Vector2.Zero,
                        ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders))
                {
                    if (refNow) ImGui.TextDisabled(ImpAsset.ToKeywordPath(asset.file_link));
                    if (DrawImpVarFields(asset, depth + 1)) changed = true;
                }
                ImGui.EndChild();
                ImGui.PopStyleColor(2);
            }
            ImGui.TreePop();
        }

        return changed;
    }

    // TSubclassOf-style dropdown for a TRef<T> [ImpVar]. Lists "(none)" plus every concrete
    // subclass of T (via the struct's own static Options()), so only valid classes are pickable.
    // Reflection is used because T is only known at runtime; all the type rules live in TRef<T>.
    static bool DrawTypeRef(string label, Type t, ref object? value)
    {
        var options = ((IEnumerable<Type>)t.GetMethod(nameof(TRef<object>.Options))!
            .Invoke(null, null)!).ToList();

        var names = new string[options.Count + 1];
        names[0] = "(none)";
        for (int i = 0; i < options.Count; i++) names[i + 1] = options[i].Name;

        var current = (Type?)t.GetProperty(nameof(TRef<object>.Type))!.GetValue(value);
        int idx = current == null ? 0 : options.IndexOf(current) + 1;

        if (!ImGui.Combo(label, ref idx, names, names.Length)) return false;

        Type? chosen = idx <= 0 ? null : options[idx - 1];
        value = Activator.CreateInstance(t, new object?[] { chosen });   // new TRef<T>(chosen)
        return true;
    }

    // Editor for any List<T>. Each element is drawn with DrawValue, so the element type decides
    // the widget (TRef dropdown, asset slot, struct tree, primitive...). "+" appends a default
    // element, "x" removes a row. A null list is lazily allocated on first add.
    static bool DrawList(string label, Type t, ref object? value, int depth)
    {
        Type elemType = t.GetGenericArguments()[0];
        var list = value as IList;
        bool changed = false;

        // no SpanAvailWidth: it would stretch the node's hit box across the whole row and swallow
        // clicks meant for the SameLine "+" button. AllowOverlap lets that button win the hover.
        bool open = ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        ImGui.SameLine();
        ImGui.TextDisabled($"[{list?.Count ?? 0}]  {elemType.Name}");
        ImGui.SameLine();
        if (ImGui.SmallButton("+##add"))
        {
            if (list == null) { list = (IList)Activator.CreateInstance(t)!; value = list; }
            list.Add(DefaultElement(elemType));
            changed = true;
        }

        if (open)
        {
            if (list != null)
            {
                int removeAt = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    ImGui.PushID(i);
                    if (ImGui.SmallButton("x")) removeAt = i;
                    ImGui.SameLine();
                    object? elem = list[i];
                    if (DrawValue($"[{i}]", elemType, ref elem, depth + 1))
                    {
                        list[i] = elem;
                        changed = true;
                    }
                    ImGui.PopID();
                }
                if (removeAt >= 0) { list.RemoveAt(removeAt); changed = true; }
            }
            ImGui.TreePop();
        }
        return changed;
    }

    // Editor for any Dictionary<K,V>. Values are drawn with DrawValue; string keys are editable
    // inline (rename = remove+re-add), other key types show read-only. "+" adds an entry with a
    // generated default key, "x" removes it. A null dictionary is lazily allocated on first add.
    static bool DrawDictionary(string label, Type t, ref object? value, int depth)
    {
        Type keyType = t.GetGenericArguments()[0];
        Type valType = t.GetGenericArguments()[1];
        var dict = value as IDictionary;
        bool changed = false;

        // see DrawList: no SpanAvailWidth so the SameLine "+" button stays clickable.
        bool open = ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        ImGui.SameLine();
        ImGui.TextDisabled($"{{{dict?.Count ?? 0}}}  {keyType.Name}→{valType.Name}");
        ImGui.SameLine();
        if (ImGui.SmallButton("+##add"))
        {
            if (dict == null) { dict = (IDictionary)Activator.CreateInstance(t)!; value = dict; }
            object? key = NewKey(keyType, dict);
            if (key != null) { dict[key] = DefaultElement(valType); changed = true; }
        }

        if (open && dict != null)
        {
            // snapshot keys so the dictionary can be mutated (rename/remove) while iterating
            var keys = new List<object>();
            foreach (var k in dict.Keys) keys.Add(k!);

            object? removeKey = null; bool hasRemove = false;
            foreach (var k in keys)
            {
                ImGui.PushID(k.GetHashCode());
                if (ImGui.SmallButton("x")) { removeKey = k; hasRemove = true; }
                ImGui.SameLine();

                // editable string keys (rename in place); everything else is a plain label
                object curKey = k;
                if (keyType == typeof(string))
                {
                    string ks = (string)k;
                    ImGui.SetNextItemWidth(120);
                    if (ImGui.InputText("##key", ref ks, 128) && ks != (string)k && ks.Length > 0 && !dict.Contains(ks))
                    {
                        var v = dict[k];
                        dict.Remove(k);
                        dict[ks] = v;
                        curKey = ks;
                        changed = true;
                    }
                    ImGui.SameLine();
                }

                object? val = dict.Contains(curKey) ? dict[curKey] : null;
                if (DrawValue(keyType == typeof(string) ? "##val" : $"[{curKey}]", valType, ref val, depth + 1))
                {
                    dict[curKey] = val;
                    changed = true;
                }
                ImGui.PopID();
            }
            if (hasRemove) { dict.Remove(removeKey!); changed = true; }
        }
        if (open) ImGui.TreePop();
        return changed;
    }

    // A fresh element/value for a new collection slot: "" for strings, default for value types
    // (structs, TRef, primitives...), null for reference types (asset slots show as empty).
    static object? DefaultElement(Type t) =>
        t == typeof(string) ? "" : t.IsValueType ? Activator.CreateInstance(t) : null;

    // A unique default key for a new dictionary entry, by key kind.
    static object? NewKey(Type keyType, IDictionary dict)
    {
        if (keyType == typeof(string))
        {
            string k = "key"; int n = 0;
            while (dict.Contains(k)) k = $"key{++n}";
            return k;
        }
        if (keyType.IsEnum)
        {
            foreach (var e in Enum.GetValues(keyType))
                if (!dict.Contains(e)) return e;
            return null;   // all enum values already used
        }
        if (keyType == typeof(int) || keyType == typeof(long))
        {
            long i = 0;
            while (dict.Contains(Convert.ChangeType(i, keyType))) i++;
            return Convert.ChangeType(i, keyType);
        }
        return keyType.IsValueType ? Activator.CreateInstance(keyType) : null;
    }

    // Copies an asset's [ImpVar] fields into a fresh, file-less instance — converting a
    // shared on-disk reference into an independently editable embedded instance.
    static ImpAsset CloneAsInstance(ImpAsset src)
    {
        var clone = (ImpAsset)Activator.CreateInstance(src.GetType())!;
        for (var t = src.GetType(); t != null && t != typeof(object); t = t.BaseType)
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            if (f.IsDefined(typeof(ImpVarAttribute), false))
                f.SetValue(clone, f.GetValue(src));
        return clone;   // file_link stays "" → instance
    }
}
