using System.Numerics;
using Engine.Globals;
using ImGuiNET;
using Raylib_cs;

namespace Editor;

public static class EdIcons
{
    static readonly Dictionary<string, Texture2D> _main = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Texture2D> _enums = new(StringComparer.OrdinalIgnoreCase);
    static bool _loaded;

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        Scan("{engine}/Editor/MainButtons", _main);
        Scan("{engine}/Editor/Enums", _enums);
    }

    public static Texture2D Main(string name)
    {
        Load();
        if (string.IsNullOrEmpty(name)) return default;
        if (_main.TryGetValue(name, out Texture2D t) && t.Id != 0) return t;
        if (_main.TryGetValue("ico_editor_" + name, out t) && t.Id != 0) return t;
        int us = name.LastIndexOf('_');
        if (us > 0) return Main(name[..us]);
        return default;
    }

    public static Texture2D Enum(System.Enum value)
    {
        Load();
        if (value == null) return default;
        string key = value.GetType().Name + "." + value;
        if (_enums.TryGetValue(key, out Texture2D t)) return t;
        return default;
    }

    public static bool Button(string id, string label, Texture2D ico, bool selected = false, bool show_label = false)
    {
        bool has_label = show_label && !string.IsNullOrEmpty(label);
        if (ico.Id == 0)
        {
            if (selected)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
            bool text_hit = ImGui.Button(label);
            if (selected) ImGui.PopStyleColor();
            return text_hit;
        }

        Vector2 pad = ImGui.GetStyle().FramePadding;
        float inner = ImGui.GetTextLineHeight();
        float gap = ImGui.GetStyle().ItemInnerSpacing.X;
        Vector2 text_sz = has_label ? ImGui.CalcTextSize(label) : Vector2.Zero;
        Vector2 size = new(inner + pad.X * 2f + (has_label ? gap + text_sz.X : 0f), ImGui.GetFrameHeight());
        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
        bool hit = ImGui.Button(id, size);
        if (selected) ImGui.PopStyleColor();
        Vector2 min = ImGui.GetItemRectMin() + pad;
        Vector2 ico_max = new(min.X + inner, ImGui.GetItemRectMax().Y - pad.Y);
        if (ico_max.X > min.X && ico_max.Y > min.Y)
            ImGui.GetWindowDrawList().AddImage((IntPtr)ico.Id, min, ico_max);
        if (has_label)
        {
            Vector2 tp = new(min.X + inner + gap, ImGui.GetItemRectMin().Y + (ImGui.GetFrameHeight() - text_sz.Y) * 0.5f);
            ImGui.GetWindowDrawList().AddText(tp, ImGui.GetColorU32(ImGuiCol.Text), label);
        }
        else if (!string.IsNullOrEmpty(label) && ImGui.IsItemHovered())
            ImGui.SetTooltip(label);
        return hit;
    }

    static void Scan(string local_dir, Dictionary<string, Texture2D> dest)
    {
        string abs = GFile.Make_Path_Absolute(local_dir);
        if (string.IsNullOrEmpty(abs) || !Directory.Exists(abs)) return;
        foreach (string file in Directory.GetFiles(abs, "*.png"))
        {
            string key = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(key) || dest.ContainsKey(key)) continue;
            try { dest[key] = Raylib.LoadTexture(file); }
            catch { dest[key] = default; }
        }
    }
}
