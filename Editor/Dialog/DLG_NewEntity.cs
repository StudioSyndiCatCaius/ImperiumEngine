using System.Numerics;
using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace Editor.Dialog;

// Creates a new entity asset (A_Entity) — a reusable preset that behaves like a component whose
// root type the user picks here. The picker is the same addable-component tree used when adding
// components to a level (via DLG_SelectComponent's shared type list); the chosen type becomes the
// entity's parent_type and its single root component. The name is validated exactly like
// DLG_NewAsset before the file is written.
public class DLG_NewEntity : EditorDialog
{
    string _folder = "";        // absolute target folder the new entity is created in
    string _filter = "";
    string _name = "";
    string _error = "";
    TypeTree _tree = null!;
    Action<string>? _on_created;

    public override string Title => "New Entity";

    // Opens the root-type picker for `folder`. On success `on_created` receives the absolute path
    // of the new file.
    public static void Show(string folder, Action<string>? on_created = null)
    {
        new DLG_NewEntity
        {
            _folder = folder,
            _on_created = on_created,
            _tree = new TypeTree(DLG_SelectComponent.AddableTypes(), typeof(ImpComponent)),
        }.Show();
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        var prev = _tree.SelectedType;
        bool commit = _tree.Draw(ref _filter, new Vector2(360, 320));
        // picking a type defaults the name to it, unless the user has typed their own
        if (_tree.SelectedType != prev && _tree.SelectedType != null &&
            (_name.Length == 0 || (prev != null && _name == prev.Name)))
            _name = _tree.SelectedType.Name;

        var type = _tree.SelectedType;
        ImGui.TextDisabled(type != null ? $"Root: {type.Name}" : "Select a root component type.");

        ImGui.SetNextItemWidth(300);
        if (ImGui.InputText("##name", ref _name, 128)) _error = "";
        ImGui.SameLine(0, 4);
        ImGui.TextDisabled(EntityExt);

        if (type != null)
            ImGui.TextDisabled($"→ {_name.Trim()}{EntityExt}");
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
        var type = _tree.SelectedType;
        if (type == null) { _error = "Select a root component type."; return; }

        string name = _name.Trim();
        if (name.Length == 0) { _error = "Enter a name."; return; }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { _error = "Invalid characters in name."; return; }

        var entity = new A_Entity { parent_type = type.Name };
        entity.components.Add((ImpComponent)Activator.CreateInstance(type)!);   // the root component

        string full = Path.Combine(_folder, name + entity.GetExtension());
        if (File.Exists(full)) { _error = "A file with that name already exists."; return; }

        if (!entity.File_Save(full)) { _error = "Failed to write file (see log)."; return; }

        var cb = _on_created;
        Dismiss();
        cb?.Invoke(full);
    }

    static string EntityExt => new A_Entity().GetExtension();
}
