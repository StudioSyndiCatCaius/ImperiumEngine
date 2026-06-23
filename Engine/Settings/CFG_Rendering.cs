using ImperiumCore;
using ImperiumCore.Classes;

namespace ImperiumEngine.Settings;

public class CFG_Rendering : ImpConfig
{
 
    [ImpVar][Exposed]
    public bool AmbientOcclusion_Enabled = true;
    
    [ImpVar][Exposed][Category("Motion Blur")]
    public bool MotionBlur_Enabled = true;
}