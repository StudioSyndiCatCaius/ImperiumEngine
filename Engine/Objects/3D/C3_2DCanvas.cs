namespace ImperiumEngine.Objects._3D;

/*
 *  2D components that are children of this are NOT drawn to the screen normally, but instead are draw within the context of this canvas container
 *  
 */


public class C3_2DCanvas
{
    /*
     *  TRUE=draws on the 2d GUI canvas, like a normal ImpComponent2D, BUT taking the screen-space position relative to the world location
     *  FALSE=draws in 3D space as a texture on top of a plane
     */
    bool screen_space;
    
    //only avaialble for screen_space=false. If rendered as a 3D component, TRUE will make sure the drawn plane ALWAYS faces the camera
    bool camera_facing;
}