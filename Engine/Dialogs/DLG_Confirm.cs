using Engine.Comps._2D;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Dialogs;

public class DLG_Confirm : ImpDialog
{
    // as immediate mode
    public override void Draw()
    {
        base.Draw();
        TBounds2 sp = UI.Box(new(0, 0, 0, 100), TLayout2.FULL);
        TBounds2[] areas = sp.Split([1.0f, 0.2f], true, EUIOrentation.V);
        UI.Text("Alert Message", TLayout2.FULL,default, areas[0]);
        TBounds2[] ar_btns = areas[1].Split([1, 1], true);
        UI.Button("YES", TLayout2.FULL,default, ar_btns[0]);
        UI.Button("NO", TLayout2.FULL,default, ar_btns[0]);
    }
    
    // as retained mode
    public DLG_Confirm()
    {
        C2_Box box = new C2_Box()
        {
            box_format = EBoxFormat.Vertical,
            on_begin = (c) =>
            {
                c.Child_Add(new C2_Text()
                {
                    text = "Alert Message",
                });
                c.Child_Add(new C2_Box()
                {
                    box_format = EBoxFormat.Horizontal,
                    on_begin = (c2) =>
                    {
                        c2.Child_Add(new C2_Button()
                        {
                            text = "YES",
                        });
                        c2.Child_Add(new C2_Button()
                        {
                            text = "NO",
                        });
                    }
                });
            } 
        };
    }
}