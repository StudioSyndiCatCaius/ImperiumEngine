namespace Engine.Enums;

public enum ENotifyGeneric
{
    Begin, 
    Update, 
    End,
}

public enum ENotifyProcess
{
    Update, Draw2D, Draw3D,
}

public enum ENotifyGrabTarget
{
    Hover_AsTarget_Start, // when this begins hovering over a target 
    Hover_AsTarget_End, // when this stops hovering over a target
    Hover_AsInstigator_Start, // when an instigator starts hovering over this 
    Hover_AsInstigator_End, // when an instigator stops hovering over this
    Drop_AsTarget, //when this is dropped on a target
    Drop_AsInstigator //when an instigator drops this
}