using Engine.Core;
using Engine.Structs;

namespace Engine.Assets;

public struct TSkeletonBone
{
     public string name;
     public TTransform3 transform;
     public List<TSkeletonBone> children;
}

public class A_Skeleton : ImpAsset
{
     // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
     // CLASS
     // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
     public TSkeletonBone root; //is this even needed with how raylib & r3d does skeletons?
     
     // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
     // STATIC
     // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
     public static A_Skeleton MANNEQUIN = new(){ sourcefile = "{engine}/3D/Character/QuatManeq/sk_QuaManneq_anims1_IP.glb" }; //this is same source files as `A_Mesh.MANNEQUIN`. meaning it's ImpFile should already be loaded
}