using Engine.Core;

namespace Engine.Assets;

public enum ESignificanceLevel
{
    Top, High, Medium, Low, None
}

public struct TSignificanceLevelGlobalConfig
{
    [ImpVar] float distance;
}

[Title("Significance")]
public class A_Significance_Config : ImpAsset
{
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar] public A_Significance_LevelConfig default_level_config;
    [ImpVar] public Dictionary<ESignificanceLevel,A_Significance_LevelConfig> level_configs;
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATICS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar][Config] public static Dictionary<ESignificanceLevel,TSignificanceLevelGlobalConfig> global_configs=new ()
    {
        [ESignificanceLevel.Top]=new()
        {
            
        },
        [ESignificanceLevel.High]=new()
        {
            
        },
        [ESignificanceLevel.Medium]=new()
        {
            
        },
        [ESignificanceLevel.Low]=new ()
        {
            
        },
        [ESignificanceLevel.None]=new()
        {
            
        }
    };
    [Builtin] public static A_Significance_Config CHARACTER = new();
    [Builtin] public static A_Significance_Config STATIC = new();
}

public class A_Significance_LevelConfig : ImpAsset
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar] public bool update_enabled = true;
    [ImpVar] public bool is_drawn = true;
    [ImpVar] public float update_frequency = 0.0f;

    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATICS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [Builtin] public static A_Significance_LevelConfig DEFAULT_TOP = new()
    {

    };
    [Builtin] public static A_Significance_LevelConfig DEFAULT_HIGH = new()
    {
        update_frequency = 0.1f,
    };
    [Builtin] public static A_Significance_LevelConfig DEFAULT_MEDIUM = new()
    {
        update_frequency = 0.25f,
    };
    [Builtin] public static A_Significance_LevelConfig DEFAULT_LOW = new()
    {
        is_drawn = false,
        update_frequency = 0.5f,
    };
    [Builtin] public static A_Significance_LevelConfig DEFAULT_NONE = new()
    {
        is_drawn=false,
        update_enabled=false,
    };
}