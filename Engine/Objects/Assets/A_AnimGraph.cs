using ImperiumEngine.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects.Assets;

/*
 *  Anim Graphs are a custom graphs for animating characters (both 3d skeletal & 2d sprites).
 */
public abstract class A_AnimGraph : ImpAsset
{
    public TGraphData graph_data;

    
}


public abstract class A_AnimGraph_Skeletal : A_AnimGraph
{
    
}


public abstract class A_AnimGraph_Sprite : A_AnimGraph
{
    
}