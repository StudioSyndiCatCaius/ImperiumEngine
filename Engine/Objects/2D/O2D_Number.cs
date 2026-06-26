using System.Drawing;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

//This will replace both progrss bar and slider as the single component for number displays
public class O2D_Number : ImpComponent2D
{
    [ImpVar][Exposed] public A_UiStyle_Number style;
    
    [ImpVar][Exposed] public float value=0.5f;
    [ImpVar][Exposed] public float min=0;
    [ImpVar][Exposed] public float max=1;
    
    [ImpVar][Exposed] public bool show_value = true;
    [ImpVar][Exposed] public bool show_progress = true; //show the percentage progress bar
    [ImpVar][Exposed] public bool enable_sliderHandle = false; //show the slider handle
    [ImpVar][Exposed] public bool editable = false; //allow the user to edit the value
    
    [ImpVar][Exposed] public Color Color_Value;
}

public class A_UiStyle_Number : ImpAsset
{
    public TBrush_Text text;
    
    public TBrush_Image image_background;
    public TBrush_Image image_progress;
    public TBrush_Image image_slider;

    public A_UiStyle_Number()
    {
        text = new TBrush_Text();
        image_background = new TBrush_Image()
        {
            tint = Color.FromArgb(50, 50, 50)
        };
        image_progress = new TBrush_Image()
        {
            tint = Color.FromArgb(170, 170, 170)
        };
        image_slider = new TBrush_Image()
        {
            tint = Color.FromArgb(255, 255, 255)
        };
    }
}