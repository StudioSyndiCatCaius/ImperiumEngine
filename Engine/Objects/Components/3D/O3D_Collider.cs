using System.Numerics;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;

namespace ImperiumEngine.Objects._3D;

public enum EColliderShape
{
    Box,
    Sphere,
    Capsule,
}

public class O3D_Collider : ImpComponent3D
{
    [ImpVar] public EColliderShape colliderShape;
    [ImpVar] public double radius;
    [ImpVar] public double capsuleHeight;
}