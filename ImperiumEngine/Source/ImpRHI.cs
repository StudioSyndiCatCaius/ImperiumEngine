namespace ImperiumEngine.Source;

public struct FBufferContext
{
    public uint vao;
    public uint vbo;
    public uint ebo;
}

public struct FBufferData
{
    public float[] Vertices;
    public uint[]  Indices;

    public FBufferData(float[] vert, uint[] ind)
    {
        Vertices = vert;
        Indices = ind;
    }
}

public class ImpRHI
{
    public virtual void Buffer_Delete(FBufferContext context) { }
    public virtual FBufferContext Buffer_Generate(FBufferData bufferData) { return new FBufferContext(); }

    public virtual uint Shader_Generate()
    {
        return 0;
    }
}