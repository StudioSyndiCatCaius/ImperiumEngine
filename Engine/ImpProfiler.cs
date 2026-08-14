using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Raylib_cs;

namespace ImperiumEngine;

/// <summary>
/// Frame timing and layout-cache instrumentation. Toggled with F3.
/// Everything is guarded by <see cref="enabled"/> so the cost when off is one
/// predictable branch. The overlay draws with raylib's default font rather than
/// C2_Text so the HUD never shows up in its own measurements.
/// </summary>
public static class ImpProfiler
{
    public enum EPhase { Input, Update, Cursor, Draw3D, Draw2D, COUNT }

    public static bool enabled;

    /// <summary>
    /// F4. Turns the ImpComp2D layout cache off so the old uncached cost can be measured
    /// against the new one in the same session, on the same scene, without a rebuild.
    /// Purely a measurement aid - results are identical either way, only the cost differs.
    /// </summary>
    public static bool layout_cache = true;

    // ------------------------------------------------------------
    // Phase timing
    // ------------------------------------------------------------

    static readonly Stopwatch _sw = Stopwatch.StartNew();
    static readonly double[] _acc = new double[(int)EPhase.COUNT];
    static readonly double[] _avg = new double[(int)EPhase.COUNT];
    static readonly string[] _names = { "input", "update", "cursor", "draw3d", "draw2d" };

    static double _phase_t0;
    static EPhase _phase = EPhase.COUNT;
    static double _frame_t0, _frame_ms, _frame_avg;

    // Exponential moving average over ~30 frames, so the numbers sit still
    // long enough to read instead of strobing every frame.
    const double Smooth = 1.0 / 30.0;

    static double Now => _sw.Elapsed.TotalMilliseconds;

    /// <summary>Exposed so ImpComp.Draw can time a single OnDraw* call without allocating its own Stopwatch.</summary>
    public static double Now_Ms => Now;

    // ------------------------------------------------------------
    // Layout cache counters
    // ------------------------------------------------------------

    public static long dim_calls, dim_hits;
    public static long xform_calls, xform_hits;
    public static long size_calls, size_hits;
    public static long anchor_calls, anchor_hits;
    public static long scene_calls, scene_hits;
    public static long epoch_bumps;
    public static long comp_count;

    static double a_dim_calls, a_dim_hits;
    static double a_xform_calls, a_xform_hits;
    static double a_size_calls, a_size_hits;
    static double a_anchor_calls, a_anchor_hits;
    static double a_scene_calls, a_scene_hits;
    static double a_epoch_bumps, a_comp_count;

    static double Ema(double avg, double sample) => avg + (sample - avg) * Smooth;

    // ------------------------------------------------------------
    // Per-type draw cost
    // ------------------------------------------------------------

    // Raw, single-frame totals - not EMA smoothed. A spike is already visible on the
    // frame it happens, and smoothing here would blur out which type caused it, which
    // is the only thing this block exists to answer.
    static readonly Dictionary<Type, double> _draw_ms = new();

    // C2_SceneView owns the 3D viewport (R3D.End + blit + gizmo). Split so the
    // 80ms+ "C2_SceneView" line can be told apart: submit vs GPU pass vs overlay.
    public static double sv_rt, sv_r3d, sv_blit, sv_gizmo;

    /// <summary>Adds time spent inside one comp's own OnDraw2D/OnDraw3D/OnDraw2DForeground call, keyed by its type.</summary>
    public static void DrawType_Add(Type t, double ms)
    {
        _draw_ms.TryGetValue(t, out double v);
        _draw_ms[t] = v + ms;
    }

    // ------------------------------------------------------------
    // Frame
    // ------------------------------------------------------------

    public static void Frame_Begin()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F3)) enabled = !enabled;
        if (Raylib.IsKeyPressed(KeyboardKey.F4)) layout_cache = !layout_cache;
        if (!enabled) return;

        _frame_t0 = Now;
        for (int i = 0; i < (int)EPhase.COUNT; i++) _acc[i] = 0;
        dim_calls = dim_hits = 0;
        xform_calls = xform_hits = 0;
        size_calls = size_hits = 0;
        anchor_calls = anchor_hits = 0;
        scene_calls = scene_hits = 0;
        epoch_bumps = 0;
        comp_count = 0;
        sv_rt = sv_r3d = sv_blit = sv_gizmo = 0;
        _draw_ms.Clear();
    }

    public static void Frame_End()
    {
        if (!enabled) return;

        _frame_ms = Now - _frame_t0;
        _frame_avg = Ema(_frame_avg, _frame_ms);
        for (int i = 0; i < (int)EPhase.COUNT; i++) _avg[i] = Ema(_avg[i], _acc[i]);

        a_dim_calls = Ema(a_dim_calls, dim_calls);
        a_dim_hits = Ema(a_dim_hits, dim_hits);
        a_xform_calls = Ema(a_xform_calls, xform_calls);
        a_xform_hits = Ema(a_xform_hits, xform_hits);
        a_size_calls = Ema(a_size_calls, size_calls);
        a_size_hits = Ema(a_size_hits, size_hits);
        a_anchor_calls = Ema(a_anchor_calls, anchor_calls);
        a_anchor_hits = Ema(a_anchor_hits, anchor_hits);
        a_scene_calls = Ema(a_scene_calls, scene_calls);
        a_scene_hits = Ema(a_scene_hits, scene_hits);
        a_epoch_bumps = Ema(a_epoch_bumps, epoch_bumps);
        a_comp_count = Ema(a_comp_count, comp_count);
    }

    public static void Phase_Begin(EPhase phase)
    {
        if (!enabled) return;
        Phase_End();
        _phase = phase;
        _phase_t0 = Now;
    }

    public static void Phase_End()
    {
        if (!enabled || _phase == EPhase.COUNT) return;
        _acc[(int)_phase] += Now - _phase_t0;
        _phase = EPhase.COUNT;
    }

    // ------------------------------------------------------------
    // Overlay
    // ------------------------------------------------------------

    public static void Draw()
    {
        if (!enabled) return;
        Phase_End();

        Font f = Raylib.GetFontDefault();
        const float fs = 18f, sp = 1f, line = 20f;

        string[] rows =
        {
            $"frame  {_frame_avg,6:0.00} ms   ({(_frame_avg > 0.0001 ? 1000.0 / _frame_avg : 0),5:0} fps)",
            $"input  {_avg[(int)EPhase.Input],6:0.00} ms",
            $"update {_avg[(int)EPhase.Update],6:0.00} ms",
            $"cursor {_avg[(int)EPhase.Cursor],6:0.00} ms",
            $"draw3d {_avg[(int)EPhase.Draw3D],6:0.00} ms",
            $"draw2d {_avg[(int)EPhase.Draw2D],6:0.00} ms",
            "",
            $"comps  {a_comp_count,8:0}",
            $"epochs {a_epoch_bumps,8:0}",
            "",
            layout_cache ? "layout cache   calls     hit%" : "layout cache  OFF (F4)  calls",
            Row("dim", a_dim_calls, a_dim_hits),
            Row("xform", a_xform_calls, a_xform_hits),
            Row("size", a_size_calls, a_size_hits),
            Row("anchor", a_anchor_calls, a_anchor_hits),
            Row("scene", a_scene_calls, a_scene_hits),
        };

        var top_draw = _draw_ms.OrderByDescending(kv => kv.Value).Take(8).ToList();
        List<string> extra = new();
        if (sv_r3d > 0.01 || sv_blit > 0.01 || sv_gizmo > 0.01 || sv_rt > 0.01)
        {
            extra.Add("scene view (ms, this frame)");
            extra.Add($"  {"rt",-20} {sv_rt,7:0.00}");
            extra.Add($"  {"r3d",-20} {sv_r3d,7:0.00}");
            extra.Add($"  {"blit",-20} {sv_blit,7:0.00}");
            extra.Add($"  {"gizmo",-20} {sv_gizmo,7:0.00}");
        }
        string[] draw_rows = top_draw.Count == 0 && extra.Count == 0
            ? Array.Empty<string>()
            : extra
                .Concat(top_draw.Count == 0
                    ? Array.Empty<string>()
                    : new[] { "top draw types (ms, this frame)" }
                        .Concat(top_draw.Select(kv => $"  {Truncate(kv.Key.Name, 20),-20} {kv.Value,7:0.00}")))
                .ToArray();

        float w = 380f;
        float h = (rows.Length + draw_rows.Length) * line + 12f;
        Raylib.DrawRectangleV(new Vector2(8, 8), new Vector2(w, h), new Color(0, 0, 0, 190));

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].Length == 0) continue;
            Raylib.DrawTextEx(f, rows[i], new Vector2(16, 14 + i * line), fs, sp, Color.RayWhite);
        }
        for (int i = 0; i < draw_rows.Length; i++)
        {
            Raylib.DrawTextEx(f, draw_rows[i], new Vector2(16, 14 + (rows.Length + i) * line), fs, sp, Color.RayWhite);
        }
    }

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    static string Row(string label, double calls, double hits)
    {
        double pct = calls > 0.0001 ? hits / calls * 100.0 : 0.0;
        return $"  {label,-7}{calls,9:0}  {pct,6:0.0}%";
    }
}
