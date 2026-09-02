using System.Numerics;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Interfaces;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Comps._2D;

public enum EButtonLayout
{
    H_Icon_Text,
    H_Text_Icon,
    V_Icon_Text,
    V_Text_Icon,
}

public enum EButtonState
{
    Normal, Hover, Pressed,
}
[ImpClass(Common = true)]
public class C2_Button : Imp2D
{
    [ImpVar] public UI_Button style=UI_Button.DEFAULT;
    [ImpVar] public string text;
    [ImpVar] public A_Texture? icon;
    [ImpVar] public EButtonLayout button_layout;

    private bool _hovered;
    private bool _held;
    
    public Action<C2_Button> on_click;
    public Action<C2_Button> on_hover;
    public Action<C2_Button> on_unhover;

    public override bool ChildLayout_IsFree() { return false; }

    public C2_Button()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = this;
    }

    public override void OnInit()
    {
        base.OnInit();
        cursor_filter = ECursorFilter.Hit;
    }

    public override void OnOption_Refreshed()
    {
        if (option_data is I_General g)
        {
            Console.WriteLine("button refreshed: "+g.getTitle());
            text = g.getTitle();
            icon = g.getIcon();
            //text = "ga";
        }
        else
        {
            text = "nah";
        }
    }
    

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);
        if (bounds.IsEmpty) bounds = Bounds_Cache();

        UI_Box box=style.box_normal;
        if(_held) box=style.box_pressed;
        else if(_hovered) box=style.box_hover;
        
        TBounds2 _content_bounds = box.Draw(bounds, global_transform);
        
        TBounds2[] icon_text_bounds;
        float icon_size=0.0f; //get icons size here
        float[] _sizes = new float[1];
        _sizes.SetValue(icon_size,0);
        if (button_layout == EButtonLayout.H_Icon_Text || button_layout == EButtonLayout.H_Text_Icon)
        {
            icon_text_bounds = _content_bounds.Split(_sizes, false);
        }
        else
        {
            icon_text_bounds = _content_bounds.Split(_sizes, false, EUIOrentation.V);
        }

        if (button_layout == EButtonLayout.H_Icon_Text || button_layout == EButtonLayout.V_Icon_Text) icon_text_bounds.Reverse();
        
        style.font.Draw(text, icon_text_bounds[0], global_transform, ETextWrap.Word);
        if(icon!=null) icon.Draw(icon_text_bounds[1],global_transform,EImageLayout.Retain_Fit);

    }

    public override void _NotifyAs_CursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._NotifyAs_CursorTarget(player, notify, dt);
        switch (notify)
        {
            case ENotifyGeneric.Begin:
                _hovered = true;
                on_hover?.Invoke(this);
                Hooks.btn_hover?.Invoke(this);
                break;
            case ENotifyGeneric.End:
                _hovered = false;
                on_unhover?.Invoke(this);
                Hooks.btn_unhover?.Invoke(this);
                break;
        }
    }

    public override void _InputAs_CursorTarget(ImpPlayer player, EInputKey key, EInputState state, double dt)
    {
        base._InputAs_CursorTarget(player, key, state, dt);
        if (!player.Key_IsDragStart(key) && !ImpPlayer.KeyType_IsTouch(key)) return;
        if (state == EInputState.Pressed) _held = true;
        else if (state == EInputState.Released)
        {
            if (_held && _hovered && player.drag_target == null)
            {
                on_click?.Invoke(this);
                Hooks.btn_clicked?.Invoke(this);
            }
            _held = false;
        }
    }
    
}

public class UI_Button : ImpAsset
{
    private static TMargins _default_nineslice = new TMargins(10, 10, 10, 10);
    
    [ImpVar] public UI_Box box_normal = new UI_Box
    {
        is_inlined = true,
        background = A_Texture.UI_BTN,
        background_nineslice = _default_nineslice,
        tint = new Color(58, 58, 64, 255),
        inner_margins = new TMargins(8, 8, 6, 6),
    };

    [ImpVar] public UI_Box box_hover = new UI_Box
    {
        is_inlined = true,
        background = A_Texture.UI_BTN,
        background_nineslice = _default_nineslice,
        tint = new Color(78, 82, 94, 255),
        inner_margins = new TMargins(8, 8, 6, 6),
    };

    [ImpVar] public UI_Box box_pressed = new UI_Box
    {
        is_inlined = true,
        background = A_Texture.UI_BTN,
        background_nineslice = _default_nineslice,
        tint = new Color(40, 42, 48, 255),
        inner_margins = new TMargins(8, 8, 6, 6),
    };
    [ImpVar] public A_Font font=A_Font.ARIAL_P;
    
    // ================================================================================================================
    // STATIC
    // ================================================================================================================
    [Builtin] public static UI_Button DEFAULT = new();
}
