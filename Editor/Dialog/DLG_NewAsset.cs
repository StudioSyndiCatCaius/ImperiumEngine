using Editor.Panel;
using ImperiumEngine;
using ImperiumEngine.Dialogs;

namespace Editor.Dialog;

// Pick an ImpAsset class + name. Writes a new asset file of that type.
public class DLG_NewAsset : Dialog_ClassPicker
{
    string _folder = "";

    public DLG_NewAsset()
    {
        title = "New Asset";
        is_create_new = true;
        root_type = typeof(ImpAsset);
        on_confirm = _ => Create();
    }

    public static void Run(string folder = null)
    {
        DLG_NewAsset dlg = new();
        dlg._folder = folder;
        if (dlg._folder == null)
        {
            dlg._folder = "";
        }
        dlg.Show();
    }

    void Create()
    {
        Type type = selected_type;
        if (type == null || type.IsAbstract || !typeof(ImpAsset).IsAssignableFrom(type))
        {
            stay_open = true;
            Hint_Set("Pick an asset class.");
            return;
        }

        string name = (txtedit_create_name.text ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            stay_open = true;
            Hint_Set("Name is required.");
            return;
        }

        string folder = PNL_FileBrowser.Folder_ForCreate(_folder);
        if (string.IsNullOrEmpty(folder))
        {
            stay_open = true;
            Hint_Set("No Content folder to write to.");
            return;
        }

        ImpAsset asset = Activator.CreateInstance(type) as ImpAsset;
        if (asset == null)
        {
            stay_open = true;
            Hint_Set("Could not create that class.");
            return;
        }

        string ext = "." + asset.File_GetExtension();
        if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - ext.Length);
        }
        if (string.IsNullOrEmpty(name))
        {
            stay_open = true;
            Hint_Set("Name is required.");
            return;
        }

        string path;
        try
        {
            path = Path.GetFullPath(Path.Combine(folder, name + ext));
        }
        catch
        {
            stay_open = true;
            Hint_Set("Path is not valid.");
            return;
        }

        if (File.Exists(path))
        {
            stay_open = true;
            Hint_Set("A file with that name already exists.");
            return;
        }

        if (!asset.File_SaveTo(path))
        {
            stay_open = true;
            Hint_Set("Could not write the file.");
            return;
        }

        PNL_FileBrowser.Browsers_Notify();
        ImpAsset.Editor_OnOpenAsset?.Invoke(asset);
    }
}
