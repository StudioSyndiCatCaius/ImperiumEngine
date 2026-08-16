using ImperiumEngine.Dialogs;

namespace Editor.Dialog;

public class DLG_ConfirmDelete : Dialog_Confirm
{
    public new static void Run(string message, Action on_yes, Action on_no = null)
    {
        DLG_ConfirmDelete dlg = new()
        {
            message = message,
            text_yes = "Delete",
            text_no = "Cancel",
            on_yes = on_yes,
            on_no = on_no,
        };
        dlg.Show();
    }
}