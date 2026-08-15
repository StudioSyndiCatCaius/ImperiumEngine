using ImperiumEngine;
using ImperiumEngine.Comps._1D.Dialog;

namespace Editor.Dialog;

public class DLG_ChooseComp : Dialog_ClassPicker
{
    public DLG_ChooseComp()
    {
        title = "Choose Component";
        root_type = typeof(ImpComp);
    }
}
