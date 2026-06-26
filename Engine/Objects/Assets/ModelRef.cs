using ImperiumCore.Classes;

namespace ImperiumCore.Assets;




public enum EModelRefType
{
    Mesh,
    Texture,
    Sound,
}

/*
 * ModelRef references a some data model from ImpApp (like texture, mesh, sound, etc) based on its `SourceFile` and type.
 * This is how you can have multiple assets (of say a mesh or a texture) but only change some properties of it (like texture hue, or mesh materials)
 * WITHOUT needed a total copy of the source file.
 * E.G. You have a slime monster mesh `slime.glb` with 2 `A_Mesh` ImpAssets referecing it `slime_red.ImpAsset` and `slime_blue.ImpAsset` with only their material change but both referencing the same source file. 
 */
public class ModelRef : ImpAsset
{
    [ImpVar] public EModelRefType ModelType;
}