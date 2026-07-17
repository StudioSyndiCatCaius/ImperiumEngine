using ImperiumEngine.Classes;
using R3D_cs;

namespace ImperiumEngine.Objects.Config;

public class CFG_Graphics : ImpConfig
{
    //Toggle fullscreen with F11. Not TRUE fullscreen but a borderless window (to avoid screen flashing).
    public bool enable_fullscreen_toggle = true;
    public bool enable_window_resize = true;
    public bool enable_vsync=true;
    
    public int resolution_width=1280;
    public int resolution_height=720;

    public AntiAliasingMode antialiasing_mode=AntiAliasingMode.Smaa;
    
}