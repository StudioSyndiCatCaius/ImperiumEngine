

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
    new EditorLevel_Projects(),new EditorProject());

ImpAsset.Import<A_Texture2D>("{Engine}/Content/2D/icons/T_ico_barsHorizontal.png", true);

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