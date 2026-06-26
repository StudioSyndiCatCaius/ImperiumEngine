using ImperiumCore.Classes.Components;
using ImperiumEngine.Objects._2D;

namespace ImperiumEngine.Objects._1D.GameStates;


//A game state that runs until a task state reaches >= 1.0
public abstract class GameState_Task : ImpGameState
{
    public O2D_ProgressBar ui_progress;
    public O2D_Text ui_title;
    public O2D_Text ui_status;

    public virtual double GetTaskProgress() => 0;
    
    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (GetTaskProgress() >= 1.0)
        {
            State_Shutdown( "Complete");
        }
    }
}