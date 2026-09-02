using Engine;
using Engine.Structs;

namespace Engine.Core;

public class ImpDialog : ImpComp
{
    public static bool Bind(ImpDialog dlg, Action<int> on_close_event)
    {
        if(App.dialog_current!=null) return false;
        App.dialog_current = dlg;
        dlg.on_close_event = on_close_event;
        return true;
    }
    // ------------------------------------------------------------------------------------
    // Close
    // ------------------------------------------------------------------------------------
    public Action<int> on_close_event;
    
    public virtual void Draw()
    {
        //UI.Box(new(0, 0, 0, 100),TLayout2.FULL);
    }
    
    public void Close(int flag)
    {
        if(App.dialog_current==this)
        {
            on_close_event?.Invoke(flag);
            App.dialog_current = null;
            on_close_event=null;
        }
    }
}