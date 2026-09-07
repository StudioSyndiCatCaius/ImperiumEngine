namespace Engine.Assets;



public struct TSkelAnimPose
{

    public static TSkelAnimPose Blend(TSkelAnimPose A, TSkelAnimPose B, float t)
    {
        return A;
    }
}


public class A_SkeletalAnimator
{
    public virtual TSkelAnimPose ProcessAnimation(TSkelAnimPose input)
    {
        return input;
    }
}


