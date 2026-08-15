using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._1D;

public class C1_Dialog
{
    public bool is_open;

    C2_DialogHost _overlay;
    C2_DialogShade _shade;
    static C1_Dialog _open;

    public static bool IsOpen => _open != null && _open.is_open;
    public static ImpComp Host => IsOpen ? _open._overlay : null;

    protected C2_Box Overlay => _overlay;
    protected C2_DialogShade Shade => _shade;

    public virtual void Show()
    {
        CloseOpen();
        ImpComp host = C2_MenuBar.PopupHost() ?? ImpScene.current?.root;
        if (host == null) return;
        _overlay = new C2_DialogHost
        {
            layout = TLayout2.FULL,
            cursor_filter = ECursorFilter.Pass,
            style = null,
            on_escape = Close,
        };
        _shade = new C2_DialogShade
        {
            layout = TLayout2.FULL,
            style = new UiStyle_Box { tint = new Color(0, 0, 0, 150) },
            cursor_filter = ECursorFilter.Hit,
            on_click = Close,
        };
        _overlay.Child_Add(_shade);
        host.Child_Add(_overlay);
        Hog_Take();
        _open = this;
        is_open = true;
    }

    public virtual void Close()
    {
        Hog_Release();
        _overlay?.Destroy();
        _overlay = null;
        _shade = null;
        is_open = false;
        if (_open == this) _open = null;
    }

    public static void CloseOpen()
    {
        _open?.Close();
    }

    void Hog_Take()
    {
        if (_overlay == null || ImpPlayer.players.Count == 0) return;
        ImpPlayer p = ImpPlayer.players[0];
        if (p.input_hog == null) p.input_hog = _overlay;
    }

    // Subclasses that Detach a reusable panel before Destroying the overlay must
    // call this first. After Detach the hog is no longer under _overlay, so a
    // later Hog_Release would miss it and leave the scene view deaf.
    protected void Hog_Release()
    {
        if (_overlay == null) return;
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            ImpPlayer p = ImpPlayer.players[i];
            ImpComp hog = p.input_hog;
            if (hog == null) continue;
            for (ImpComp n = hog; n != null; n = n.parent)
            {
                if (n != _overlay) continue;
                p.input_hog = null;
                break;
            }
        }
    }
}

public class C2_DialogHost : C2_Box
{
    public Action on_escape;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer p = ImpPlayer.players[0];
            if (p.input_hog == null) p.input_hog = this;
        }
        if (on_escape != null && ImpPlayer.Key_IsPressed(EInputKey.Key_Escape))
            on_escape();
    }
}

public class C2_DialogShade : C2_Box
{
    public Action on_click;

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt == ECursorEvent.Select_A) on_click?.Invoke();
    }
}
