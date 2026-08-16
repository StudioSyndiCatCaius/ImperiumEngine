using ImperiumEngine.Structs;

namespace ImperiumEngine;

public class A_Save : ImpAsset
{
    
}

public class Save_Game : A_Save
{
    public TRef<ImpScene> scene_current;
    public TTransform3 scene_position;
}


public class Save_Global : A_Save
{
    
}