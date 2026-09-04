using Engine;
using Engine.Structs;

namespace Engine.Core;

public class ImpDialog : ImpComp
{
    public static bool Bind(ImpDialog dlg)
    {
        if(App.dialog_current!=null) return false;
        App.dialog_current = dlg;
        return true;
    }
    // ------------------------------------------------------------------------------------
    // Close
    // ------------------------------------------------------------------------------------
    public override void OnEnd()
    {
        base.OnEnd();
        
        if(App.dialog_current==this)
        {
            App.dialog_current = null;
            
        }
    }
    
}