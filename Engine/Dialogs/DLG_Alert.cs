using Engine.Assets;
using Engine.Comps._2D;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Dialogs;

public class DLG_Alert : ImpDialog
{
    public static void Run(string msg, Action on_close = null)
    {
        DLG_Alert dlg = new();
        ImpDialog.Bind(dlg, (i) =>
        {

        });
    }

    public DLG_Alert()
    {
        C2_Box box = new([
            new C2_Text( "Alert Message")
            {
                
            }
        ])
        {
            layout = TLayout2.CENTER_BOX
        };
        Child_Add(box);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);
    }
}