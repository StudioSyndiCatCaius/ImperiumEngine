namespace ImperiumEngine.Comps._2D;

public enum EProgressBarOrientation
{
    H_LeftToRight,
    H_RightToLeft,
    V_TopToBottom,
    V_BottomToTop,
}

public class C2_ProgressBar : Imp2D
{
    [ImpVar] public UI_ProgressBar style;
    [ImpVar] public EProgressBarOrientation orientation;
    [ImpVar] public float percent;
    
    [ImpVar] public bool use_ghosting = true;
    [ImpVar] public float ghost_delay = 0f; //time after percent changes before ghost is drawn
    [ImpVar] public float ghost_time = 0.2f; //time in seconds until the ghost percent matches the current percent

}

public class UI_ProgressBar : ImpAsset
{
    [ImpVar] public UI_Box box_back;
    [ImpVar] public UI_Box box_fill;
    [ImpVar] public UI_Box box_ghost; //optional. used for when the progress bar changes, this is the ghost for the previous value
    [ImpVar] public bool scaled_fill = false; //True= draw as scaled by percent. False= cuts of fill image by percent

    public void Draw(float percent,EProgressBarOrientation orientation)
    {
        
    }
}