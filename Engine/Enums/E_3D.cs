using System.Numerics;

namespace Engine.Enums;

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


public enum ECollisionResponse
{
    Ignore,
    Overlap,
    Block,
}