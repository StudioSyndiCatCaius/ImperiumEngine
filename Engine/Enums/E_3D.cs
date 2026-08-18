using System.Numerics;
using ImperiumEngine.Comps;

namespace ImperiumEngine.Enums;

public enum ECollisionChannel
{
    Visibility,
    Cursor,
    World,
    Pawn,
    
    Custom_01=11,
    Custom_02=12,
    Custom_03=13,
    Custom_04=14,
    Custom_05=15,
    Custom_06=16,
    Custom_07=17,
    Custom_08=18,
    Custom_09=19,
    Custom_10=20,
}

public struct TTraceResult3D
{
    public bool hit;
    public Imp3D? hit_comp;
    public Vector3 hit_position;
    public Vector3 hit_normal;
    public Vector3 start_position;
    public Vector3 start_normal;
    
}

public enum ECollisionResponse
{
    Ignore,
    Overlap,
    Block,
}