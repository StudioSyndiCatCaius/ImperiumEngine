using System.Numerics;
using Engine.Comps._2D;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Dialogs;

public class DLG_Confirm : ImpDialog
{
    
    // as retained mode
    public DLG_Confirm(string msg, Action<bool> on_response, string yes_text = "YES", string no_text = "NO")
    {
        Bind(this);
        C2_Box box = new([
            new C2_Text(msg)
            {
                layout = TLayout2.FULL,
                text_align = TLayoutAlignment.CENTER,
            },
            new C2_Box([
                new C2_Button(yes_text, _ =>
                {
                    on_response(true);
                    Kill();
                }){layout = TLayout2.FULL},
                new C2_Button(no_text, _ =>
                {
                    on_response(false);
                    Kill();
                }){layout = TLayout2.FULL},
            ])
            {
                style = UI_Box.BLANK,
                box_format = EBoxFormat.Horizontal,
                layout = new()
                {
                    alignment = new()
                    {
                        align_H = ELayoutAlignment.Fill,
                        align_V = ELayoutAlignment.Center
                    },
                    size = new Vector2(100, 50),
                },
            },
        ])
        {
            box_format = EBoxFormat.Vertical,
            layout = TLayout2.CENTER_BOX
        };
        Child_Add(box);
    }
}