using ImperiumCore;
using ImperiumCore.Classes.Components;

namespace ImperiumEngine.Objects._3D;

public class O3D_SpringArm : ImpComponent3D
{
    
    [ImpVar][Exposed] public float Length=100f;
    
    [ImpVar][Exposed] public bool LagLocation=false;
    [ImpVar][Exposed] public float LagSpeed_Location=10f;
    [ImpVar][Exposed] public bool LagRotation=false;
    [ImpVar][Exposed] public float LagSpeed_Rotation=10f;
}