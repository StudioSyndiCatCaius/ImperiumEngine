using Editor;
using ImperiumEngine;
using ImperiumEngine.Main;
using Raylib_cs;

ImpApp app= new ImpApp();

ImpApp.name = "Imperium Editor";
ImpScene _root = new ImpScene();
ImpScene.current = _root;
_root.root.Child_Add(new EdMain());

Raylib.SetConfigFlags(ConfigFlags.MaximizedWindow | ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);

app.Run(() =>
    Raylib.MaximizeWindow()
    );