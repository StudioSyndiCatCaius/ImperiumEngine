using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public struct TPickerOption
{
    public string name;
    public string detail;
    public object data;
    public A_Texture icon;
    public Color tint;

    public TPickerOption(string name, object data)
    {
        this.name = name;
        this.data = data;
        detail = "";
        icon = null;
        tint = Color.Blank;
    }
}

public class C2_Picker : Imp2D
{
    const int PopupRowsMax = 10;
    const float PopupMinW = 260f;
    const float ChipW = 3f;

    public string placeholder = "None";
    public string text = "";
    public Func<string> text_get;
    public Color tint = Color.Blank;
    public Func<Color> tint_get;
    public A_Texture icon;
    public bool show_clear = true;

    public Func<List<TPickerOption>> options_build;
    public Action<TPickerOption> on_picked;
    public Action on_cleared;
    public Action<string> on_dropped;
    //Gates what a drag may drop here, so a texture can't land in a mesh slot.
    public Func<string, bool> drop_accepts;

    bool _open;
    bool _hover;
    bool _drop_hover;
    C2_PickerPopup _popup;

    public bool IsOpen => _open;

    public C2_Picker()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
    }

    public static List<TPickerOption> Options_Assets(Type type)
    {
        List<TPickerOption> list = new();
        foreach (var (path, cls) in ImpAsset.Files_OfType(type))
        {
            Type found = ImpAsset.AssetType_FromName(cls);
            list.Add(new TPickerOption(Path.GetFileNameWithoutExtension(path), path)
            {
                detail = cls,
                icon = A_Texture.THUMB_FILE,
                tint = ImpAsset.Color_ForType(found ?? type),
            });
        }
        // Engine built-ins have no .ImpAsset file, so the file scan alone would only ever
        // list the loaded game's assets.
        foreach (var (key, asset) in ImpAsset.Builtins_OfType(type))
        {
            Type found = asset.GetType();
            list.Add(new TPickerOption(ImpAsset.Name_ForPath(key), ImpAsset.BuiltinPrefix + key)
            {
                detail = found.Name,
                icon = A_Texture.THUMB_FILE,
                tint = ImpAsset.Color_ForType(found),
            });
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static List<TPickerOption> Options_Classes(Type root)
    {
        List<TPickerOption> list = new();
        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] all;
            try { all = asm.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e)
            { all = e.Types.Where(t => t != null).ToArray(); }
            foreach (Type t in all)
            {
                if (t == null || t.IsAbstract || !root.IsAssignableFrom(t)) continue;
                list.Add(new TPickerOption(t.Name, t)
                {
                    detail = t.Namespace ?? "",
                    icon = Icon_For(t),
                    tint = ImpAsset.Color_ForType(t),
                });
            }
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    static A_Texture Icon_For(Type t)
    {
        if (typeof(Imp3D).IsAssignableFrom(t)) return A_Texture.ICO_COMP3D;
        if (typeof(Imp2D).IsAssignableFrom(t)) return A_Texture.ICO_COMP2D;
        if (typeof(ImpComp).IsAssignableFrom(t)) return A_Texture.ICO_COMP;
        return A_Texture.THUMB_FILE;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (text_get != null) text = text_get() ?? "";
        if (tint_get != null) tint = tint_get();

        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        UiStyle_Box bg = _open ? UiStyle_Box.STYLE_BTN_PRESS
            : _hover ? UiStyle_Box.STYLE_BTN_HOVER
            : UiStyle_Box.STYLE_BKG_MID;
        bg.Draw(dim);

        float x = dim.position.X + 4;
        if (tint.A > 0)
        {
            Raylib.DrawRectangleV(new Vector2(dim.position.X + 2, dim.position.Y + 3),
                new Vector2(ChipW, dim.size.Y - 6), tint);
            x += ChipW + 2;
        }
        if (icon != null && icon.texture.Id != 0)
        {
            float s = MathF.Max(0, dim.size.Y - 6);
            Raylib.DrawTexturePro(icon.texture,
                new Rectangle(0, 0, icon.texture.Width, icon.texture.Height),
                new Rectangle(x, dim.position.Y + (dim.size.Y - s) * 0.5f, s, s),
                Vector2.Zero, 0f, Color.White);
            x += s + 4;
        }

        bool set = !string.IsNullOrEmpty(text);
        float clear_w = show_clear && set ? 16 : 0;
        UI_Text ts = set ? UI_Text.LIGHT : UI_Text.MUTED;
        ts.Draw(set ? text : placeholder, new Vector2(x, dim.position.Y),
            new Vector2(MathF.Max(0, dim.position.X + dim.size.X - clear_w - 4 - x), dim.size.Y),
            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);

        if (_drop_hover)
        {
            Raylib.DrawRectangleLinesEx(
                new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 2f,
                new Color(90, 220, 140, 255));
        }

        if (clear_w > 0)
        {
            float cx = dim.position.X + dim.size.X - 8;
            float cy = dim.position.Y + dim.size.Y * 0.5f;
            Color col = _hover ? Color.White : new Color(180, 180, 180, 255);
            Raylib.DrawLineEx(new Vector2(cx - 3, cy - 3), new Vector2(cx + 3, cy + 3), 1.5f, col);
            Raylib.DrawLineEx(new Vector2(cx - 3, cy + 3), new Vector2(cx + 3, cy - 3), 1.5f, col);
        }
    }

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin) _hover = true;
        else if (notify == ENotifyGeneric.End) _hover = false;
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt != ECursorEvent.Select_A) return;
        TDimensions2 dim = Dimensions_Get();
        if (show_clear && !string.IsNullOrEmpty(text)
            && player.cursor.position.X >= dim.position.X + dim.size.X - 16)
        {
            Open_Set(false);
            on_cleared?.Invoke();
            return;
        }
        Open_Set(!_open);
    }

    /// <summary>True when a drag carrying this path may be dropped here.</summary>
    public bool Drop_Accepts(string path)
    {
        if (on_dropped == null || string.IsNullOrEmpty(path)) return false;
        return drop_accepts == null || drop_accepts(path);
    }

    string Drop_Path(ImpComp dropped)
    {
        string path = dropped?.CursorGrab_Payload() as string;
        return Drop_Accepts(path) ? path : null;
    }

    public override void _Notify_OnGrabDrop(ImpPlayer player, ENotifyGrabTarget notify, ImpComp other, double dt)
    {
        base._Notify_OnGrabDrop(player, notify, other, dt);
        if (notify == ENotifyGrabTarget.Hover_AsInstigator_Start)
            _drop_hover = Drop_Path(other) != null;
        else if (notify == ENotifyGrabTarget.Hover_AsInstigator_End)
            _drop_hover = false;
        else if (notify == ENotifyGrabTarget.Drop_AsInstigator)
        {
            _drop_hover = false;
            string path = Drop_Path(other);
            if (path != null) on_dropped(path);
        }
    }

    void Open_Set(bool open)
    {
        if (open == _open) return;
        _open = open;
        if (!open)
        {
            if (_popup != null && ImpPlayer.players.Count > 0 && ImpPlayer.players[0].input_hog == _popup)
                ImpPlayer.players[0].input_hog = null;
            _popup?.Destroy();
            _popup = null;
            return;
        }

        ImpComp host = C2_MenuBar.PopupHost();
        if (host == null) { _open = false; return; }
        TDimensions2 dim = Dimensions_Get();
        List<TPickerOption> opts = options_build != null ? options_build() : new List<TPickerOption>();
        _popup = new C2_PickerPopup(this, opts)
        {
            layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Start,
                    orient_V = EUIViewportAlignment.Start,
                },
        };
        _popup.transform.position = new Vector2(dim.position.X, dim.position.Y + dim.size.Y + 2);
        host.Child_Add(_popup);
    }

    internal void Pick(TPickerOption opt)
    {
        Open_Set(false);
        on_picked?.Invoke(opt);
    }

    internal void Close() => Open_Set(false);
}

class C2_PickerPopup : C2_Box
{
    readonly C2_Picker _owner;
    readonly List<TPickerOption> _all;
    readonly List<TPickerOption> _shown = new();
    string _filter = "";
    float _scroll;
    const int MaxRows = 10;
    const float RowH = 22f;

    public C2_PickerPopup(C2_Picker owner, List<TPickerOption> options)
    {
        _owner = owner;
        _all = options ?? new List<TPickerOption>();
        cursor_filter = ECursorFilter.Hit;
        style = new UiStyle_Box { texture = null, tint = new Color(36, 36, 36, 255) };
        Rebuild();
    }

    void Rebuild()
    {
        _shown.Clear();
        foreach (TPickerOption o in _all)
        {
            if (_filter.Length > 0
                && (o.name == null || o.name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                && (o.detail == null || o.detail.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0))
                continue;
            _shown.Add(o);
        }
        int rows = Math.Min(_shown.Count, MaxRows);
        layout.size = new Vector2(MathF.Max(260, _owner.Dimensions_Get().size.X), RowH + MathF.Max(RowH, rows * RowH));
        layout.size_min = layout.size;
        _scroll = Math.Clamp(_scroll, 0, MathF.Max(0, _shown.Count - MaxRows));
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer p = ImpPlayer.players[0];
        p.input_hog = this;

        int ch = Raylib.GetCharPressed();
        bool changed = false;
        while (ch > 0)
        {
            if (ch >= 32) { _filter += char.ConvertFromUtf32(ch); changed = true; }
            ch = Raylib.GetCharPressed();
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Backspace) && _filter.Length > 0)
        {
            _filter = _filter[..^1];
            changed = true;
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Escape)) { _owner.Close(); return; }
        if (changed) Rebuild();

        TDimensions2 dim = Dimensions_Get();
        if (p.Cursor_IsInDimensions(dim))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0) _scroll = Math.Clamp(_scroll - wheel, 0, MathF.Max(0, _shown.Count - MaxRows));
        }

        if (!ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left)) return;
        if (!p.Cursor_IsInDimensions(dim))
        {
            if (p.target_cursor != _owner) _owner.Close();
            return;
        }

        int i = (int)_scroll + (int)((p.cursor.position.Y - dim.position.Y - RowH) / RowH);
        if (i >= 0 && i < _shown.Count) _owner.Pick(_shown[i]);
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        Raylib.DrawRectangleLinesEx(new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 1, new Color(0, 120, 215, 255));

        UiStyle_Box.STYLE_BKG_DARK.Draw(new TDimensions2 { position = dim.position, size = new Vector2(dim.size.X, RowH) });
        UI_Text ts = _filter.Length > 0 ? UI_Text.LIGHT : UI_Text.MUTED;
        ts.Draw(_filter.Length > 0 ? _filter + "|" : "Type to search",
            dim.position + new Vector2(6, 0), new Vector2(dim.size.X - 12, RowH),
            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);

        if (_shown.Count == 0)
        {
            UI_Text.MUTED.Draw(_all.Count == 0 ? "Nothing to pick" : "No matches",
                dim.position + new Vector2(0, RowH), new Vector2(dim.size.X, dim.size.Y - RowH),
                0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Center);
            return;
        }

        Vector2 mouse = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
        int first = (int)_scroll;
        int last = Math.Min(_shown.Count, first + MaxRows);
        for (int i = first; i < last; i++)
        {
            TPickerOption o = _shown[i];
            var r = new Rectangle(dim.position.X, dim.position.Y + RowH + (i - first) * RowH, dim.size.X, RowH);
            if (Raylib.CheckCollisionPointRec(mouse, r))
                Raylib.DrawRectangleRec(r, new Color(0, 96, 166, 255));
            float x = r.X + 6;
            if (o.tint.A > 0)
            {
                Raylib.DrawRectangleV(new Vector2(r.X + 2, r.Y + 3), new Vector2(3, r.Height - 6), o.tint);
                x += 6;
            }
            if (o.icon != null && o.icon.texture.Id != 0)
            {
                float s = 14;
                Raylib.DrawTexturePro(o.icon.texture, new Rectangle(0, 0, o.icon.texture.Width, o.icon.texture.Height),
                    new Rectangle(x, r.Y + (r.Height - s) * 0.5f, s, s), Vector2.Zero, 0, Color.White);
                x += s + 4;
            }
            float dw = string.IsNullOrEmpty(o.detail) ? 0 : 110;
            UI_Text.LIGHT.Draw(o.name, new Vector2(x, r.Y), new Vector2(MathF.Max(0, r.Width - dw - (x - r.X) - 6), r.Height),
                0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);
            if (dw > 0)
                UI_Text.MUTED.Draw(o.detail, new Vector2(r.X + r.Width - dw - 6, r.Y), new Vector2(dw, r.Height),
                    0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.End);
        }
    }

}
