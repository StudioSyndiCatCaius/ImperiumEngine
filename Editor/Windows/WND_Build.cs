using ImperiumEngine;
using ImperiumEngine.Comps._2D;

namespace Editor.Windows;
/*
 * Window for building & packaging the game:
 * - Layout:
 * - - left:
 * - - - TOP: List of all platforms to build on
 * - - - BOTTOM: list of ImpBuild configurations
 * - - right: inspector for the selected build configuration
 * 
 * 
 */
public class WND_Build : EdWindow
{
    public ImpPlatform selected_platform;
    public ImpBuild selected_build;
    
    [ImpVar] public string build_path; //base path to output folders. game is placed in there with a folder for "Platform_Config". e.g. "{path_to_build}/Windows_Loose/"
    
    C2_List list_platforms = new();
    C2_List list_builds = new();
    C2_Inspector inspector = new();
    
    C2_Button btn_build; // begin build. uses a Dialog_Process
}