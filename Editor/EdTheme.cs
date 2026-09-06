using System.Globalization;
using System.Numerics;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;

namespace Editor;

public static class EdTheme
{
    public static void Apply(string path = "{engine}/Themes/default.toml")
    {
        string abs = GFile.Make_Path_Absolute(path);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
        {
            GLog.Warning("ImGui theme not found: " + path);
            return;
        }

        TTable tbl;
        try { tbl = TTable.FromTOML(File.ReadAllText(abs)); }
        catch (Exception e)
        {
            GLog.Warning("ImGui theme parse failed: " + e.Message);
            return;
        }

        ImGuiStylePtr s = ImGui.GetStyle();
        foreach (var pair in tbl.data)
        {
            string key = pair.Key.ToString();
            if (key.Equals("colors", StringComparison.OrdinalIgnoreCase) && pair.Value is TTable cols)
            {
                foreach (var c in cols.data)
                {
                    if (!Enum.TryParse(c.Key.ToString(), out ImGuiCol col)) continue;
                    if (!ColorOf(c.Value, out Vector4 v)) continue;
                    s.Colors[(int)col] = v;
                }
                continue;
            }
            ApplyVar(s, key, pair.Value);
        }
    }

    static void ApplyVar(ImGuiStylePtr s, string key, object? val)
    {
        switch (key)
        {
            case "alpha": s.Alpha = F(val); break;
            case "disabledAlpha": s.DisabledAlpha = F(val); break;
            case "windowPadding": s.WindowPadding = V2(val); break;
            case "windowRounding": s.WindowRounding = F(val); break;
            case "windowBorderSize": s.WindowBorderSize = F(val); break;
            case "windowMinSize": s.WindowMinSize = V2(val); break;
            case "windowTitleAlign": s.WindowTitleAlign = V2(val); break;
            case "windowMenuButtonPosition": s.WindowMenuButtonPosition = Dir(val); break;
            case "childRounding": s.ChildRounding = F(val); break;
            case "childBorderSize": s.ChildBorderSize = F(val); break;
            case "popupRounding": s.PopupRounding = F(val); break;
            case "popupBorderSize": s.PopupBorderSize = F(val); break;
            case "framePadding": s.FramePadding = V2(val); break;
            case "frameRounding": s.FrameRounding = F(val); break;
            case "frameBorderSize": s.FrameBorderSize = F(val); break;
            case "itemSpacing": s.ItemSpacing = V2(val); break;
            case "itemInnerSpacing": s.ItemInnerSpacing = V2(val); break;
            case "cellPadding": s.CellPadding = V2(val); break;
            case "indentSpacing": s.IndentSpacing = F(val); break;
            case "columnsMinSpacing": s.ColumnsMinSpacing = F(val); break;
            case "scrollbarSize": s.ScrollbarSize = F(val); break;
            case "scrollbarRounding": s.ScrollbarRounding = F(val); break;
            case "grabMinSize": s.GrabMinSize = F(val); break;
            case "grabRounding": s.GrabRounding = F(val); break;
            case "tabRounding": s.TabRounding = F(val); break;
            case "tabBorderSize": s.TabBorderSize = F(val); break;
            case "tabMinWidthForCloseButton": s.TabMinWidthForCloseButton = F(val); break;
            case "colorButtonPosition": s.ColorButtonPosition = Dir(val); break;
            case "buttonTextAlign": s.ButtonTextAlign = V2(val); break;
            case "selectableTextAlign": s.SelectableTextAlign = V2(val); break;
        }
    }

    static float F(object? v)
    {
        if (v is float f) return f;
        if (v is double d) return (float)d;
        if (v is int i) return i;
        if (v is long l) return l;
        if (v is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float p))
            return p;
        return 0f;
    }

    static Vector2 V2(object? v)
    {
        if (v is List<object> list && list.Count >= 2)
            return new Vector2(F(list[0]), F(list[1]));
        float n = F(v);
        return new Vector2(n, n);
    }

    static ImGuiDir Dir(object? v)
    {
        string n = v?.ToString() ?? "";
        if (n.Equals("Right", StringComparison.OrdinalIgnoreCase)) return ImGuiDir.Right;
        if (n.Equals("Up", StringComparison.OrdinalIgnoreCase)) return ImGuiDir.Up;
        if (n.Equals("Down", StringComparison.OrdinalIgnoreCase)) return ImGuiDir.Down;
        if (n.Equals("None", StringComparison.OrdinalIgnoreCase)) return ImGuiDir.None;
        return ImGuiDir.Left;
    }

    static bool ColorOf(object? v, out Vector4 c)
    {
        c = default;
        string s = v?.ToString() ?? "";
        int open = s.IndexOf('(');
        int close = s.LastIndexOf(')');
        if (open < 0 || close <= open) return false;
        string[] p = s[(open + 1)..close].Split(',');
        if (p.Length < 3) return false;
        if (!float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r)) return false;
        if (!float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float g)) return false;
        if (!float.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float b)) return false;
        float a = 1f;
        if (p.Length >= 4)
            float.TryParse(p[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a);
        c = new Vector4(r / 255f, g / 255f, b / 255f, a);
        return true;
    }
}
