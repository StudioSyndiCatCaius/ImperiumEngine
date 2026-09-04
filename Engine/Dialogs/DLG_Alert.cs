using System.Numerics;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Dialogs;

public class DLG_Alert : ImpDialog
{
    public DLG_Alert(string msg = "Alert", Action on_close = null)
    {
        Bind(this);
        C2_Box box = new([
            new C2_Text(msg)
            {
                layout = TLayout2.FULL,
                text_align = TLayoutAlignment.CENTER,
            },
            new C2_Button("OK", _ => Kill())
            {
                layout = new()
                {
                    alignment = new()
                    {
                        align_H = ELayoutAlignment.Fill,
                        align_V = ELayoutAlignment.Center
                    },
                    size = new Vector2(100, 50),
                },
            }
        ])
        {
            box_format = EBoxFormat.Vertical,
            layout = TLayout2.CENTER_BOX
        };
        Child_Add(box);
    }
    
}