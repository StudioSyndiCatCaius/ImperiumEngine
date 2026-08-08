using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects.Assets;

public abstract class A_Animation : ImpAsset
{
    
}


/*
 * like a "Level Sequence" from Unreal Engine. these are fully custom animations.
 * The only thing is they require an "instiator" entity class. By default "ImpComponent" and if not is set when this anim is started,
 * Then the level root will be used as the instigator
 */
public class A_Animation_Custom : A_Animation
{
    
}



/*
 * 
 */
public class A_Animation_Skeletal : A_Animation
{
    
}


/*
 * Animation for a sequence of 2d sprites
 */
public class A_Animation_Sprite : A_Animation
{
    
}