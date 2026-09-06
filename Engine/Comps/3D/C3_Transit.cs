using Engine.Assets;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._3D;

//transit between scenes
public class C3_Transit : Imp3D
{
    [ImpVar] public TRef<A_Scene> linked_scene;
    
}