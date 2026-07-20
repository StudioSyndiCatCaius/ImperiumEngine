using Raylib_cs;

namespace ImperiumEngine.Interfaces;

public interface I_EditorConfig
{
    
}


public interface I_EditorAsset
{
    //later i want ti implement it so you can write custom thumbnail drawing, for things like 3d assets, widgets, etc.
    public virtual Texture2D GetThumbnailTexture() { return new Texture2D();}
    public Color GetThumbnailColor() { return Color.White;}
    
}