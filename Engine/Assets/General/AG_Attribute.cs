using ImperiumEngine;
using Raylib_cs;

namespace ImperiumEngine.Assets.General;

public class AG_Attribute : A_General
{
    [ImpVar] public Color damage_color;
    [ImpVar] public bool is_static;
    [ImpVar] public float start_percent=0.0f;
    [ImpVar] public float max_value;
    [ImpVar] public int decimals=2;
    [ImpVar] public A_Curve1 rank_curve;
}