using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Structs;

namespace ImperiumEditor.Config;

public class CFG_ED_Launch : EditorConfig
{
    [ImpVar][Exposed][Category("Flags")] public bool autoloadLastProject = false;
    [ImpVar][Exposed][Category("Paths")] public List<string> AdditionalProjectDirectories;
    
    [ImpVar][Exposed][Category("Test")] public Dictionary<string,Int32> Test_Dic;
    [ImpVar][Exposed][Category("Test")] public TTransform3D Test_Transform;
    [ImpVar][Exposed][Category("Test")][AdvancedDisplay] public int tango = 10;
}