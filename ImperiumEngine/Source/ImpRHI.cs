using System.Numerics;
using Silk.NET.Maths;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace ImperiumEngine.Source;

public abstract class ImpRHI
{
    protected string  Title;
    protected Vector2 WindowSize;

    public bool bCanResize;
    public bool bIsFullscreen;
    
    public event Action         Begin;
    public event Action<double> Update;
    public event Action<double> Render;
    public event Action         End;
    
    protected ImpRHI(string title, Vector2 windowSize)
    {
        Title = title;
        WindowSize = windowSize;
    }
    
    protected void Native_Begin()
    {
        On_PreBegin();
        Begin?.Invoke();
        On_PostBegin();
    }

    protected void Native_Update(double delta)
    {
        On_PreUpdate(delta);
        Update?.Invoke(delta);
        On_PostUpdate(delta);
    }

    protected void Native_Render(double delta)
    {
        On_PreRender(delta);
        Render?.Invoke(delta);
        On_PostRender(delta);
    }

    protected void Native_End()
    {
        On_PreEnd();
        End?.Invoke();
        On_PostEnd();
    }

    public abstract void Run();
    
    protected virtual void On_PreBegin() {}
    protected virtual void On_PostBegin() {}
    protected virtual void On_PreEnd() {}
    protected virtual void On_PostEnd() {}
    protected virtual void On_PreUpdate(double delta) {}
    protected virtual void On_PostUpdate(double delta) {}
    protected virtual void On_PreRender(double delta) {}
    protected virtual void On_PostRender(double delta) {}
    
    public virtual void Window_Resize(Vector2 size) { }
}

// =========================================================================================================
// Silk
// =========================================================================================================

public abstract class ImpRHI_SILK : ImpRHI
{
    public IWindow         SilkWindow;
    public ImGuiController SilkImgui;
    public IInputContext   SilkInput;
    
    public ImpRHI_SILK(string title, Vector2 windowSize) : base(title, windowSize)
    {
        Title = title;
        WindowSize = windowSize;
    }
    
    public override void Run()
    {
        //Create Silk Window
        WindowOptions opt = WindowOptions.Default;
        opt.Title = Title;
        opt.Size = new Vector2D<int>((int)WindowSize.X,(int)WindowSize.Y);
        
        SilkWindow = Window.Create(opt);

        //Setup Event Bindings
        SilkWindow.Load += Native_Begin;
        SilkWindow.Update += Native_Update;
        SilkWindow.Render += Native_Render;
        
        SILK_Setup();
        SilkWindow.FramebufferResize += s =>
        {
            Window_Resize(new Vector2(s.X,s.Y));
        }; 
        
        // -- MAIN LOOP --
        SilkWindow.Run();
        Native_End();
    }
    
    protected override void On_PreUpdate(double delta)
    {
        SilkImgui?.Update((float)delta);
    }

    protected override void On_PostRender(double delta)
    {
        SilkImgui?.Render();
        //SilkWindow.SwapBuffers();
        base.On_PostRender(delta);
    }

    protected override void On_PostEnd()
    {
        SILK_End();
        SilkInput?.Dispose();
        SilkWindow?.Dispose();
        base.On_PostEnd();
    }

    public virtual void SILK_Setup() {}
    public virtual void SILK_End() {}
}