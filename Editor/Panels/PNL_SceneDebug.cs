using System.Diagnostics;
using System.Numerics;
using Editor.Scenes;
using Editor.UI;
using Editor.Windows;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using ImGuiNET;
using R3D_cs;
using Raylib_cs;

namespace Editor.Panels;

public enum EEditorSceneDebug_StatsDisplay
{
    [Title("By Class")] ByClass, //shows the frame cost compiled by class (i.e. total time for C3_Mesh would all be next to `C3_Mesh`)
    [Title("Individual")] Individual, //shows top 15 most expensive comps
}

public class PNL_SceneDebug : EdPanel
{
    const int MAX_COMP_STATS = 15;
    const double Smooth = 0.2;

    [EdConfig] public bool show_fps_stats = false; //calculates and draws performance stats (for fps)
    [EdConfig] public bool show_object_count = false; // process-wide ImpAsset / ImpComp counts, with editor vs game split
    [EdConfig] public EEditorSceneDebug_StatsDisplay comp_stats_display = EEditorSceneDebug_StatsDisplay.ByClass;
    [EdConfig] public bool show_environment_stats = false; //calculates and draws performance stats cost by the A_environment/r3d environment effects
    [EdConfig] public bool show_all_bounds = false; //draws all imp3d bound boxes on debug draw pass

    public static PNL_SceneDebug? active;

    readonly EUI_EnumToggle _comp_stats_toggle = new();
    readonly Dictionary<ImpComp, double> _comp_ms = new();
    readonly Dictionary<string, double> _row_ema = new();
    readonly List<OverlayLine> _lines = new();

    double _update_ms;
    double _draw_ms;
    double _env_ms;
    double _update_ema;
    double _draw_ema;
    double _env_ema;
    double _fps_ema;

    struct OverlayLine
    {
        public string text;
        public uint color;
    }

    public PNL_SceneDebug()
    {
        title = "Debug";
        active = this;
        _comp_stats_toggle.on_changed = e => comp_stats_display = (EEditorSceneDebug_StatsDisplay)e;
    }

    public override void OnDrawPanel()
    {
        ImGui.SeparatorText("Performance");
        ImGui.Checkbox("FPS stats", ref show_fps_stats);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Draw FPS, frame time, and per-comp cost in the scene viewport");

        ImGui.Checkbox("Object count", ref show_object_count);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Draw process-wide ImpAsset and ImpComp counts (editor vs game) in the scene viewport");

        ImGui.TextUnformatted("Comp stats");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("By class totals each type. Individual lists the 15 most expensive comps.");
        _comp_stats_toggle.value = comp_stats_display;
        _comp_stats_toggle.OnDraw();

        ImGui.Checkbox("Environment stats", ref show_environment_stats);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Draw A_Environment / R3D effect cost in the 3D viewport");

        ImGui.SeparatorText("Viewport");
        ImGui.Checkbox("All bounds", ref show_all_bounds);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Draw every Imp3D bounds box during the editor debug pass");
    }

    public void BeginFrame()
    {
        active = this;
        _comp_ms.Clear();
        _update_ms = 0;
        _draw_ms = 0;
        _env_ms = 0;
        Imp3D.debug_draw_bounds = show_all_bounds;
        ImpComp.profile_update = null;
        ImpComp.profile_draw = null;
    }

    public void ProfileUpdate(A_Scene? scene, double dt)
    {
        if (scene == null) return;
        if (!show_fps_stats)
        {
            scene.ProcessNotify(ENotifyProcess.Update, dt);
            return;
        }
        ImpComp.profile_update = Accumulate;
        long t0 = Stopwatch.GetTimestamp();
        scene.ProcessNotify(ENotifyProcess.Update, dt);
        _update_ms = ToMs(Stopwatch.GetTimestamp() - t0);
        ImpComp.profile_update = null;
    }

    public void ProfileDrawTree(Action draw)
    {
        if (draw == null) return;
        if (!show_fps_stats)
        {
            draw();
            return;
        }
        ImpComp.profile_draw = Accumulate;
        long t0 = Stopwatch.GetTimestamp();
        draw();
        _draw_ms = ToMs(Stopwatch.GetTimestamp() - t0);
        ImpComp.profile_draw = null;
    }

    public void ProfileEnv(Action submit)
    {
        if (submit == null) return;
        if (!show_environment_stats)
        {
            submit();
            return;
        }
        long t0 = Stopwatch.GetTimestamp();
        submit();
        _env_ms = ToMs(Stopwatch.GetTimestamp() - t0);
    }

    void Accumulate(ImpComp c, double ms)
    {
        if (c == null) return;
        _comp_ms.TryGetValue(c, out double prev);
        _comp_ms[c] = prev + ms;
    }

    public void DrawOverlay(Vector2 vp_min, Vector2 vp_size, A_Scene? scene, bool include_env)
    {
        if (!show_fps_stats && !show_object_count && !(include_env && show_environment_stats)) return;
        if (vp_size.X < 8f || vp_size.Y < 8f) return;

        BuildLines(scene, include_env);
        if (_lines.Count == 0) return;

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(vp_min, vp_min + vp_size, true);

        float pad = 8f;
        float line_h = ImGui.GetTextLineHeightWithSpacing();
        float max_w = 0f;
        for (int i = 0; i < _lines.Count; i++)
        {
            Vector2 sz = ImGui.CalcTextSize(_lines[i].text);
            if (sz.X > max_w) max_w = sz.X;
        }

        Vector2 p = vp_min + new Vector2(pad, pad);
        Vector2 rmin = p - new Vector2(6f, 4f);
        Vector2 rmax = p + new Vector2(max_w + 6f, line_h * _lines.Count + 4f);
        dl.AddRectFilled(rmin, rmax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.04f, 0.05f, 0.07f, 0.72f)), 4f);

        for (int i = 0; i < _lines.Count; i++)
        {
            dl.AddText(p + new Vector2(0f, line_h * i), _lines[i].color, _lines[i].text);
        }

        dl.PopClipRect();
    }

    void BuildLines(A_Scene? scene, bool include_env)
    {
        _lines.Clear();
        uint white = Col(0.92f, 0.94f, 0.96f);
        uint dim = Col(0.62f, 0.66f, 0.72f);
        uint head = Col(0.78f, 0.86f, 1f);

        if (show_fps_stats)
        {
            float fps = Raylib.GetFPS();
            float frame_ms = Raylib.GetFrameTime() * 1000f;
            _fps_ema = Ema(_fps_ema, fps);
            _update_ema = Ema(_update_ema, _update_ms);
            _draw_ema = Ema(_draw_ema, _draw_ms);

            AddLine($"{_fps_ema:0} FPS   {frame_ms:0.0} ms", FpsColor(_fps_ema));
            AddLine($"Update  {Fmt(_update_ema)}", Heat(_update_ema));
            AddLine($"Draw    {Fmt(_draw_ema)}", Heat(_draw_ema));

            if (_comp_ms.Count > 0)
            {
                AddLine("", white);
                if (comp_stats_display == EEditorSceneDebug_StatsDisplay.ByClass)
                {
                    AddLine("By class", head);
                    AppendClassRows(dim);
                }
                else
                {
                    AddLine("Top comps", head);
                    AppendIndividualRows(dim);
                }
            }
        }

        if (show_object_count)
        {
            if (_lines.Count > 0) AddLine("", white);
            AddLine("Objects", head);
            AppendObjectRows(dim, white);
        }

        if (include_env && show_environment_stats)
        {
            if (_lines.Count > 0) AddLine("", white);
            _env_ema = Ema(_env_ema, _env_ms);
            AddLine($"R3D     {Fmt(_env_ema)}", Heat(_env_ema));
            AppendEnvRows(scene?.environment, dim, white);
        }
    }

    void AppendClassRows(uint dim)
    {
        Dictionary<Type, (double ms, int n)> by = new();
        foreach (KeyValuePair<ImpComp, double> kv in _comp_ms)
        {
            Type t = kv.Key.GetType();
            by.TryGetValue(t, out (double ms, int n) cur);
            by[t] = (cur.ms + kv.Value, cur.n + 1);
        }

        List<(string key, string label, double ms)> rows = new(by.Count);
        foreach (KeyValuePair<Type, (double ms, int n)> kv in by)
        {
            string key = "c:" + kv.Key.Name;
            double ema = EmaRow(key, kv.Value.ms);
            rows.Add((key, $"{kv.Key.Name}  x{kv.Value.n}", ema));
        }
        rows.Sort((a, b) => b.ms.CompareTo(a.ms));
        int n = Math.Min(MAX_COMP_STATS, rows.Count);
        for (int i = 0; i < n; i++)
            AddLine($" {Pad(rows[i].label)}  {Fmt(rows[i].ms)}", Mix(Heat(rows[i].ms), dim));
    }

    void AppendIndividualRows(uint dim)
    {
        List<(string key, string label, double ms)> rows = new(_comp_ms.Count);
        foreach (KeyValuePair<ImpComp, double> kv in _comp_ms)
        {
            ImpComp c = kv.Key;
            string type = c.GetType().Name;
            string name = string.IsNullOrEmpty(c.name) ? type : c.name;
            string label = name == type ? type : $"{name}  ({type})";
            string key = "i:" + c.GetHashCode();
            rows.Add((key, label, EmaRow(key, kv.Value)));
        }
        rows.Sort((a, b) => b.ms.CompareTo(a.ms));
        int n = Math.Min(MAX_COMP_STATS, rows.Count);
        for (int i = 0; i < n; i++)
            AddLine($" {Pad(rows[i].label)}  {Fmt(rows[i].ms)}", Mix(Heat(rows[i].ms), dim));
    }

    void AppendObjectRows(uint dim, uint white)
    {
        int assets = 0, assets_ed = 0, assets_gm = 0;
        HashSet<ImpAsset> asset_seen = new();
        foreach (ImpAsset a in App.assets.Values)
        {
            if (a == null || !asset_seen.Add(a)) continue;
            assets++;
            if (IsEditorAsset(a)) assets_ed++;
            else assets_gm++;
        }

        int comps = 0, comps_ed = 0, comps_gm = 0;
        HashSet<ImpComp> comp_seen = new();
        HashSet<A_Scene> scenes = new();
        void AddScene(A_Scene? s)
        {
            if (s != null) scenes.Add(s);
        }
        AddScene(App.scene_current);
        if (App.scenes_global != null)
        {
            for (int i = 0; i < App.scenes_global.Count; i++)
                AddScene(App.scenes_global[i]);
        }
        WND_Scene? wnd = WND_Scene.active;
        if (wnd != null)
        {
            for (int i = 0; i < wnd.scene_tabs.Count; i++)
                AddScene(wnd.scene_tabs[i].scene);
        }

        void Walk(ImpComp? c, bool editor)
        {
            if (c == null || !comp_seen.Add(c)) return;
            comps++;
            if (editor) comps_ed++;
            else comps_gm++;
            for (int i = 0; i < c.children.Count; i++)
                Walk(c.children[i], editor);
        }

        foreach (A_Scene s in scenes)
            Walk(s.root, s is SCN_Editor);
        Walk(App.dialog_current, App.dialog_current?.scene is SCN_Editor);

        int total = assets + comps;
        int ed = assets_ed + comps_ed;
        int gm = assets_gm + comps_gm;

        AddLine($" Total    {total}", white);
        AddLine($"  Editor  {ed}", dim);
        AddLine($"  Game    {gm}", dim);
        AddLine($" Assets   {assets}", white);
        AddLine($"  Editor  {assets_ed}", dim);
        AddLine($"  Game    {assets_gm}", dim);
        AddLine($" Comps    {comps}", white);
        AddLine($"  Editor  {comps_ed}", dim);
        AddLine($"  Game    {comps_gm}", dim);
    }

    static bool IsEditorAsset(ImpAsset a)
    {
        if (GAsset.Builtin_Is(a)) return true;
        string p = a.filepath ?? "";
        if (p.StartsWith("{engine}", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.StartsWith("{builtin}", StringComparison.OrdinalIgnoreCase)) return true;
        string eng = GFile.GetDir_Content(EContentDir.Engine);
        return !string.IsNullOrEmpty(eng)
            && p.IndexOf(eng, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void AppendEnvRows(A_Environment? env, uint dim, uint white)
    {
        if (env == null)
        {
            AddLine(" no environment", dim);
            return;
        }
        AddLine(" " + Flag("SSAO", env.ssao_enabled, env.ssao_sample_count + " samples"), env.ssao_enabled ? white : dim);
        AddLine(" " + Flag("SSIL", env.ssil_enabled), env.ssil_enabled ? white : dim);
        AddLine(" " + Flag("SSGI", env.ssgi_enabled), env.ssgi_enabled ? white : dim);
        AddLine(" " + Flag("SSR", env.ssr_enabled), env.ssr_enabled ? white : dim);
        AddLine(" " + Flag("Fog", env.fog_mode != Fog.Disabled, env.fog_mode.ToString()), env.fog_mode != Fog.Disabled ? white : dim);
        AddLine(" " + Flag("VFog", env.volumetric_fog_enabled), env.volumetric_fog_enabled ? white : dim);
        AddLine(" " + Flag("DoF", env.dof_mode != DoF.Disabled, env.dof_mode.ToString()), env.dof_mode != DoF.Disabled ? white : dim);
        AddLine(" " + Flag("Bloom", env.bloom_mode != Bloom.Disabled, env.bloom_mode.ToString()), env.bloom_mode != Bloom.Disabled ? white : dim);
    }

    static string Flag(string name, bool on, string extra = "")
    {
        string state = on ? "on " : "off";
        if (on && !string.IsNullOrEmpty(extra)) return $"{name,-5} {state}  {extra}";
        return $"{name,-5} {state}";
    }

    void AddLine(string text, uint color) => _lines.Add(new OverlayLine { text = text, color = color });

    double EmaRow(string key, double ms)
    {
        _row_ema.TryGetValue(key, out double prev);
        double next = Ema(prev, ms);
        _row_ema[key] = next;
        return next;
    }

    static double Ema(double prev, double next)
    {
        if (prev <= 0) return next;
        return prev + (next - prev) * Smooth;
    }

    static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    static string Fmt(double ms)
    {
        if (ms < 0.01) return $"{ms * 1000:0.0} us";
        if (ms < 10) return $"{ms:0.00} ms";
        return $"{ms:0.0} ms";
    }

    static string Pad(string s)
    {
        if (s.Length <= 28) return s.PadRight(28);
        return s[..26] + "..";
    }

    static uint Col(float r, float g, float b, float a = 1f) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

    static uint Heat(double ms)
    {
        if (ms >= 2.0) return Col(1f, 0.38f, 0.32f);
        if (ms >= 0.5) return Col(1f, 0.82f, 0.32f);
        return Col(0.55f, 0.86f, 0.52f);
    }

    static uint FpsColor(double fps)
    {
        if (fps >= 50) return Col(0.55f, 0.86f, 0.52f);
        if (fps >= 30) return Col(1f, 0.82f, 0.32f);
        return Col(1f, 0.38f, 0.32f);
    }

    static uint Mix(uint a, uint b)
    {
        Vector4 ca = ImGui.ColorConvertU32ToFloat4(a);
        Vector4 cb = ImGui.ColorConvertU32ToFloat4(b);
        return ImGui.ColorConvertFloat4ToU32((ca + cb) * 0.5f);
    }
}
