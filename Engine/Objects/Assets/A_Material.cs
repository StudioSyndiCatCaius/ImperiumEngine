using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects.Assets;

public abstract class A_Material : ImpAsset
{
    
}


//a custom graph shader
public abstract class A_MaterialShader : A_Material
{
    
}


//an instance of a material using a parent material
public abstract class A_MaterialInstance : A_Material
{
    public A_Material parent;
}