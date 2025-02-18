using Silk.NET.OpenGL;
using Silk.NET.Assimp;
using System.Numerics;
using System.Runtime.InteropServices;
using StbImageSharp;
using File = System.IO.File;
using PrimitiveType = Silk.NET.OpenGL.PrimitiveType;

namespace ImperiumEngine.Source.Objects._3D
{
    public class O3D_Mesh : ImpComponent3D
    {
        private uint vao;
        private uint vbo;
        private uint ebo;
        private uint textureId;
        private uint uvBuffer;
        private int indexCount;
        private string texturePath;

        // Vertex structure to hold position, normal, and UV coordinates
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 TexCoords;
        }

        public O3D_Mesh(string glbPath, string texturePath = null)
        {
            this.texturePath = texturePath;
            LoadGLB(glbPath);
        }

        private unsafe void LoadGLB(string path)
        {
            using var assimp = Assimp.GetApi();
            var scene = assimp.ImportFile(path, 
                (uint)(PostProcessPreset.TargetRealTimeQuality | PostProcessSteps.FlipUVs));

            if (scene == null || scene->MFlags == Silk.NET.Assimp.Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
            {
                throw new Exception("Failed to load GLB file: {0}");
            }

            // Process the first mesh (you might want to handle multiple meshes)
            var mesh = *scene->MMeshes[0];
            var vertices = new List<Vertex>();
            var indices = new List<uint>();

            // Get vertices
            for (int i = 0; i < mesh.MNumVertices; i++)
            {
                var vertex = new Vertex
                {
                    Position = new Vector3(
                        mesh.MVertices[i].X,
                        mesh.MVertices[i].Y,
                        mesh.MVertices[i].Z
                    ),
                    Normal = mesh.MNormals != null ? new Vector3(
                        mesh.MNormals[i].X,
                        mesh.MNormals[i].Y,
                        mesh.MNormals[i].Z
                    ) : Vector3.Zero
                };

                // Get UV coordinates if they exist
                if (mesh.MTextureCoords[0] != null)
                {
                    vertex.TexCoords = new Vector2(
                        mesh.MTextureCoords[0][i].X,
                        mesh.MTextureCoords[0][i].Y
                    );
                }

                vertices.Add(vertex);
            }

            // Get indices
            for (int i = 0; i < mesh.MNumFaces; i++)
            {
                var face = mesh.MFaces[i];
                for (int j = 0; j < face.MNumIndices; j++)
                {
                    indices.Add(face.MIndices[j]);
                }
            }

            indexCount = indices.Count;

            // Create and bind VAO
            vao = _gl.GenVertexArray();
            _gl.BindVertexArray(vao);

            // Create and bind VBO
            vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            
            fixed (void* v = &vertices.ToArray()[0])
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, 
                    (nuint)(vertices.Count * sizeof(Vertex)), 
                    v, BufferUsageARB.StaticDraw);
            }

            // Create and bind EBO
            ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
            fixed (void* i = &indices.ToArray()[0])
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, 
                    (nuint)(indices.Count * sizeof(uint)), 
                    i, BufferUsageARB.StaticDraw);
            }

            // Set vertex attributes
            var vertexSize = sizeof(Vertex);
            
            // Position attribute
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 
                (uint)vertexSize, (void*)0);
            _gl.EnableVertexAttribArray(0);

            // Normal attribute
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 
                (uint)vertexSize, (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);

            // Texture coordinate attribute
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 
                (uint)vertexSize, (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);

            // Load texture if path is provided
            if (!string.IsNullOrEmpty(texturePath))
            {
                LoadTexture();
            }

            assimp.ReleaseImport(scene);
        }

        private unsafe void LoadTexture()
        {
            // Setup StbImage
            StbImage.stbi_set_flip_vertically_on_load(1);

            // Load the image
            using (Stream stream = File.OpenRead(texturePath))
            {
                ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        
                textureId = _gl.GenTexture();
                _gl.BindTexture(TextureTarget.Texture2D, textureId);

                fixed (void* data = &image.Data[0])
                {
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 
                        (uint)image.Width, (uint)image.Height, 0, 
                        PixelFormat.Rgba, PixelType.UnsignedByte, data);
                }

                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        
                _gl.GenerateMipmap(TextureTarget.Texture2D);
            }
        }

        public override unsafe void On_Draw3D(float delta, uint shader)
        {
            Transform.Rotation.Y += delta;

            _gl.BindVertexArray(vao);
            
            if (textureId != 0)
            {
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, textureId);
                _gl.Uniform1(_gl.GetUniformLocation(shader, "texture0"), 0);
            }

            var model = Matrix4x4.CreateRotationY((float)Transform.Rotation.Y) * 
                       Matrix4x4.CreateRotationX((float)Transform.Rotation.X);

            _gl.UniformMatrix4(_gl.GetUniformLocation(shader, "model"), 1, false, (float*)&model);
            
            _gl.DrawElements(GLEnum.Triangles, (uint)indexCount, DrawElementsType.UnsignedInt, (void*)0);
        }

        public override void On_End()
        {
            _gl.DeleteBuffer(vbo);
            _gl.DeleteBuffer(ebo);
            if (textureId != 0) _gl.DeleteTexture(textureId);
            _gl.DeleteVertexArray(vao);
            base.On_End();
        }
    }
}