namespace Engine.Assets.Materials;

public class A_M3_VertexPainted : A_Material
{
    [ImpVar] public TMaterialCommons base_config=new();
    [ImpVar] public TMaterialCommons R_config=new();
    [ImpVar] public TMaterialCommons G_config=new();
    [ImpVar] public TMaterialCommons B_config=new();
}