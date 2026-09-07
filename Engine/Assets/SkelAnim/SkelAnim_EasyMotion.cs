namespace Engine.Assets.SkelAnim;

//easy locomotion setup
public class SkelAnim_EasyMotion : A_SkeletalAnimator
{
    [ImpVar] public A_SkeletonAnim idle_anim;
    [ImpVar] public A_BlendSpace motion_blend_space;
    [ImpVar] public A_BlendSpace falling_blend_space; // Y = fall/up velocity
}