using Engine.Assets;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._3D;

public struct TDungeonCellOption
{
    [ImpVar] public TRef<A_Scene> scene;
    [ImpVar] public float weight=1.0f;

    public TDungeonCellOption()
    {
        scene = default;
    }
}

public struct TDungeonCellType
{
    [ImpVar] public List<TDungeonCellOption> options;
}

// Procedural level generator
public class C3_Dungeon : Imp3D
{
    [ImpVar] public A_Dungeon_Builder builder;
    [ImpVar] public A_Dungeon_Style style;
    [ImpVar] public TVector3i size=new(10,1,10);

    public List<Imp3D> generated_cells;

    

    [CallInEditor][ScriptCall]
    public void Generate()
    {
        Destroy();
    }
    
    [CallInEditor][ScriptCall]
    public void Destroy()
    {
        
    }
}

public class A_Dungeon_Builder : ImpAsset
{
    
}

public class A_Dungeon_Style : ImpAsset
{
    [ImpVar] public Dictionary<TLabel, TDungeonCellType> types = new();
}