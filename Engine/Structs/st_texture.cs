namespace ImperiumCore.Structs;

public enum ETextureMipmapGen
{
    None,
    Sharpen,
}

public enum ETextureFilter
{
    None,
    Nearest,
    Bilinear,
    Trilinear,
    Anisotropic,
}

public struct TModel_Texture
{
    public int width, height;
    public byte[]? pixels; // RGBA8, row-major, top-down. Uploaded to the GPU lazily by the RHI.
    public ETextureFilter filter;
    public ETextureMipmapGen mipmapGen;
}