using System.Numerics;
using System.Reflection;
using Editor.Windows;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Panel;

public class PNL_CommonComps : C2_Box
{
    C2_TabBox tabs_comps = new()
    {
        layout = TLayout2.FULL,
        tab_height = 24,
        tab_width = 64,
    };

    EdCommonCompsCategory tab_3d = new()
    {
        name = "3D",
        root_type = typeof(Imp3D),
    };

    EdCommonCompsCategory tab_2d = new()
    {
        name = "2D",
        root_type = typeof(Imp2D),
    };

    public PNL_CommonComps()
    {
        name = "Comps";
        cursor_filter = ECursorFilter.Pass;
        layout = TLayout2.FULL;

        tabs_comps.Child_Add(tab_3d);
        tabs_comps.Child_Add(tab_2d);
        Child_Add(tabs_comps);

        tab_3d.Rebuild();
        tab_2d.Rebuild();
    }
}

public class EdCommonCompsCategory : C2_List
{
    public Type root_type;

    Type _built_root;

    public EdCommonCompsCategory()
    {
        layout = TLayout2.FULL;
        orentation = EUIOrentation.H;
        is_scrollable = true;
        auto_scale_section_count = true;
        spacing = 4;
    }

    public override void OnUpdate(double dt)
    {
        if (root_type != _built_root)
        {
            Rebuild();
        }
        base.OnUpdate(dt);
    }

    public void Rebuild()
    {
        if (scroll_box != null)
        {
            scroll_box.Child_RemoveAll();
        }
        else
        {
            for (int i = children.Count - 1; i >= 0; i--)
            {
                if (children[i] is EdCommonCompTile)
                {
                    children[i].Destroy();
                }
            }
        }

        _built_root = root_type;
        if (root_type == null)
        {
            return;
        }

        List<Type> types = Types_Common(root_type);
        for (int i = 0; i < types.Count; i++)
        {
            Child_Add(new EdCommonCompTile(types[i]));
        }
    }

    public static List<Type> Types_Common(Type root)
    {
        List<Type> types = new();
        if (root == null)
        {
            return types;
        }

        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (C2_Tree.IsEditorAssembly(asm))
            {
                continue;
            }
            Type[] found;
            try
            {
                found = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                found = ex.Types.Where(t => t != null).ToArray();
            }
            for (int i = 0; i < found.Length; i++)
            {
                Type t = found[i];
                if (t == null || !t.IsClass || t.IsAbstract || t.ContainsGenericParameters)
                {
                    continue;
                }
                if (!root.IsAssignableFrom(t))
                {
                    continue;
                }
                if (C2_Tree.Class_IsHidden(t))
                {
                    continue;
                }
                if (!C2_Tree.Class_IsCommon(t))
                {
                    continue;
                }
                types.Add(t);
            }
        }

        types.Sort((a, b) => string.Compare(
            C2_Tree.Class_DisplayName(a),
            C2_Tree.Class_DisplayName(b),
            StringComparison.OrdinalIgnoreCase));
        return types;
    }
}

public class EdCommonCompTile : Imp2D
{
    public Type type;

    bool _hover;
    double _last_click;

    const float TileW = 72f;
    const float TileH = 86f;

    public EdCommonCompTile()
    {
        cursor_filter = ECursorFilter.Hit;
        layout.size = new Vector2(TileW, TileH);
        layout.size_min = layout.size;
    }

    public EdCommonCompTile(Type t) : this()
    {
        type = t;
        name = C2_Tree.Class_DisplayName(t);
    }

    public override bool CursorGrab_IsEnabled(ImpPlayer player)
    {
        return type != null;
    }

    public override object CursorGrab_Payload()
    {
        return type;
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt != ECursorEvent.Select_A || type == null)
        {
            return;
        }
        double now = Raylib.GetTime();
        bool dbl = now - _last_click < 0.35;
        _last_click = now;
        if (dbl)
        {
            WND_Scene.active?.scene_tree.AddComp(type);
        }
    }

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin)
        {
            _hover = true;
        }
        else if (notify == ENotifyGeneric.End)
        {
            _hover = false;
        }
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0)
        {
            return;
        }

        Color bg = _hover ? new Color(50, 50, 54, 255) : new Color(22, 22, 22, 255);
        Raylib.DrawRectangleV(dim.position, dim.size, bg);

        float pad = 4;
        float icon_area = MathF.Max(24, dim.size.Y - 28);
        Vector2 ipos = dim.position + new Vector2(pad, pad);
        Vector2 isz = new Vector2(dim.size.X - pad * 2, icon_area);

        A_Texture icon = C2_Tree.Class_Icon(type);
        if (icon != null && icon.texture.Id != 0 && icon.texture.Width > 0)
        {
            Texture2D tex = icon.texture;
            float side = MathF.Min(isz.X, isz.Y) * 0.72f;
            float dw = side;
            float dh = side;
            if (tex.Width >= tex.Height)
            {
                dh = side * (tex.Height / (float)tex.Width);
            }
            else
            {
                dw = side * (tex.Width / (float)tex.Height);
            }
            Raylib.DrawTexturePro(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(
                    ipos.X + (isz.X - dw) * 0.5f,
                    ipos.Y + (isz.Y - dh) * 0.5f,
                    dw, dh),
                Vector2.Zero, 0f, Color.White);
        }

        string label = name ?? "";
        UI_Text.LIGHT.Draw(label,
            new Vector2(dim.position.X + 3, dim.position.Y + dim.size.Y - 22),
            new Vector2(dim.size.X - 6, 20),
            11, ETextWrap.None,
            EUIPositionAlignment.Center, EUIPositionAlignment.Center);
    }
}
