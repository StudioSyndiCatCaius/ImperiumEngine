using ImperiumEngine.Source.Cores;
using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImperiumEngine.Source.Resources;

public class Texture2D: ImpResource
{
    public int RendererID { get; private set; }

    public Texture2D(string path)
    {
        RendererID = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, RendererID);

        using (var image = Image.Load<Rgba32>(path))
        {
            var pixels = new byte[4 * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);
            
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        }

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    }

    public void Dispose()
    {
        GL.DeleteTexture(RendererID);
    }
}