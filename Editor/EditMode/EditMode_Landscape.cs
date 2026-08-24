using ImperiumEngine;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;

namespace Editor.EditMode;

public class EditMode_Landscape : EdEditMode
{
    ImpLandscapeBrush brush_type;
    private TLandscapeBrushState brush_state;
    C3_Landscape landscape;

    public Dictionary<int, float> GetBrushPoints()
    {
        return null;
    }
    
    public override void OnUpdate(double dt)
    {
        if (ImpPlayer.Key_IsDown(EInputKey.Mouse_Left))
        {
            if (brush_type != null)
            {
                foreach (var point in GetBrushPoints())
                {
                    brush_type.EditPoint(this, point.Key, point.Value);
                }
            }
        }
    }
}


// ########################################################################################################
// Brushes
// ########################################################################################################

public abstract class ImpLandscapeBrush
{

    public virtual void EditPoint(EditMode_Landscape mode, int point_id, float weight) {}
}

// -----------------

public class LandBrush_Paint : ImpLandscapeBrush
{
    
}

public class LandBrush_Smooth : ImpLandscapeBrush
{
    
}

public class LandBrush_Flatten : ImpLandscapeBrush
{
    
}

public class LandBrush_Noise : ImpLandscapeBrush
{
    
}