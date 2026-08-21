using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;
using Model = R3D_cs.Model;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_PlayerStart : Imp3D
{
    // #################################################################################
    // Static
    // #################################################################################
    
    public static C3_PlayerStart GetFirstOfSceneLink(TRef<ImpScene> scene, int id = -1)
    {
        return null;
    }
    
    // #################################################################################
    // Class
    // #################################################################################
    
    [ImpVar] public TRef<ImpScene> linked_scene;
    [ImpVar] public int player_index = 0;

    private TBounds3 _custom_bounds;
    
    public override void OnDraw3D(double dt, EDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (flags.HasFlag(EDrawFlags.Editor))
        {
            _custom_bounds = TBounds3.Merge([
                Draw3D_Capsule(global_transform, 0.5f, 2, 6, Color.Green),
                Draw3D_Arrow(global_transform, 1, 0.03f, Color.Green)
            ]);
        }
    }

    protected override TBounds3 Bounds_Calc() { return _custom_bounds; }
}