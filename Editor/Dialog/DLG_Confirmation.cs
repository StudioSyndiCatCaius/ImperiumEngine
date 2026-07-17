using ImperiumEngine.Structs;

namespace Editor.Dialog;

public class DLG_Confirmation : EditorDialog
{
    TText message;
    
    TText text_yes=new TText("Yes");
    TText text_no=new TText("No");
}