using Editor.Panel;
using ImperiumEngine;
using ImperiumEngine.Dialogs;
using ImperiumEngine.Structs;

namespace Editor.Dialog;

// Pick an ImpComp class + name. Creates an ImpScene whose root is that class.
public class DLG_NewScene : Dialog_ClassPicker
{
    string _folder = "";

    public DLG_NewScene()
    {
        title = "New Scene";
        is_create_new = true;
        root_type = typeof(ImpComp);
        on_confirm = _ => Create();
    }

    public static void Run(string folder = null)
    {
        DLG_NewScene dlg = new();
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
        if (type == null || type.IsAbstract || !typeof(ImpComp).IsAssignableFrom(type))
        {
            stay_open = true;
            Hint_Set("Pick a root component class.");
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

        ImpScene scene = new();
        string ext = "." + scene.File_GetExtension();
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

        ImpComp root = Activator.CreateInstance(type) as ImpComp;
        if (root == null)
        {
            stay_open = true;
            Hint_Set("Could not create that class.");
            return;
        }

        root.name = name;
        scene.root = root;
        scene.root_type = new TClass<ImpComp>(type);
        if (!scene.File_SaveTo(path))
        {
            stay_open = true;
            Hint_Set("Could not write the file.");
            return;
        }

        PNL_FileBrowser.Browsers_Notify();
        ImpAsset.Editor_OnOpenAsset?.Invoke(scene);
    }
}
