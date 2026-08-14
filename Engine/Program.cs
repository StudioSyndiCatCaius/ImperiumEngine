using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;


ImpApp app = new();

app.Run(null, () =>
{
    ImpScene _scene=new ImpScene()
    {
        is_running=true
    };

    ImpScene.current=_scene;
    ImpComp _root=new ImpComp();
    _scene.root=_root;

    _root.Child_Add(new C3_Mesh());
    _root.Child_Add(new C2_Box());
    //_root.Child_Add(new C2_SceneView());
});
