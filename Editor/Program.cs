

using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumEditor.Scenes;
using ImperiumEngine.Renderers;


/*  EDITOR SCENE FLOW
 *      1. - LevelProjects
 *      2. - scene_loading
 *      3. - LevelMain
*/

ImpApp App = new ImpApp("Imperium Editor",1280,720,
    new RHI_Raylib(),
    new EditorLevel_Projects());
App.Run();