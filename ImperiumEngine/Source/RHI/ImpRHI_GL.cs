using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ImperiumEngine.Source.RHI;



public class ImpRHI_GL : ImpRHI
{
   private GL _gl;

   public ImpRHI_GL(IWindow window)
   {
      _gl = window.CreateOpenGL();
   }
   public override void Buffer_Delete(FBufferContext context)
   {
      _gl.DeleteBuffer(context.vbo);
      _gl.DeleteBuffer(context.ebo);
      _gl.DeleteVertexArray(context.vao);
      base.Buffer_Delete(context);
   }

   public override FBufferContext Buffer_Generate(FBufferData bufferData)
   {
      unsafe
      {
         FBufferContext _context = new FBufferContext();
      
         // Create and bind VAO
         _context.vao=_gl.GenVertexArray();
         _gl.BindVertexArray(_context.vao);
        
         // Create and bind VBO
         _context.vbo = _gl.GenBuffer();
         _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _context.vbo);
         fixed (void* v = &bufferData.Vertices[0])
         {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(bufferData.Vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
         }
        
         // Create and bind EBO
         _context.ebo = _gl.GenBuffer();
         _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _context.ebo);
         fixed (void* i = &bufferData.Indices[0])
         {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(bufferData.Indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);
         }
         return base.Buffer_Generate(bufferData);
      }
   }

   private string _shader_vert = @"
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
   
   private string _shader_frag=@"
        #version 330 core
        in vec3 fragColor;
        out vec4 FragColor;
        
        void main()
        {
            FragColor = vec4(fragColor, 1.0);
        }";

   public override uint Shader_Generate()
   { 
      uint shader;
      
      uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
      _gl.ShaderSource(vertexShader, _shader_vert);
      _gl.CompileShader(vertexShader);

      uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
      _gl.ShaderSource(fragmentShader, _shader_frag);
      _gl.CompileShader(fragmentShader);
        
      string infoLog = _gl.GetShaderInfoLog(vertexShader);
      if (!string.IsNullOrWhiteSpace(infoLog))
      {
         Console.WriteLine($"Vertex shader compilation error: {infoLog}");
      }

      shader = _gl.CreateProgram();
        
      _gl.AttachShader(shader, vertexShader);
      _gl.AttachShader(shader, fragmentShader);
      _gl.LinkProgram(shader);

      _gl.DeleteShader(vertexShader);
      _gl.DeleteShader(fragmentShader);
      return shader;
   }
}