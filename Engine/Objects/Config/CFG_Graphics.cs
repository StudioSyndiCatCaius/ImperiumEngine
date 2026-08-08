using ImperiumEngine;
using ImperiumEngine.Classes;
using R3D_cs;

namespace ImperiumEngine.Objects.Config;

public class CFG_Graphics : ImpConfig
{
    //Toggle fullscreen with F11. Not TRUE fullscreen but a borderless window (to avoid screen flashing).
    [ImpVar] public bool enable_fullscreen_toggle = true;
    [ImpVar] public bool enable_window_resize = true;
    [ImpVar] public bool enable_vsync=true;

    [ImpVar] public int resolution_width=1280;
    [ImpVar] public int resolution_height=720;

    [ImpVar] public AntiAliasingMode antialiasing_mode=AntiAliasingMode.Smaa;

}