using Engine.Comps._2D;
using Engine.Core;

namespace Engine;

/*
 *  This is the main library of all global Actions that can be hooked into (typically my the scripting system)
 */
[ImpClass(GlobalizeFunctions = true)]
public static class Hooks
{
    // ----------------------------------------------------------------------------------------------------------------
    // App
    // ----------------------------------------------------------------------------------------------------------------
    public static Action app_pre_init;
    public static Action app_post_init;

    // ----------------------------------------------------------------------------------------------------------------
    // Scene
    // ----------------------------------------------------------------------------------------------------------------
    public static Action scene_change_begin;
    public static Action scene_change_end;
    
    // ----------------------------------------------------------------------------------------------------------------
    // Button
    // ----------------------------------------------------------------------------------------------------------------
    public static Action<C2_Button> btn_clicked;
    public static Action<C2_Button> btn_hover;
    public static Action<C2_Button> btn_unhover;
    
    // ----------------------------------------------------------------------------------------------------------------
    // OptionList
    // ----------------------------------------------------------------------------------------------------------------
    public static Action<C2_OptionList, int, Imp2D> opt_select;
    public static Action<C2_OptionList, int, Imp2D> opt_hover;
    public static Action<C2_OptionList, int, Imp2D> opt_unhover;
}