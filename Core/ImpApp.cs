using System.Numerics;
using ImperiumCore.Classes;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace ImperiumCore;

//The basemost class for making an app using Imperium Engine
public class ImpApp
{
    private readonly IWindow _window;
    // private List<IWindow> window_children;
    public readonly ImpRender RHI;
    
    public List<ImpPlayer> players;

    public ImpLevel Level_Current; //level that that can be changed and transitioned between
    public ImpLevel Level_Load; //level activated only when transiting between current levels
    public ImpLevel Level_Global; //level that is kept loaded the entire app lifetime
    
    public Dictionary<string, ImpAsset> AssetRegistry = new();
    
    public List<ImpLevel> Levels_GetAll()
    {
        List<ImpLevel> _lvls = null;
        _lvls.Add(Level_Global);
        _lvls.Add(Level_Current);
        _lvls.Add(Level_Load);
        return _lvls;
    }
    
    public ImpApp(string title = "Imperium Engine", int width = 1280, int height = 720,
        ImpRender? graphics = null, ImpLevel? level = null)
    {
        RHI = graphics;
        
        Level_Current = level;
        Level_Global = new ImpLevel();
        Level_Load = new ImpLevel();

        var options = WindowOptions.Default with
        {
            Size  = new Vector2D<int>(width, height),
            Title = title
        };

        _window = Window.Create(options);
        _window.Load    += AppBegin;
        _window.Update  += AppUpdate;
        _window.Render  += AppRender;
        _window.Closing += AppEnd;
    }

    private void AppEnd()
    {
        foreach (var lvl in Levels_GetAll())
        {
            lvl.OnEnd();
        }
        RHI.OnRHI_End();
    }

    private void AppRender(double dt)
    {
        foreach (var lvl in Levels_GetAll())
        {
            lvl.OnDraw(RHI, dt);
        }
    }

    private void AppUpdate(double dt)
    {
        foreach (var lvl in Levels_GetAll())
        {
            lvl.OnUpdate(dt);
        }
    }

    private void AppBegin()
    {
        RHI.OnRHI_Init();
        foreach (var lvl in Levels_GetAll())
        {
            lvl.App = this;
            lvl.OnBegin(this);
        }
    }

    public void Run()
    {
        _window.Run();
        _window.Dispose();
    }
    
    // ================================================================================================================
    // Level
    // ================================================================================================================

    //change the current level. if sync is true, dont handle level_load. simple pause & hard transition once the new level is ready
    public bool Level_Change(ImpLevel new_level, bool sync = false)
    {
        return false;
    }
    
    
}