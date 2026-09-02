using System.Numerics;
using Engine.Structs;

namespace Engine.Core;

public class ImpViewport
{
    public Vector2 position;
    public Vector2 size = new(1280, 720);

    public TBounds2 Bounds => new()
    {
        start = position,
        end = position + size,
    };
}
