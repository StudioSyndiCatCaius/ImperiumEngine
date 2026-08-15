using Editor;
using Editor.Scenes;
using ImperiumEngine;
using Raylib_cs;


ImpApp app = new();


app.Run(() =>
{
    Raylib.SetConfigFlags(ConfigFlags.MaximizedWindow | ConfigFlags.ResizableWindow);
    
}, () =>
{ 
    Raylib.MaximizeWindow();
    ImpScene.current.root = new Scene_Editor()
    {

    };
    ImpScene.current.is_running=true;
    
}, () =>
{
    if (Scene_Editor.active != null)
    {
        EdState.Save(Scene_Editor.active);
    }
});