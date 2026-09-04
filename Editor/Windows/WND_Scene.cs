using System.Numerics;
using Editor.Panels;
using Engine;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;

namespace Editor.Windows;


public class WND_Scene : EdWindow
{

    // ==============================================================================================
    // INIT
    // ==============================================================================================
    public WND_Scene()
    {
        
    }
    
    // ==============================================================================================
    // Scene
    // ==============================================================================================


 
    // ==============================================================================================
    // INPUT
    // ==============================================================================================
    
}

public enum EEditorScene_View { View_3D, View_2D, }
public enum EEditorGizmo_Mode { Translate, Rotate, Scale, }
public enum EEditorGizmo_Axis { World, Local }
public enum EEditorScene_EditMode { Gizmo, Landscape }

// ############################################################################################################
// Scene Panel
// ############################################################################################################

public class PNL_Scene : C2_Box
{
    public EEditorScene_View view = EEditorScene_View.View_3D;
    public EEditorGizmo_Mode gizmo_mode = EEditorGizmo_Mode.Translate;
    public EEditorGizmo_Axis gizmo_axis = EEditorGizmo_Axis.World;

    public A_Scene scene;
    

}

// ############################################################################################################
// Common Comp Item
// ############################################################################################################

public class CommonCompItem : Imp2D
{
    
}