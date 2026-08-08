using System.Numerics;
using ImGuiNET;

namespace Editor.Dialog;

// Confirms recursive folder deletion. Lists every file that will be removed so the user can
// review the damage before committing. Empty folders skip this dialog and delete immediately.
public class DLG_DeleteFolder : EditorDialog
{
    string _folder = "";
    string _title_name = "";
    string[] _files = [];
    int _subdir_count;
    Action? _on_confirm;

    public override string Title => $"Delete Folder — {_title_name}";

    // Shows a confirmation listing all files under `folder`. Invokes `on_confirm` only if the
    // user accepts. Caller is responsible for performing the actual delete.
    public static void Ask(string folder, Action on_confirm)
    {
        string[] files;
        int subdirs = 0;
        try
        {
            files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            subdirs = Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).Length;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeleteFolder] Could not list {folder}: {ex.Message}");
            // still offer a bare confirm so the user can retry / force
            files = [];
        }

        // relativize for display
        string[] display = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            try { display[i] = Path.GetRelativePath(folder, files[i]).Replace('\\', '/'); }
            catch { display[i] = files[i]; }
        }

        new DLG_DeleteFolder
        {
            _folder = folder,
            _title_name = Path.GetFileName(folder),
            _files = display,
            _subdir_count = subdirs,
            _on_confirm = on_confirm,
        }.Show();
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        string name = Path.GetFileName(_folder);
        ImGui.TextWrapped(
            $"Permanently delete folder \"{name}\" and everything inside it? This cannot be undone.");
        ImGui.Spacing();

        if (_files.Length == 0 && _subdir_count == 0)
            ImGui.TextDisabled("(no files found — folder may already be empty)");
        else
        {
            string summary = _files.Length == 1
                ? "1 file will be deleted"
                : $"{_files.Length} files will be deleted";
            if (_subdir_count > 0)
                summary += _subdir_count == 1
                    ? " (plus 1 subfolder)"
                    : $" (plus {_subdir_count} subfolders)";
            ImGui.TextColored(new Vector4(1f, 0.55f, 0.3f, 1f), summary);
        }

        ImGui.Spacing();

        // scrollable file list
        float list_h = Math.Clamp(12 + _files.Length * ImGui.GetTextLineHeightWithSpacing(),
            80f, 320f);
        ImGui.BeginChild("delete_files", new Vector2(420, list_h), ImGuiChildFlags.Borders);
        if (_files.Length == 0)
            ImGui.TextDisabled("(no files)");
        else
            foreach (string f in _files)
                ImGui.TextUnformatted(f);
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.70f, 0.20f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.85f, 0.28f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.55f, 0.12f, 0.10f, 1f));
        if (ImGui.Button("Delete", new Vector2(120, 0)))
        {
            var cb = _on_confirm;
            Dismiss();
            cb?.Invoke();
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120, 0)))
            Dismiss();
    }
}
