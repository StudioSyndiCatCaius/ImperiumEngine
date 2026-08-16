using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;

// Modal overlay. Not an ImpComp — one instance at a time, owned by ImpPlayer.current_dialog.
// Child types open with a static Run(...) that takes layout args and Action callbacks for choices.
public class ImpDialog
{
    public bool is_open;

    // Shade / Escape. Subclasses assign this to their cancel / no / ok path before Show().
    public Action on_dismiss;

    C2_DialogHost _overlay;
    C2_DialogShade _shade;

    public static ImpDialog Current => ImpPlayer.current_dialog;
    public static bool IsOpen => Current != null && Current.is_open;
    public static ImpComp Host => IsOpen ? Current._overlay : null;

    protected C2_Box Overlay => _overlay;
    protected C2_DialogShade Shade => _shade;

    public static bool Contains(ImpComp c)
    {
        ImpComp host = Host;
        if (host == null || c == null)
        {
            return false;
        }
        return C2_MenuBar.IsUnder(host, c);
    }

    public virtual void Show()
    {
        if (is_open)
        {
            return;
        }
        if (ImpPlayer.current_dialog != null && ImpPlayer.current_dialog != this)
        {
            ImpPlayer.current_dialog.Close();
        }
        ImpPlayer.Popup_Close();

        ImpComp host = C2_MenuBar.PopupHost() ?? ImpScene.current?.root;
        if (host == null)
        {
            return;
        }

        _overlay = new C2_DialogHost
        {
            layout = TLayout2.FULL,
            cursor_filter = ECursorFilter.Pass,
            style = null,
            on_escape = Dismiss,
        };
        _shade = new C2_DialogShade
        {
            layout = TLayout2.FULL,
            style = new UiStyle_Box { tint = new Color(0, 0, 0, 150) },
            cursor_filter = ECursorFilter.Hit,
            on_click = Dismiss,
        };
        _overlay.Child_Add(_shade);
        host.Child_Add(_overlay);
        Hog_Take();
        ImpPlayer.current_dialog = this;
        is_open = true;
    }

    public virtual void Close()
    {
        Hog_Release();
        _overlay?.Destroy();
        _overlay = null;
        _shade = null;
        is_open = false;
        if (ImpPlayer.current_dialog == this)
        {
            ImpPlayer.current_dialog = null;
        }
    }

    public static void CloseOpen()
    {
        if (ImpPlayer.current_dialog != null)
        {
            ImpPlayer.current_dialog.Close();
        }
    }

    protected virtual void Dismiss()
    {
        if (on_dismiss != null)
        {
            on_dismiss();
            return;
        }
        if (is_open)
        {
            Close();
        }
    }

    void Hog_Take()
    {
        if (_overlay == null)
        {
            return;
        }
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            ImpPlayer p = ImpPlayer.players[i];
            p.input_hog = _overlay;
            p.target_grabbed = null;
            p.grab_is_active = false;
        }
    }

    // Subclasses that Detach a reusable panel before Destroying the overlay must
    // call this first. After Detach the hog is no longer under _overlay, so a
    // later Hog_Release would miss it and leave the scene view deaf.
    protected void Hog_Release()
    {
        if (_overlay == null)
        {
            return;
        }
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            ImpPlayer p = ImpPlayer.players[i];
            ImpComp hog = p.input_hog;
            if (hog != null && C2_MenuBar.IsUnder(_overlay, hog))
            {
                p.input_hog = null;
            }
            Imp2D focus = p.target_focus;
            if (focus != null && C2_MenuBar.IsUnder(_overlay, focus))
            {
                p.target_focus = null;
            }
        }
    }
}

[ImpClass(Hidden = true)]
public class C2_DialogHost : C2_Box
{
    public Action on_escape;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer p = ImpPlayer.players[0];
            if (p.input_hog == null || !C2_MenuBar.IsUnder(this, p.input_hog))
            {
                p.input_hog = this;
            }
        }
        if (on_escape != null && ImpPlayer.Key_IsPressed(EInputKey.Key_Escape))
        {
            on_escape();
        }
    }
}

[ImpClass(Hidden = true)]
public class C2_DialogShade : C2_Box
{
    public Action on_click;

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt == ECursorEvent.Select_A)
        {
            on_click?.Invoke();
        }
    }
}
