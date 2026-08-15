using ImperiumEngine;
using ImperiumEngine.Comps._1D.Dialog;
using ImperiumEngine.Comps._2D;

namespace Editor.Dialog;

public class DLG_NewAsset : Dialog_ClassPicker
{
    public DLG_NewAsset()
    {
        is_create_new=true;
        root_type=typeof(ImpAsset);
    }
}