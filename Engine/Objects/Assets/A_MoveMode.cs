using ImperiumEngine;
using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects.Assets;


// a configurable move mode for a character component
public class A_MoveMode : ImpAsset
{
    [ImpVar] public double acceleration = 1.0f;
    [ImpVar] public double deceleration = 1.0f;
    [ImpVar] public double max_speed = 10.0f;

    //default gravity is downwards (but possibly there may be systems later to override this)
    [ImpVar] public double gravity_scale = 1.0f;
}