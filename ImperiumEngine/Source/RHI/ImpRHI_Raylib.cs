using System.Numerics;
using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;

namespace ImperiumEngine.Source.RHI;

public class ImpRHI_Raylib : ImpRHI
{
    public ImpRHI_Raylib(string title, Vector2 windowSize) : base(title, windowSize)
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
        Raylib.InitWindow((int)windowSize.X, (int)windowSize.Y, title);
        Raylib.SetTargetFPS(60);
    }
    
    public override void Run()
    {
        rlImGui.Setup(true);
        
        Native_Begin();
        
        while (!Raylib.WindowShouldClose())
        {
            // -- UPDATE
            float deltaTime = Raylib.GetFrameTime();
            Native_Update(deltaTime);
            
            
            // -- RENDER
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            rlImGui.Begin();
            
            Native_Render(deltaTime);
            
            rlImGui.End();
            Raylib.EndDrawing();
        }
        
        rlImGui.Shutdown();
        Native_End();
    }

    protected override void On_PreEnd()
    {
        Raylib.CloseWindow();
        base.On_PreEnd();
    }
}