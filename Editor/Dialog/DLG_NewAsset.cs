using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ImperiumEngine.Classes;

namespace Editor.Dialog;

// Picks a concrete ImpAsset type from a tree mirroring the class hierarchy (with a search box up
// top), then creates a fresh instance of it as a new file in a target folder. The chosen name is
// validated — non-empty, legal file-name characters, and not already taken — before the asset is
// written via File_Save.
//
// A "fixed" variant (ShowFixed) skips the picker and only asks for a name; it backs the New Level
// menu item, which always creates an A_Level.
public class DLG_NewAsset : EditorDialog
{
    string _folder = "";        // absolute target folder the new asset is created in
    string _title = "New Asset";
    string _filter = "";
    string _name = "";
    string _error = "";
    Type? _fixed;               // when set, the picker is hidden and this type is always used
    TypeTree? _tree;
    Action<string>? _on_created;

    public override string Title => _title;

    Type? SelectedType => _fixed ?? _tree?.SelectedType;

    // Opens the full asset-type picker for `folder`. On success `on_created` receives the absolute
    // path of the new file.
    public static void Show(string folder, Action<string>? on_created = null)
    {
        new DLG_NewAsset
        {
            _folder = folder,
            _on_created = on_created,
            _tree = new TypeTree(ImpToml.ConcreteAssetTypes(typeof(ImpAsset)), typeof(ImpAsset)),
        }.Show();
    }

    // Name-only variant: the type is fixed, so no picker is shown (used by "New Level").
    public static void ShowFixed(string folder, Type type, string title, Action<string>? on_created = null)
    {
        new DLG_NewAsset
        {
            _folder = folder,
            _fixed = type,
            _title = title,
            _name = DefaultName(type),
            _on_created = on_created,
        }.Show();
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        bool commit = false;

        if (_tree != null)
        {
            var prev = _tree.SelectedType;
            commit = _tree.Draw(ref _filter, new Vector2(360, 320));
            // picking a type defaults the name to it, unless the user has typed their own
            if (_tree.SelectedType != prev && _tree.SelectedType != null &&
                (_name.Length == 0 || (prev != null && _name == DefaultName(prev))))
                _name = DefaultName(_tree.SelectedType);
        }

        var type = SelectedType;
        ImGui.TextDisabled(type != null ? $"Type: {type.Name}" : "Select an asset type.");

        ImGui.SetNextItemWidth(300);
        if (ImGui.InputText("##name", ref _name, 128)) _error = "";
        ImGui.SameLine(0, 4);
        ImGui.TextDisabled(type != null ? ExtensionOf(type) : "");

        if (type != null)
            ImGui.TextDisabled($"→ {_name.Trim()}{ExtensionOf(type)}");
        if (_error != "")
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _error);

        ImGui.Separator();

        bool valid = type != null && _name.Trim().Length > 0;
        ImGui.BeginDisabled(!valid);
        if (ImGui.Button("Create", new Vector2(120, 0)) || (valid && commit))
            TryCreate();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120, 0)))
            Dismiss();
    }

    void TryCreate()
    {
        _error = "";
        var type = SelectedType;
        if (type == null) { _error = "Select an asset type."; return; }

        string name = _name.Trim();
        if (name.Length == 0) { _error = "Enter a name."; return; }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { _error = "Invalid characters in name."; return; }

        var asset = (ImpAsset)Activator.CreateInstance(type)!;   // ctor runs here, once
        string full = Path.Combine(_folder, name + asset.GetExtension());
        if (File.Exists(full)) { _error = "A file with that name already exists."; return; }

        if (!asset.File_Save(full)) { _error = "Failed to write file (see log)."; return; }

        var cb = _on_created;
        Dismiss();
        cb?.Invoke(full);
    }

    // A_MoveMode -> MoveMode (drop the A_ prefix for the default file name)
    static string DefaultName(Type t)
    {
        string n = t.Name;
        return n.StartsWith("A_") ? n[2..] : n;
    }

    // Reads Editor_GetExtension() without constructing the type — some asset ctors touch the GPU /
    // load resources, so an uninitialized instance is enough to dispatch the override for a preview.
    static readonly MethodInfo? s_get_ext = typeof(ImpAsset)
        .GetMethod("Editor_GetExtension", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    static string ExtensionOf(Type t)
    {
        if (s_get_ext == null) return ".impasset";
        try { return (string)s_get_ext.Invoke(RuntimeHelpers.GetUninitializedObject(t), null)!; }
        catch { return ".impasset"; }
    }
}
