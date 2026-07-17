

using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumEditor.Scenes;
using ImperiumEngine.Renderers;


/*  EDITOR SCENE FLOW
 *      1. - LevelProjects
 *      2. - scene_loading
 *      3. - LevelMain
*/


ImpApp App = new ImpApp("Imperium Editor",1280,720,
    new RHI_OpenGL(),
    null,new EditorProject());

foreach (var _file in ImpFile.ListFiles("{Engine}/_Resources/icons/", "png"))
{
    ImpAsset.Import<A_Texture2D>(_file, true);    
}

App.Level_Current = new EditorLevel_Main();

App.Run();

// ====================================================================================
// Project
// ====================================================================================

class EditorProject : A_Project
{
    
}

// ====================================================================================
// Config
// ====================================================================================

public abstract class EditorConfig : ImpConfig
{
    public override string Config_GetSaveDir()
    {
        return ImpFile.Dir_EngineConfig();
    }
}