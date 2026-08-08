using System.Numerics;
using ImGuiNET;
using ImperiumEngine.Classes;
using Raylib_cs;

namespace Editor;

// Autoloads type icons from the engine content pack by simple type name:
//   {engine}/icons/components/{TypeName}.png
//   {engine}/2D/icons/components/{TypeName}.png   (shipped fallback)
// Walks the inheritance chain so C3_Mesh can fall back to ImpComponent3D / ImpComponent, etc.
// Used by the scene outliner and the component / asset type pickers.
public static class EditorIcons
{
    static readonly Dictionary<string, Texture2D> s_by_name = new(StringComparer.OrdinalIgnoreCase);
    static bool s_scanned;

    public static float SlotSize => ImGui.GetTextLineHeight();

    // Leading spaces so TreeNodeEx / Selectable label text clears the painted icon.
    public static string LabelPad => "   ";

    public static Texture2D? Get(Type? type)
    {
        if (type == null) return null;
        EnsureScanned();

        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            if (s_by_name.TryGetValue(t.Name, out var tex) && tex.Id != 0)
                return tex;
            if (TryLoad(t.Name, out tex))
                return tex;
        }
        return null;
    }

    // Call immediately after TreeNodeEx / Selectable whose label used LabelPad.
    // `tree` true → icon sits after the tree arrow; false → left edge of a flat row.
    public static void DrawOnLastItem(Type? type, bool tree = true)
    {
        var tex = Get(type);
        if (tex is not { Id: not 0 } t) return;

        var min = ImGui.GetItemRectMin();
        var size = ImGui.GetItemRectSize();
        float sz = MathF.Min(SlotSize, MathF.Max(4f, size.Y - 2f));
        float x = tree ? min.X + ImGui.GetTreeNodeToLabelSpacing() : min.X + 2f;
        float y = min.Y + (size.Y - sz) * 0.5f;

        ImGui.GetWindowDrawList().AddImage(
            (IntPtr)t.Id,
            new Vector2(x, y),
            new Vector2(x + sz, y + sz));
    }

    static void EnsureScanned()
    {
        if (s_scanned) return;
        s_scanned = true;

        foreach (var dir in IconDirs())
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (s_by_name.ContainsKey(name)) continue;
                TryLoadPath(name, file);
            }
        }
    }

    static IEnumerable<string> IconDirs()
    {
        string root = ImpFile.s_engineContentDir;
        if (string.IsNullOrEmpty(root)) yield break;
        yield return Path.Combine(root, "icons", "components");
        yield return Path.Combine(root, "2D", "icons", "components");
    }

    static bool TryLoad(string typeName, out Texture2D tex)
    {
        tex = default;
        if (s_by_name.TryGetValue(typeName, out tex))
            return tex.Id != 0;

        foreach (var dir in IconDirs())
        {
            string path = Path.Combine(dir, typeName + ".png");
            if (File.Exists(path) && TryLoadPath(typeName, path))
            {
                tex = s_by_name[typeName];
                return true;
            }
        }

        s_by_name[typeName] = default;
        return false;
    }

    static bool TryLoadPath(string typeName, string path)
    {
        try
        {
            var tex = Raylib.LoadTexture(path);
            if (tex.Id == 0) return false;
            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            s_by_name[typeName] = tex;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
