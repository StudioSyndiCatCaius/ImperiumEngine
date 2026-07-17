using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects._1D;

public class O1D_Timer : ImpComponent
{
    private double current_time;
    private bool is_running;
    
    public double duration;
    public bool loop=false;
    public bool autostart=false;
}