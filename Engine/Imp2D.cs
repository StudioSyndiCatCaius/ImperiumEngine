using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;


public class Imp2D : ImpComp
{
    // #################################################################################
    // Static
    // #################################################################################

    public static void Draw_Rect(TBounds2 pos, TTransform2 offset, Color color)
    {
        if (!Draw_Resolve(pos, offset, out Vector2 dest, out Vector2 size, out float rot)) return;
        Rectangle rec = new(dest.X, dest.Y, size.X, size.Y);
        if (MathF.Abs(rot) < 1e-4f) Raylib.DrawRectangleRec(rec, color);
        else Raylib.DrawRectanglePro(rec, Vector2.Zero, rot, color);
    }

    public static void Draw_Texture(TBounds2 pos, TTransform2 offset, A_Texture texture, Color tint)
    {
        if (texture == null) return;
        Texture2D tex = texture.texture;
        if (tex.Id == 0 || tex.Width <= 0 || tex.Height <= 0) return;
        if (!Draw_Resolve(pos, offset, out Vector2 dest, out Vector2 size, out float rot)) return;
        Raylib.DrawTexturePro(tex,
            new Rectangle(0, 0, tex.Width, tex.Height),
            new Rectangle(dest.X, dest.Y, size.X, size.Y),
            Vector2.Zero, rot, tint);
    }

    public static void Draw_Text(TBounds2 pos, TTransform2 offset, string text, A_Font font, Color tint)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!Draw_Resolve(pos, offset, out Vector2 dest, out Vector2 size, out float rot)) return;
        Font f = font != null && font.font.Texture.Id != 0 ? font.font : Raylib.GetFontDefault();
        float font_size = size.Y;
        const float spacing = 1f;
        if (MathF.Abs(rot) < 1e-4f) Raylib.DrawTextEx(f, text, dest, font_size, spacing, tint);
        else Raylib.DrawTextPro(f, text, dest, Vector2.Zero, rot, font_size, spacing, tint);
    }

    // bounds are local; offset is the world transform (same compose as Imp2D).
    // scale 0 is treated as 1 so `default(TTransform2)` still draws.
    static bool Draw_Resolve(TBounds2 pos, TTransform2 offset, out Vector2 dest, out Vector2 size, out float rot)
    {
        if (offset.scale.X == 0f) offset.scale.X = 1f;
        if (offset.scale.Y == 0f) offset.scale.Y = 1f;
        Vector2 ext = pos.end - pos.start;
        size = ext * offset.scale;
        dest = WorldFromLocal(offset, new TTransform2 { position = pos.start }).position;
        rot = (float)offset.rotation;
        if (size.X < 0f) { dest.X += size.X; size.X = -size.X; }
        if (size.Y < 0f) { dest.Y += size.Y; size.Y = -size.Y; }
        return size.X > 0f && size.Y > 0f;
    }
    
    
    private static readonly Stack<Rectangle> _clip = new();

    public static void Clip_Push(TDimensions2 dim)
    {
        Vector2 dpi = Raylib.GetWindowScaleDPI();
        if (dpi.X <= 0) dpi.X = 1;
        if (dpi.Y <= 0) dpi.Y = 1;
        float x = dim.position.X, y = dim.position.Y, w = dim.size.X, h = dim.size.Y;
        if (_clip.Count > 0)
        {
            Rectangle p = _clip.Peek();
            float x2 = MathF.Min(x + w, p.X + p.Width);
            float y2 = MathF.Min(y + h, p.Y + p.Height);
            x = MathF.Max(x, p.X);
            y = MathF.Max(y, p.Y);
            w = x2 - x;
            h = y2 - y;
        }
        if (w < 0) w = 0;
        if (h < 0) h = 0;
        Rectangle r = new(x, y, w, h);
        _clip.Push(r);
        Raylib.BeginScissorMode(
            (int)MathF.Floor(r.X * dpi.X),
            (int)MathF.Floor(r.Y * dpi.Y),
            (int)MathF.Ceiling(r.Width * dpi.X),
            (int)MathF.Ceiling(r.Height * dpi.Y));
    }

    public static void Clip_Pop()
    {
        if (_clip.Count == 0) return;
        _clip.Pop();
        if (_clip.Count == 0)
        {
            Raylib.EndScissorMode();
            return;
        }
        Rectangle r = _clip.Peek();
        Vector2 dpi = Raylib.GetWindowScaleDPI();
        if (dpi.X <= 0) dpi.X = 1;
        if (dpi.Y <= 0) dpi.Y = 1;
        Raylib.BeginScissorMode(
            (int)MathF.Floor(r.X * dpi.X),
            (int)MathF.Floor(r.Y * dpi.Y),
            (int)MathF.Ceiling(r.Width * dpi.X),
            (int)MathF.Ceiling(r.Height * dpi.Y));
    }

    // Turns the GL matrix about the dimension's pivot so plain unrotated draw calls
    // (nine-slice patches, tiles, text) come out rotated. Always pair with Rotate_Pop.

    public static bool Rotate_Push(TDimensions2 dim)
    {
        if (!dim.IsRotated) return false;
        Vector2 o = dim.Pivot_Point;
        Rlgl.PushMatrix();
        Rlgl.Translatef(o.X, o.Y, 0f);
        Rlgl.Rotatef(dim.rotation, 0f, 0f, 1f);
        Rlgl.Translatef(-o.X, -o.Y, 0f);
        return true;
    }

    public static void Rotate_Pop(bool pushed)
    {
        if (pushed) Rlgl.PopMatrix();
    }

    // reverse draw order: last child first, children before self

    public static Imp2D? Trace_Point(Vector2 pos, ImpComp node)
    {
        if (node == null || !node.is_visible) return null;
        if (node is Imp2D c2 && c2.cursor_filter == ECursorFilter.Ignore) return null;

        for (int i = node.children.Count - 1; i >= 0; i--)
        {
            Imp2D? hit = Trace_Point(pos, node.children[i]);
            if (hit != null) return hit;
        }

        if (node is Imp2D self && self.cursor_filter == ECursorFilter.Hit)
        {
            if (self.Dimensions_Get().Contains(pos)) return self;
        }
        return null;
    }

    // #################################################################################
    // Class
    // #################################################################################
    [ImpVar] public TTransform2 transform = new();
    [ImpVar] public Vector2 position;
    
    [ImpVar] public float stretch_ratio=1.0f;
    [ImpVar] public TLayout2 layout=new();
    
    [ImpVar] public bool clip_children;
    [ImpVar] public Vector2 pivot=Vector2.Zero;
    [ImpVar] public bool normalize_pivot; // if true, pivot is normalized to [0,1], instead of absolute position
    
    [ImpVar][Category("Cursor")] public ECursorFilter cursor_filter=ECursorFilter.Pass;

    static bool _scene_draw;
    static TCamera2D _scene_cam;
    static Vector2 _scene_view;
    static Vector2 _scene_canvas = new(1920, 1080);
    static ImpComp _scene_root;

    // ------------------------------------------------------------
    // Layout cache - probably remove this later
    // ------------------------------------------------------------
    //
    // Every layout primitive below re-walks the parent chain, so they compose into
    // O(depth^3) for a single Transform_Get/Dimensions_Get - and those get queried
    // several times per comp per frame (layout, draw, hit test, gizmo). Memoizing
    // against a global epoch makes repeat queries within one epoch O(1).

    static uint _layout_epoch = 1;

    // Cached value + the epoch it was computed at. Excluded from cloning (see
    // CloneFields) so a fresh copy always recomputes rather than inheriting a
    // stamp that looks current.
    uint _e_scene, _e_size, _e_anchor, _e_xform, _e_dim, _e_bounds;
    bool _c_scene;
    Vector2 _c_size, _c_anchor;
    TTransform2 _c_xform;
    TDimensions2 _c_dim;
    TBounds2 _c_bounds;

    /// <summary>
    /// Drops every cached layout value. Must be called after anything moves, resizes
    /// or reparents a comp outside of that comp's own OnUpdate.
    ///
    /// The transform setters, the structural Child_*/Detach/Destroy calls, the scene
    /// layout statics and the gizmo drag all invalidate for themselves. What cannot be
    /// intercepted is a direct field write - `comp.layout.size = x`, `comp.transform.position = y` -
    /// since those are public fields. The per-phase bumps in ImpApp.Run and the per-OnUpdate
    /// bump in ImpComp.Update are the safety net that keeps those sound: a cached value
    /// never outlives the OnUpdate body that could have invalidated it. New code that
    /// mutates layout outside an OnUpdate must call this itself.
    /// </summary>
    public static void Layout_Invalidate()
    {
        // Skip 0 on wrap: the per-instance stamps default to 0, so an epoch of 0
        // would read as a hit on a comp that has never computed anything.
        if (++_layout_epoch == 0) _layout_epoch = 1;
        if (ImpProfiler.enabled) ImpProfiler.epoch_bumps++;
    }

    /// <summary>
    /// Marks the hierarchy laid out as scene content rather than editor chrome. Scene comps
    /// anchor against the canvas, honour their pivot, and rotate. Set once per frame by the
    /// scene view so layout matches whether it is queried during update, hit test, or draw.
    /// </summary>
    public static void SceneLayout_Set(ImpComp root, Vector2 canvas)
    {
        _scene_root = root;
        if (canvas.X > 1f && canvas.Y > 1f) _scene_canvas = canvas;
        Layout_Invalidate();
    }

    public static void SceneDraw_Begin(TCamera2D cam, Vector2 view_size)
    {
        _scene_draw = true;
        _scene_cam = cam;
        _scene_view = view_size;
        Layout_Invalidate();
    }

    public static void SceneDraw_End()
    {
        _scene_draw = false;
        Layout_Invalidate();
    }

    /// <summary>True when this comp is scene content (see SceneLayout_Set).</summary>
    public bool Scene_IsContent()
    {
        if (ImpProfiler.enabled) ImpProfiler.scene_calls++;
        if (_e_scene == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.scene_hits++;
            return _c_scene;
        }
        _e_scene = _layout_epoch;

        _c_scene = false;
        if (_scene_root != null)
        {
            for (ImpComp n = this; n != null; n = n.parent)
                if (n == _scene_root) { _c_scene = true; break; }
        }
        return _c_scene;
    }

    /// <summary>Local-space size after Fill resolves against the parent rect (canvas at the root).</summary>
    public Vector2 Size_Layout()
    {
        if (ImpProfiler.enabled) ImpProfiler.size_calls++;
        if (_e_size == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.size_hits++;
            return _c_size;
        }
        _e_size = _layout_epoch;

        if (!Scene_IsContent()) return _c_size = layout.size;
        Vector2 view = parent is Imp2D p ? p.Size_Layout() : _scene_canvas;
        return _c_size = new Vector2(
            layout.orient_H == EUIViewportAlignment.Fill ? view.X : layout.size.X,
            layout.orient_V == EUIViewportAlignment.Fill ? view.Y : layout.size.Y);
    }

    /// <summary>Local-space offset the view orentation adds on top of transform.position.</summary>
    public Vector2 Anchor_Offset()
    {
        if (ImpProfiler.enabled) ImpProfiler.anchor_calls++;
        if (_e_anchor == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.anchor_hits++;
            return _c_anchor;
        }
        _e_anchor = _layout_epoch;

        if (!Scene_IsContent()) return _c_anchor = Vector2.Zero;
        Vector2 view = parent is Imp2D p ? p.Size_Layout() : _scene_canvas;
        Vector2 own = Size_Layout();
        return _c_anchor = new Vector2(
            Anchor(layout.orient_H, view.X, own.X), Anchor(layout.orient_V, view.Y, own.Y));
    }

    static float Anchor(EUIViewportAlignment a, float view, float own) => a switch
    {
        EUIViewportAlignment.Center => (view - own) * 0.5f,
        EUIViewportAlignment.End => view - own,
        _ => 0f,
    };

    /// <summary>Pivot as a local-space offset from the box's top-left corner.</summary>
    public Vector2 Pivot_Local()
    {
        return normalize_pivot ? pivot * Size_Layout() : pivot;
    }
    
    
    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (layout.size_max != Vector2.Zero) { layout.size=Vector2.Clamp(layout.size, layout.size_min, layout.size_max); }
        else { layout.size=Vector2.Max(layout.size, layout.size_min); }
    }
    

    // ------------------------------------------------------------
    // Option
    // ------------------------------------------------------------
    [ImpVar][Category("Option")] public C2_Button option_button;
    
    public Action<Imp2D> as_option_select;
    public Action<Imp2D> as_option_hover;
    public Action<Imp2D> as_option_unhover;
    
    // ------------------------------------------------------------
    // Layout/Position
    // ------------------------------------------------------------

    //bounds in absolute space used for drawing this comps elements 
    public TBounds2 Bounds_Get()
    {
        if (_e_bounds == _layout_epoch && ImpProfiler.layout_cache)
            return _c_bounds;
        _e_bounds = _layout_epoch;

        TBounds2 parent_bounds;
        if (parent is Imp2D p)
            parent_bounds = p.Bounds_GetForChild(p.children.IndexOf(this));
        else
            parent_bounds = new TBounds2
            {
                start = Vector2.Zero,
                end = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()),
            };
        return _c_bounds = new TBounds2(position, layout, parent_bounds);
    }
    
    //gets the bounds of a child2d component
    public virtual TBounds2 Bounds_GetForChild(int index)
    {
        return Bounds_Get();
    }
    
    public TDimensions2 Dimensions_Get()
    {
        if (ImpProfiler.enabled) ImpProfiler.dim_calls++;
        if (_e_dim == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.dim_hits++;
            return _c_dim;
        }
        _e_dim = _layout_epoch;

        if (_scene_draw && Scene_IsContent())
        {
            TTransform2 world = Transform_Get(true);
            Vector2 sc = world.scale;
            float z = _scene_cam.zoom <= 1e-6f ? 1f : _scene_cam.zoom;
            Vector2 px_size = Size_Layout() * new Vector2(MathF.Abs(sc.X), MathF.Abs(sc.Y)) * z;
            Vector2 px_pivot = Pivot_Local() * new Vector2(MathF.Abs(sc.X), MathF.Abs(sc.Y)) * z;

            // transform.position is the pivot point, so the box hangs off it: a normalized
            // pivot of 1,1 puts the box up and left of the same point 0,0 puts it down-right of.
            Vector2 pivot_view = ImpGizmo.WorldToView(world.position, _scene_cam, _scene_view);
            return _c_dim = new TDimensions2
            {
                position = pivot_view - px_pivot,
                size = px_size,
                rotation = (float)world.rotation,
                pivot = px_pivot,
            };
        }

        View_Get(out Vector2 vs, out Vector2 vsz);
        Vector2 pos = transform.position;

        // Inside a C2_List / C2_ScrollBox, Fill on the main axis uses size set by
        // the list (stretch_ratio share). Cross-axis Fill and Fill outside a list
        // still expand to the parent view. The list's own scroll_box is a viewport.
        C2_List parent_list = parent as C2_List;
        C2_ScrollBox parent_scroll = parent as C2_ScrollBox;
        bool is_list_scroll = parent_list != null && parent_list.scroll_box == this;
        EUIOrentation layout_align = EUIOrentation.V;
        bool in_layout = false;
        if (parent_list != null && !is_list_scroll)
        {
            in_layout = true;
            layout_align = parent_list.orentation;
        }
        else if (parent_scroll != null)
        {
            in_layout = true;
            layout_align = parent_scroll.orentation;
        }
        bool list_main_h = in_layout && layout_align == EUIOrentation.H;
        bool list_main_v = in_layout && layout_align == EUIOrentation.V;

        Vector2 sz = new(
            layout.orient_H == EUIViewportAlignment.Fill
                ? (list_main_h ? layout.size.X : vsz.X)
                : layout.size.X,
            layout.orient_V == EUIViewportAlignment.Fill
                ? (list_main_v ? layout.size.Y : vsz.Y)
                : layout.size.Y);
        Vector2 place = new(
            Align(layout.orient_H, vs.X, vsz.X, sz.X, pos.X),
            Align(layout.orient_V, vs.Y, vsz.Y, sz.Y, pos.Y));
        // Same rule as scene content: the placed point is the pivot, the box hangs off it.
        Vector2 pv = normalize_pivot ? pivot * sz : pivot;
        return _c_dim = new TDimensions2
        {
            position = place - pv,
            size = sz,
            rotation = (float)transform.rotation,
            pivot = pv,
        };
    }

    void View_Get(out Vector2 start, out Vector2 view_size)
    {
        if (parent is Imp2D p)
        {
            var d = p.Dimensions_Get();
            start = d.position;
            view_size = d.size;
        }
        else
        {
            start = Vector2.Zero;
            view_size = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        }
    }

    static float Align(EUIViewportAlignment a, float view_start, float view_size, float self_size, float offset) => a switch
    {
        EUIViewportAlignment.Center => view_start + (view_size - self_size) * 0.5f + offset,
        EUIViewportAlignment.End => view_start + view_size - self_size - offset,
        EUIViewportAlignment.Fill => view_start + offset,
        _ => view_start + offset,
    };
    
    // ------------------------------------------------------------
    // Transform
    // ------------------------------------------------------------

    public void Transform_Set(TTransform2 value, bool global = true)
    {
        TTransform2 local = !global || parent is not Imp2D p
            ? value
            : LocalFromWorld(p.Transform_Get(true), value);
        local.position -= Anchor_Offset();
        transform = local;
        Layout_Invalidate();
    }

    public TTransform2 Transform_Get(bool global = true)
    {
        // Only the global result is cached. The local one is transform + Anchor_Offset(),
        // and Anchor_Offset carries its own cache, so there is nothing left to save.
        if (global)
        {
            if (ImpProfiler.enabled) ImpProfiler.xform_calls++;
            if (_e_xform == _layout_epoch && ImpProfiler.layout_cache)
            {
                if (ImpProfiler.enabled) ImpProfiler.xform_hits++;
                return _c_xform;
            }
            _e_xform = _layout_epoch;
        }

        TTransform2 local = transform;
        local.position += Anchor_Offset();
        if (!global) return local;
        if (parent is not Imp2D p) return _c_xform = local;
        return _c_xform = WorldFromLocal(p.Transform_Get(true), local);
    }

    public Vector2 Position_Get(bool global = true)
    {
        return Transform_Get(global).position;
    }
    public void Position_Set(Vector2 position, bool global = true)
    {
        if (!global || parent is not Imp2D p)
        {
            transform.position = position - Anchor_Offset();
            Layout_Invalidate();
            return;
        }
        var parent_w = p.Transform_Get(true);
        TTransform2 world = new()
        {
            position = position,
            rotation = parent_w.rotation + transform.rotation,
            scale = parent_w.scale * transform.scale,
        };
        transform.position = LocalFromWorld(parent_w, world).position - Anchor_Offset();
        Layout_Invalidate();
    }
    public float Rotation_Get(bool global = true)
    {
        return (float)Transform_Get(global).rotation;
    }
    public void Rotation_Set(float rotation, bool global = true)
    {
        if (!global || parent is not Imp2D p)
            transform.rotation = rotation;
        else
            transform.rotation = rotation - p.Transform_Get(true).rotation;
        Layout_Invalidate();
    }
    public Vector2 Scale_Get(bool global = true)
    {
        return Transform_Get(global).scale;
    }
    public void Scale_Set(Vector2 scale, bool global = true)
    {
        if (!global || parent is not Imp2D p)
        {
            transform.scale = scale;
            Layout_Invalidate();
            return;
        }
        var ps = p.Transform_Get(true).scale;
        transform.scale = new Vector2(
            ps.X != 0 ? scale.X / ps.X : 0,
            ps.Y != 0 ? scale.Y / ps.Y : 0);
        Layout_Invalidate();
    }

    static TTransform2 WorldFromLocal(TTransform2 parent, TTransform2 local)
    {
        float rad = (float)(parent.rotation * (Math.PI / 180.0));
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        Vector2 scaled = local.position * parent.scale;
        return new TTransform2
        {
            position = parent.position + new Vector2(scaled.X * c - scaled.Y * s, scaled.X * s + scaled.Y * c),
            rotation = parent.rotation + local.rotation,
            scale = parent.scale * local.scale
        };
    }

    static TTransform2 LocalFromWorld(TTransform2 parent, TTransform2 world)
    {
        Vector2 inv_s = new(
            parent.scale.X != 0 ? 1f / parent.scale.X : 0,
            parent.scale.Y != 0 ? 1f / parent.scale.Y : 0);
        float rad = (float)(-parent.rotation * (Math.PI / 180.0));
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        Vector2 d = world.position - parent.position;
        return new TTransform2
        {
            position = new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c) * inv_s,
            rotation = world.rotation - parent.rotation,
            scale = world.scale * inv_s
        };
    }
}