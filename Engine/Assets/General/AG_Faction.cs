using System.Drawing;
using Engine.Enums;
using Engine.Structs;
using Color = Raylib_cs.Color;

namespace Engine.Assets.General;

public class AG_Faction : A_General
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar] public TTag faction_tag;
    [ImpVar] public Dictionary<TTag,EFactionAffinity> faction_affinity;
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [Builtin] public static AG_Faction PLAYER = new()
    {
        color = Color.SkyBlue,
        faction_affinity = new()
        {
            
        }
    };
    [Builtin] public static AG_Faction ENEMY = new()
    {
        color = Color.Red,
    };
    [Builtin] public static AG_Faction ALLY = new()
    {
        color = Color.Green,
    };
    [Builtin] public static AG_Faction NEUTRAL = new()
    {
        color = Color.Yellow,
    };
}