using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ImperiumEngine.Source._temps;

public class ImpCamera
{
    private float speed = 8f;
    private float screen_width;
    private float screen_height;
    private float sensitivity=180f;
    
    // position
    public Vector3 position;

    private Vector3 up = Vector3.UnitY;
    private Vector3 front = -Vector3.UnitZ;
    private Vector3 right = Vector3.UnitX;
    
    // rots
    private float pitch;
    private float yaw = -90f;
    private bool firstMove = true;
    public Vector2 LastPos;
    
    public ImpCamera(float screenWidth,float screenHeight, Vector3 position)
    {
        screen_width = screenWidth;
        screen_height = screenHeight;
        this.position = position;
    }

    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(position, position + front, up);
    }

    public Matrix4 GetProjectionMatrix()
    {
        return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), screen_width / screen_height,
            0.1f, 100f);
    }

    private void UpdateVectors()
    {
        if (pitch > 89f) { pitch = 89f; }
        if (pitch < -89f) { pitch = -89f; }
        
        front.X = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Cos(MathHelper.DegreesToRadians(yaw));
        front.Y = MathF.Sin(MathHelper.DegreesToRadians(pitch));
        front.Z = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Sin(MathHelper.DegreesToRadians(yaw));

        front = Vector3.Normalize(front);
        right = Vector3.Normalize(Vector3.Cross(front, Vector3.UnitY));
        up = Vector3.Normalize(Vector3.Cross(right, front));
    }

    public void InputController(KeyboardState input, MouseState mouse, FrameEventArgs e)
    {
        if (input.IsKeyDown(Keys.W))
        {
            position += front * speed * (float)e.Time;
        }
        if (input.IsKeyDown(Keys.A))
        {
            position -= right * speed * (float)e.Time;
        }
        if (input.IsKeyDown(Keys.S))
        {
            position -= front * speed * (float)e.Time;
        }
        if (input.IsKeyDown(Keys.D))
        {
            position += right * speed * (float)e.Time;
        }
        
        if (input.IsKeyDown(Keys.Space))
        {
            position.Y += speed * (float)e.Time;
        }
        if (input.IsKeyDown(Keys.LeftShift))
        {
            position.Y -= speed * (float)e.Time;
        }

        if (firstMove)
        {
            LastPos = new Vector2(mouse.X, mouse.Y);
            firstMove = false;
        }
        else
        {
            var deltaX = mouse.X - LastPos.X;
            var deltaY = mouse.Y - LastPos.Y;
            LastPos = new Vector2(mouse.X, mouse.Y);

            yaw += deltaX * sensitivity * (float)e.Time;
            pitch -= deltaY * sensitivity * (float)e.Time;
        }
        UpdateVectors();
    }

    public void Update(KeyboardState input, MouseState mouse, FrameEventArgs e)
    {
        InputController(input,mouse,e);
    }
}