using System.Numerics;
using System.Reflection;
using Editor.UI;
using Engine;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Panels;

public class PNL_Inspector : EdPanel
{
    public EUI_SearchBar property_search = new();
    
    public object? selected_object = null;

    static readonly uint ColX = ImGui.ColorConvertFloat4ToU32(new Vector4(0.86f, 0.24f, 0.24f, 1f));
    static readonly uint ColY = ImGui.ColorConvertFloat4ToU32(new Vector4(0.24f, 0.72f, 0.28f, 1f));
    static readonly uint ColZ = ImGui.ColorConvertFloat4ToU32(new Vector4(0.26f, 0.46f, 0.90f, 1f));
    static readonly uint ColW = ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.75f, 0.20f, 1f));
    static readonly uint[] AxisCols = { ColX, ColY, ColZ, ColW };

    public override void OnDraw()
    {
        base.OnDraw();

        if (selected_object == null)
        {
            ImGui.TextDisabled("Nothing selected");
            return;
        }

        ImGui.SeparatorText(selected_object.GetType().Name);

        ImGuiTableFlags flags =
            ImGuiTableFlags.BordersInnerV |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.SizingStretchProp;

        if (!ImGui.BeginTable("##inspector", 2, flags))
            return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

        // root-first so base [ImpVar] fields appear above derived ones
        List<Type> chain = new();
        for (Type? t = selected_object.GetType(); t != null && t != typeof(object); t = t.BaseType)
            chain.Insert(0, t);

        foreach (Type type in chain)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                ImpVarAttribute? attr = field.GetCustomAttribute<ImpVarAttribute>(false);
                if (attr == null || attr.Hidden) continue;

                object? value = field.GetValue(selected_object);
                string label = attr.Name ?? field.GetCustomAttribute<TitleAttribute>()?.Name ?? field.Name;

                ImGui.PushID(field.Name);
                if (DrawRow(label, field.FieldType, ref value))
                    field.SetValue(selected_object, value);
                ImGui.PopID();
            }

            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                ImpVarAttribute? attr = prop.GetCustomAttribute<ImpVarAttribute>(false);
                if (attr == null || attr.Hidden) continue;

                object? value = prop.GetValue(selected_object);
                string label = attr.Name ?? prop.GetCustomAttribute<TitleAttribute>()?.Name ?? prop.Name;

                ImGui.PushID(prop.Name);
                if (DrawRow(label, prop.PropertyType, ref value))
                    prop.SetValue(selected_object, value);
                ImGui.PopID();
            }
        }

        ImGui.EndTable();
    }

    static bool DrawRow(string label, Type t, ref object? value)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(-1);
        return DrawEditor(t, ref value);
    }

    static bool DrawEditor(Type t, ref object? value)
    {
        if (t == typeof(int))
        {
            int v = (int)value!;
            if (!ImGui.DragInt("##v", ref v)) return false;
            value = v;
            return true;
        }

        if (t == typeof(float))
        {
            float v = (float)value!;
            if (!ImGui.DragFloat("##v", ref v, 0.1f)) return false;
            value = v;
            return true;
        }

        if (t == typeof(bool))
        {
            bool v = (bool)value!;
            if (!ImGui.Checkbox("##v", ref v)) return false;
            value = v;
            return true;
        }

        if (t == typeof(string))
        {
            string v = (string?)value ?? "";
            if (!ImGui.InputText("##v", ref v, 512)) return false;
            value = v;
            return true;
        }

        if (t == typeof(Color))
        {
            Color c = (Color)value!;
            Vector4 v = new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            if (!ImGui.ColorEdit4("##v", ref v)) return false;
            value = new Color(
                (byte)(v.X * 255f),
                (byte)(v.Y * 255f),
                (byte)(v.Z * 255f),
                (byte)(v.W * 255f));
            return true;
        }

        if (t == typeof(Vector2))
        {
            Vector2 v = (Vector2)value!;
            Span<float> axes = stackalloc float[] { v.X, v.Y };
            if (!DragAxes(axes)) return false;
            value = new Vector2(axes[0], axes[1]);
            return true;
        }

        if (t == typeof(Vector3))
        {
            Vector3 v = (Vector3)value!;
            Span<float> axes = stackalloc float[] { v.X, v.Y, v.Z };
            if (!DragAxes(axes)) return false;
            value = new Vector3(axes[0], axes[1], axes[2]);
            return true;
        }

        if (t == typeof(Vector4))
        {
            Vector4 v = (Vector4)value!;
            Span<float> axes = stackalloc float[] { v.X, v.Y, v.Z, v.W };
            if (!DragAxes(axes)) return false;
            value = new Vector4(axes[0], axes[1], axes[2], axes[3]);
            return true;
        }

        ImGui.TextDisabled(t.Name);
        return false;
    }

    // Unity-style axis drags with a colored strip on the left edge of each field.
    static bool DragAxes(Span<float> axes)
    {
        int count = axes.Length;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float total = ImGui.GetContentRegionAvail().X;
        float width = (total - spacing * (count - 1)) / count;
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            if (i > 0) ImGui.SameLine();
            ImGui.PushID(i);
            ImGui.SetNextItemWidth(width);

            float slot = axes[i];
            if (ImGui.DragFloat("##a", ref slot, 0.1f))
            {
                axes[i] = slot;
                changed = true;
            }

            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRectFilled(min, new Vector2(min.X + 3f, max.Y), AxisCols[i]);

            ImGui.PopID();
        }

        return changed;
    }
}
