namespace ImperiumEngine.Source.Libs;
using Silk.NET.OpenGL;

public static class ImpLib_3D
{
    public static GL GetRHI_OpenGL()
    {
        return ImperiumEngine.Program.gl;
    }

    public static string GetDefaultShader_Vertex()
    {
        return @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aColor;
        
        out vec3 fragColor;
        
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        
        void main()
        {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
            fragColor = aColor;
        }";
    }
    
    public static string GetDefaultShader_Fragment()
    {
        return @"
        #version 330 core
        in vec3 fragColor;
        out vec4 FragColor;
        
        void main()
        {
            FragColor = vec4(fragColor, 1.0);
        }";
    }
    
    // ========================================================================================================================
    // BUFFERS
    // ========================================================================================================================

    // ------------------------------------------------------------------------------
    // Shapes
    // ------------------------------------------------------------------------------
    
    public static FBufferData BufferShape_Cube()
    {
        FBufferData output = new FBufferData();
        output.Vertices =
        [
            // Front
            -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 0.0f, 1.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, 0.5f, 0.5f, 1.0f, 1.0f, 0.0f,
            // Back
            -0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 1.0f,
            0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 1.0f, 1.0f,
            -0.5f, 0.5f, -0.5f, 0.0f, 0.0f, 0.0f
        ];
        output.Indices =
        [
            // Front
            0, 1, 2,
            2, 3, 0,
            // Top
            3, 2, 6,
            6, 7, 3,
            // Right
            1, 5, 6,
            6, 2, 1,
            // Left
            4, 0, 3,
            3, 7, 4,
            // Back
            5, 4, 7,
            7, 6, 5,
            // Bottom
            4, 5, 1,
            1, 0, 4
        ];
        return output;
    }
}

