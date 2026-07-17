using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Const;
using ImperiumEngine.Objects._3D.Lights;
using ImperiumEngine.Objects._3D.Prim;
using ImperiumEngine.Renderers;


ImpApp App = new ImpApp("Test - 3D Scene",1280,720,
    new RHI_OpenGL(),
    null,new A_Project());

A_Mesh? sceneMesh = ImpAsset.Import<A_Mesh>("D:\\PROJECTS\\ImperiumEngine\\GitRepo\\Test.Scene\\dungeon_test_scene.glb");

ImpLevel lvl = new ImpLevel();

O3D_Mesh cMesh = new O3D_Mesh()
{
    mesh = sceneMesh
};
lvl.Object_Add(cMesh);

O3D_Light_Directional cLight = new O3D_Light_Directional();
lvl.Object_Add(cLight);


//Tests lights by rotating sun with Left & right arrow keys
void Tick(float deltaTime)
{
    float sunRotation = 0;
    if (false) //check bytekey = Right
    {
        sunRotation += 1;
    }
    if (false) //check bytekey = Left
    {
        sunRotation -= 1;
    }
    cLight.transform.rotation.X+=sunRotation;
}


App.Run();