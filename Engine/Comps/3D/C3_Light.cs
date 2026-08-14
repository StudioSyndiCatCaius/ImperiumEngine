using ImperiumEngine;
using R3D_cs;
using Raylib_cs;


public enum ELightType { Point, Spot }

public class C3_Light : ImpComp3D
{
    [ImpVar] public ELightType light_type = ELightType.Point;
    [ImpVar] public Color color = Color.White;
    [ImpVar] public float energy = 1.0f;
    [ImpVar] public float specular = 1.0f;

    //point and spot only
    [ImpVar] public float range = 25.0f;
    [ImpVar] public float spot_angle = 45.0f;

    [ImpVar] public bool cast_shadows = true;

}
