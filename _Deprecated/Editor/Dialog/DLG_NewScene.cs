using ImperiumEngine;
using ImperiumEngine.Comps._1D.Dialog;

namespace Editor.Dialog;

//Dialog to create a new scene. NOTE: though you select an ImpComp, you create an ImpAsset of type "ImpScene" with that ImpComp as its root TYE
public class DLG_NewScene : Dialog_ClassPicker
{
    public DLG_NewScene()
    {
        is_create_new=true;
        root_type=typeof(ImpComp);
    }
}