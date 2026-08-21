using ImperiumEngine;
using Raylib_cs;

namespace ImperiumEngine.Assets.General;

public class AG_Attribute : A_General
{
    // ============================================================================================================
    // CLASS
    // ============================================================================================================
    
    [ImpVar][Category("Attribute")] public TText alternate_title;
    [ImpVar][Category("Attribute")] public Color damage_color;
    
    [ImpVar][Category("Value")] public bool is_static;
    [ImpVar][Category("Value")] public float start_percent=0.0f;
    [ImpVar][Category("Value")] public float max_value;
    [ImpVar][Category("Value")] public int decimals=2;
    [ImpVar][Category("Value")] public A_Curve1 rank_curve;
    
    [ImpVar][Category("Recharge")] public bool can_recharge;
    [ImpVar][Category("Recharge")] public bool recharge_is_percent; // if true, recharge_value is a percent of max_value
    [ImpVar][Category("Recharge")] public float recharge_rate=0.1f; // time for recharge (takes dt into account). 0 = dt
    [ImpVar][Category("Recharge")] public float recharge_value=1.0f; // value to recharge by per rate
    
    // ============================================================================================================
    // STATIC
    // ============================================================================================================

    public static AG_Attribute HP = new()
    {
        title = "HP",
        alternate_title = "Health",
        max_value = 9999,
        start_percent = 1.0f,
        rank_curve = CURVE_METRIC_9999,
    };
    public static AG_Attribute MP = new()
    {
        title = "MP",
        alternate_title = "Mana",
        max_value = 999,
        start_percent = 1.0f,
        rank_curve = CURVE_METRIC_999,
    };
    public static AG_Attribute TP = new()
    {
        title = "TP",
        alternate_title = "TP",
        max_value = 100,
    };
    
    public static AG_Attribute PATK = new()
    {
        is_static = true,
        title = "Phy. Attack",
        alternate_title = "Strength",
        rank_curve = CURVE_STATIC_99,
    };
    public static AG_Attribute PDEF = new()
    {
        is_static = true,
        title = "Phy. Defense",
        alternate_title = "Endurance",
        rank_curve = CURVE_STATIC_99,
    };
    public static AG_Attribute MATK = new()
    {
        is_static = true,
        title = "Mag. Attack",
        alternate_title = "Magic",
        rank_curve = CURVE_STATIC_99,
    };
    public static AG_Attribute MDEF = new()
    {
        is_static = true,
        title = "Mag. Defense",
        alternate_title = "Resistance",
        rank_curve = CURVE_STATIC_99,
    };
    public static AG_Attribute SPD = new()
    {
        is_static = true,
        title = "Speed",
        rank_curve = CURVE_STATIC_99,
    };
    public static AG_Attribute DEX = new()
    {
        is_static = true,
        title = "Dexterity",
        rank_curve = CURVE_STATIC_99,
    };
    public static AG_Attribute AGGRO = new()
    {
        is_static = true,
        title = "Aggro",
    };

    public static A_Curve1 CURVE_METRIC_9999 = new();
    
    public static A_Curve1 CURVE_METRIC_999 = new();
    
    public static A_Curve1 CURVE_STATIC_99 = new();
}