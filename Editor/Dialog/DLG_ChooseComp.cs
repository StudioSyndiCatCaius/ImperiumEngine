using ImperiumEngine;
using ImperiumEngine.Dialogs;

namespace Editor.Dialog;

public class DLG_ChooseComp : Dialog_ClassPicker
{
    public DLG_ChooseComp()
    {
        title = "Choose Component";
        root_type = typeof(ImpComp);
    }

    public static void Run(Action<Type> on_picked, string title = "Choose Component")
    {
        DLG_ChooseComp dlg = new();
        dlg.title = title;
        dlg.on_confirm = d =>
        {
            if (d.selected_type != null)
            {
                on_picked?.Invoke(d.selected_type);
            }
        };
        dlg.Show();
    }
}
