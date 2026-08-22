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
    
    public static C3_PlayerStart GetFirst(ImpScene scene, int player_index = -1)
    {
        if (scene == null || scene.root == null)
        {
            return null;
        }
        C3_PlayerStart any = null;
        C3_PlayerStart Walk(ImpComp n)
        {
            if (n is C3_PlayerStart ps)
            {
                if (player_index < 0 || ps.player_index == player_index)
                {
                    return ps;
                }
                if (any == null)
                {
                    any = ps;
                }
            }
            for (int i = 0; i < n.children.Count; i++)
            {
                C3_PlayerStart hit = Walk(n.children[i]);
                if (hit != null)
                {
                    return hit;
                }
            }
            return null;
        }
        C3_PlayerStart match = Walk(scene.root);
        if (match != null)
        {
            return match;
        }
        return any;
    }

    public static C3_PlayerStart GetFirstOfSceneLink(TRef<ImpScene> from_scene, int id = -1)
    {
        ImpScene live = ImpGame.current?.scene;
        if (live == null || live.root == null)
        {
            return null;
        }
        string want = from_scene.path;
        if (string.IsNullOrEmpty(want))
        {
            ImpScene asset = from_scene.Get();
            if (asset != null)
            {
                want = asset.filepath;
            }
        }
        if (string.IsNullOrEmpty(want))
        {
            return null;
        }
        C3_PlayerStart Walk(ImpComp n)
        {
            if (n is C3_PlayerStart ps)
            {
                string linked = ps.linked_scene.path;
                if (string.IsNullOrEmpty(linked))
                {
                    ImpScene asset = ps.linked_scene.Get();
                    if (asset != null)
                    {
                        linked = asset.filepath;
                    }
                }
                if (string.Equals(linked, want, StringComparison.OrdinalIgnoreCase))
                {
                    if (id < 0 || ps.player_index == id)
                    {
                        return ps;
                    }
                }
            }
            for (int i = 0; i < n.children.Count; i++)
            {
                C3_PlayerStart hit = Walk(n.children[i]);
                if (hit != null)
                {
                    return hit;
                }
            }
            return null;
        }
        return Walk(live.root);
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