using System.Numerics;
using Editor.Windows;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Core;
using Engine.Structs;
using ImGuiNET;


namespace Editor.Scenes;



public class SCN_Editor : A_Scene
{
    public SCN_Editor()
    {
        root = new SNC_Editor_Root();
    }
}


public class SNC_Editor_Root : ImpComp
{
    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);
    }
}