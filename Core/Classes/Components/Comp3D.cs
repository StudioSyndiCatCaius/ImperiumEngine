using ImperiumCore.Structs;

namespace ImperiumCore.Classes.Components;

// #####################################################################################################################
// 3D
// #####################################################################################################################

public class ImpComponent3D : ImpComponent
{
    [ImpVar][Exposed] public TTransform3D transform;
}
