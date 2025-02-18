using System.Numerics;
using System.Drawing;

namespace ImperiumEngine.Source.Libs;

public static class ImpLib_Math
{
    public static Int32 Int_Random(Int32 min, Int32 max)
    {
        Random rand = new Random();
        return rand.Next(min, max);
    }
    
    
    public static Color Vec4_ToColor(Vector4 vector4)
    {
        return Color.FromArgb(
            (int)(vector4.W * 255),
            (int)(vector4.X * 255),
            (int)(vector4.Y * 255),
            (int)(vector4.Z * 255)
        );
    }
    
    public static Vector4 Color_ToVec4(Color color)
    {
        return new Vector4(
            color.R / 255.0f,
            color.G / 255.0f,
            color.B / 255.0f,
            color.A / 255.0f
        );
    }
}