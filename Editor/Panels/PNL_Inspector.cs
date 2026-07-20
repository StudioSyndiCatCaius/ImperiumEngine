using System.Numerics;
using System.Reflection;
using ImGuiNET;
using ImperiumEngine;
using Raylib_cs;

namespace Editor.Panels;

// a panel that displays the property inspector & editor for the selected objects
public class PNL_Inspector : EditorPanel
{
    public List<object> selected_objects = new();

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        if (selected_objects.Count == 0)
        {
            ImGui.TextDisabled("Nothing selected");
            return;
        }

        foreach (var obj in selected_objects)
        {
            ImGui.PushID(obj.GetHashCode());
            ImGui.SeparatorText(obj.GetType().Name);
            DrawObject(obj);
            ImGui.PopID();
        }
    }

    void DrawObject(object obj)
    {
        // walk the type chain root-first so base fields (guid, is_visible...) come first.
        // only [ImpVar] fields are exposed at the object level — inside structs,
        // every public field is fair game (structs are plain data)
        var chain = new List<Type>();
        for (var t = obj.GetType(); t != null && t != typeof(object); t = t.BaseType)
            chain.Insert(0, t);

        foreach (var type in chain)
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!field.IsDefined(typeof(ImpVarAttribute), false)) continue;

            object? value = field.GetValue(obj);
            ImGui.PushID(field.Name);
            if (DrawValue(field.Name, field.FieldType, ref value))
                field.SetValue(obj, value);
            ImGui.PopID();
        }
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
}
