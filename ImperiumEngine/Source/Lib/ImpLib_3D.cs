// ImperiumEngine Core for rendering 3D graphics

using OpenTK.Mathematics;

namespace ImperiumEngine.Source.Cores;

public struct FMatrixData
{
    public List<Vector3> vertices;
    public List<Vector2> UVs;
    public uint[] indices;
}

public static class ImpLib_3D
{
    // Function to load a text file and return its contents as a string
    public static string LoadShaderSource(string filePath)
    {
        string shaderSource = "";

        try
        {
            using (StreamReader reader = new StreamReader("../../../Source/Shaders/" + filePath))
            {
                Console.WriteLine("Shader Load - SUCCESS: "+$"{filePath}");
                shaderSource = reader.ReadToEnd();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Shader Load - FAILED: " + e.Message);
        }

        return shaderSource;
    }

    public static void GFX_Load()
    {
        
    }

    public static FMatrixData GetMatrixShape_Cube()
    {
        var data = new FMatrixData();
        return data;
    }
    
    public static FMatrixData GetMatrixShape_Cone()
    {
        var data = new FMatrixData();
        return data;
    }
    
    public static FMatrixData GetMatrixShape_Sphere()
    {
        var data = new FMatrixData();
        return data;
    }
    
    // ------------ Drawing ----------------
    public static void DrawMatrix(FMatrixData data,FTransform3D transform)
    {
        
    }
    
    public static void DrawShape_Cube(FTransform3D transform)
    {
        
    }
    
    public static void DrawShape_Sphere(FTransform3D transform)
    {
        
    }
    
}

