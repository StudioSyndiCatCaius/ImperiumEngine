using Engine;
using Engine.Assets;
using Engine.Core;
using Raylib_cs;
using Test;

// -- run
App app=new();

A_Scene scene=new();
TestComp testcomp=new TestComp();

scene.root=testcomp;
App.scene_current=scene;
app.Run(new()
{
    
});