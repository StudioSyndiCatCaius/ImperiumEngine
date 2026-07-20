using System.Numerics;
using ImGuiNET;
using ImperiumEngine.Classes;

namespace Editor.Dialog;

// Picks an on-disk location for an ImpAsset and writes it there via File_Save — the flow
// used to turn an inline instance into a shared, referenceable asset file. Navigation is
// constrained to the project's Content root; the asset's own extension is enforced; and
// saving over an existing file asks for confirmation (inline, since ImGui can't stack two
// independent top-level modals).
public class DLG_SaveAsset : EditorDialog
{
    ImpAsset _asset = null!;
    string _root = "";          // absolute Content root — navigation can't escape it
    string _rel = "";           // current folder, relative to _root
    string _filename = "";      // without extension
    string _ext = ".impasset";
    string _error = "";
    string _confirm_path = "";  // when set, the inline "overwrite?" prompt is shown
    Action<string>? _on_saved;

    public override string Title => "Save Asset";

    string CurrentDir => Path.Combine(_root, _rel);

    // Queues the save dialog for `asset`. On success `on_saved` receives the absolute path
    // (the asset's file_link is now set, so it has become a reference).
    public static void Show(ImpAsset asset, string? start_rel = null, Action<string>? on_saved = null)
    {
        string root = Path.Combine(ImpAsset.s_projectDir, "Content");
        new DLG_SaveAsset
        {
            _asset = asset,
            _root = root,
            _rel = start_rel != null && Directory.Exists(Path.Combine(root, start_rel)) ? start_rel : "",
            _ext = asset.GetExtension(),
            _filename = DefaultName(asset),
            _on_saved = on_saved,
        }.Show();
    }

    static string DefaultName(ImpAsset asset)
    {
        string n = asset.GetType().Name;
        return n.StartsWith("A_") ? n[2..] : n;   // A_MoveMode -> MoveMode
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        // second stage: confirm overwriting an existing file
        if (_confirm_path != "")
        {
            DrawOverwritePrompt();
            return;
        }

        ImGui.Text($"Saving {_asset.GetType().Name}");
        ImGui.Separator();

        DrawBreadcrumb();
        DrawBrowser();

        ImGui.SetNextItemWidth(300);
        ImGui.InputText("##filename", ref _filename, 128);
        ImGui.SameLine(0, 4);
        ImGui.TextDisabled(_ext);

        ImGui.TextDisabled($"→ {Path.Combine("Content", _rel, TrimName(_filename) + _ext)}");

        if (_error != "")
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _error);

        ImGui.Separator();

        bool valid = TrimName(_filename).Length > 0;
        ImGui.BeginDisabled(!valid);
        if (ImGui.Button("Save", new Vector2(120, 0)))
            TrySave();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120, 0)))
            Dismiss();
    }

    // --- folder navigation ---

    void DrawBreadcrumb()
    {
        if (ImGui.Button("Content")) _rel = "";
        string walk = "";
        foreach (string seg in _rel.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            walk = Path.Combine(walk, seg);
            ImGui.SameLine(0, 4); ImGui.TextDisabled(">"); ImGui.SameLine(0, 4);
            string target = walk;   // capture before the loop advances
            if (ImGui.Button(seg)) _rel = target;
        }
    }

    void DrawBrowser()
    {
        ImGui.BeginChild("browser", new Vector2(420, 220), ImGuiChildFlags.Borders);

        foreach (string dir in SafeDirs(CurrentDir))
        {
            string name = Path.GetFileName(dir);
            if (ImGui.Selectable($"[ ] {name}", false, ImGuiSelectableFlags.AllowDoubleClick) &&
                ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                _rel = Path.Combine(_rel, name);
        }

        // only files this asset type could overwrite are relevant
        foreach (string file in SafeFiles(CurrentDir))
        {
            if (!file.EndsWith(_ext, StringComparison.OrdinalIgnoreCase)) continue;
            string name = Path.GetFileName(file);
            bool selected = TrimName(_filename) + _ext == name;
            if (ImGui.Selectable(name, selected))
                _filename = Path.GetFileNameWithoutExtension(name);   // reuse an existing name
        }

        ImGui.EndChild();
    }

    // --- saving ---

    void TrySave()
    {
        _error = "";
        string name = TrimName(_filename);
        if (name.Length == 0) { _error = "Enter a file name."; return; }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { _error = "Invalid characters in file name."; return; }

        string full = Path.Combine(CurrentDir, name + _ext);
        if (File.Exists(full)) { _confirm_path = full; return; }   // → overwrite prompt
        DoSave(full);
    }

    void DoSave(string full)
    {
        if (!_asset.File_Save(full))   // sets file_link → the asset is now a reference
        {
            _error = "Failed to write file (see log).";
            _confirm_path = "";
            return;
        }
        _asset.is_dirty = false;
        var cb = _on_saved;
        Dismiss();
        cb?.Invoke(full);
    }

    void DrawOverwritePrompt()
    {
        ImGui.TextWrapped($"'{Path.GetFileName(_confirm_path)}' already exists. Overwrite it?");
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

        if (ImGui.Button("Overwrite", new Vector2(120, 0)))
            DoSave(_confirm_path);
        ImGui.SameLine();
        if (ImGui.Button("Back", new Vector2(120, 0)))
            _confirm_path = "";
    }

    // strips a user-typed extension and surrounding whitespace so we don't double it up
    string TrimName(string raw)
    {
        raw = raw.Trim();
        if (raw.EndsWith(_ext, StringComparison.OrdinalIgnoreCase))
            raw = raw[..^_ext.Length];
        return raw;
    }

    static string[] SafeDirs(string dir)
    {
        try { return Directory.GetDirectories(dir).OrderBy(Path.GetFileName).ToArray(); }
        catch { return []; }
    }

    static string[] SafeFiles(string dir)
    {
        try { return Directory.GetFiles(dir).OrderBy(Path.GetFileName).ToArray(); }
        catch { return []; }
    }
}
