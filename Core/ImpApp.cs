using System.Numerics;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace ImperiumCore;

//The basemost class for making an app using Imperium Engine
public class ImpApp
{
    // The running app. Set the moment an ImpApp is constructed so ImpRender.Get()
    // (and anything else) can reach the app/RHI from anywhere.
    public static ImpApp? Active { get; private set; }

    private readonly IWindow _window;
    private IInputContext? _input;
    public IWindow Window => _window; // windowing handle for the RHI to bind its context
    public readonly ImpRender RHI;
    public A_Project appProject;
    
    public List<ImpPlayer> players = new();

    // ------------------------------------------------------------
    // LEVELS
    // ------------------------------------------------------------
    
    public ImpLevel Level_Current; //level that that can be changed and transitioned between
    public ImpLevel Level_Load; //level activated only when transiting between current levels
    public ImpLevel Level_Global; //level that is kept loaded the entire app lifetime
    
    // ------------------------------------------------------------
    // SAVE
    // ------------------------------------------------------------
    
    public ImpSave_Game Save_Game;
    public ImpSave_Global Save_Global;
    
    // ------------------------------------------------------------
    // ASSETS
    // ------------------------------------------------------------
    
    public Dictionary<string,Guid> model_links = new();
    
    //asset type models
    public Dictionary<Guid,TModel_Mesh> models_mesh = new();
    public Dictionary<Guid,TModel_Texture> models_texture = new();
    public Dictionary<Guid,TModel_Sound> models_sound = new();
    
    
    public Dictionary<string, ImpAsset> AssetRegistry = new();
    
    
    // ============================================================================================================
    // ============================================================================================================
    // APP LIFECYCLE
    // ============================================================================================================
    // ============================================================================================================
    
    public List<ImpLevel> Levels_GetAll()
    {
        var _lvls = new List<ImpLevel>();
        if (Level_Global  != null) _lvls.Add(Level_Global);
        if (Level_Current != null) _lvls.Add(Level_Current);
        if (Level_Load    != null) _lvls.Add(Level_Load);
        return _lvls;
    }

    public ImpApp(string title = "Imperium Engine", int width = 1280, int height = 720,
        ImpRender? graphics = null, ImpLevel? level = null, A_Project? project = null)
    {
        Active = this;
        appProject = project;
        
        RHI = graphics;
        if (RHI != null) RHI.App = this;

        Level_Current = level;
        Level_Global = new ImpLevel();
        Level_Load = new ImpLevel();

        var options = WindowOptions.Default with
        {
            Size  = new Vector2D<int>(width, height),
            Title = title
        };

        _window = Silk.NET.Windowing.Window.Create(options);
        _window.Load    += AppBegin;
        _window.Update  += AppUpdate;
        _window.Render  += AppRender;
        _window.Closing += AppEnd;
        _window.Resize  += sz => Viewport_Set(sz.X, sz.Y);
    }

    private void Viewport_Set(int width, int height)
    {
        if (RHI != null) RHI.ViewportSize = new TVector2i(width, height);
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
        RHI.OnRHI_FrameBegin(dt);
        foreach (var lvl in Levels_GetAll())
        {
            lvl.OnDraw(RHI, dt);
        }
        RHI.OnRHI_FrameEnd(dt);
    }

    private void AppUpdate(double dt)
    {
        RHI.OnRHI_Update(dt);

        foreach (var p in players) p.Input_Poll();
        foreach (var lvl in Levels_GetAll()) lvl.OnUpdate(dt);

        var primary = players.Count > 0 ? players[0] : null;
        if (primary != null) primary.UI_Process(UI_HitTest(primary.MousePos));
    }

    // topmost interactive 2D node under the cursor, across all level trees (draw order)
    private ImpComponent2D? UI_HitTest(Vector2 mouse)
    {
        ImpComponent2D? target = null;
        foreach (var lvl in Levels_GetAll())
        {
            foreach (var o in lvl.Objects)
            {
                if (o is ImpComponent2D c2)
                {
                    var hit = c2.Hit_Test(mouse);
                    if (hit != null) target = hit;
                }
            }
        }
        return target;
    }

    private void AppBegin()
    {
        Viewport_Set(_window.Size.X, _window.Size.Y);

        _input = _window.CreateInput();
        if (players.Count == 0) players.Add(new ImpPlayer());
        foreach (var p in players) p.InputContext ??= _input;

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