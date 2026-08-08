using System.Numerics;
using Editor.Build;
using ImGuiNET;
using ImperiumEngine.Classes;

namespace Editor.Windows;

/*
 * Window for building the game.
 *  Layout: on the left is a list of all avaialbe platforms to built (from ImpPlatform). On the right is the build config for that selected platform
 *  There are 2 kinds of builds:
 *      - Fast: simply builds and .exe (or whatever simple one-file executable for that platform, if aplicable) to the project folder, allow you to click it and play your game right there (as well as a .BAT or .SH for running in DEBUG mode with the console.)
 *      - Full: Builds out the whole game to a target location using designated config:
 *          - include/exclude asset list
 *          - package or loose-file
 *
 */

public class WND_Build : EditorWindow
{
    public override string Title => "Build";

    enum EBuildMode { Fast, Full }
    enum EPackaging { Packaged, Loose }

    // per-platform build settings. Kept in-memory for now (one entry per platform, lazily created);
    // persisting these to a build config is a follow-up.
    class BuildSettings
    {
        public EBuildMode mode = EBuildMode.Fast;
        public bool debug_launcher = true;                 // Fast: emit a .bat/.sh that runs with a console
        public string output_path = "";                    // Full: target directory
        public EPackaging packaging = EPackaging.Packaged; // Full: .ipk package vs loose files
    }

    // discovered platforms (ImpPlatform subclasses) and their settings
    readonly List<Type> _platforms = new();
    readonly Dictionary<Type, BuildSettings> _settings = new();
    Type? _selected;

    float _list_width = 200f;

    protected override void OnOpen()
    {
        base.OnOpen();
        DiscoverPlatforms();
    }

    // Every concrete ImpPlatform subclass in the engine assembly is a build target.
    void DiscoverPlatforms()
    {
        _platforms.Clear();
        _platforms.AddRange(typeof(ImpPlatform).Assembly.GetTypes()
            .Where(t => typeof(ImpPlatform).IsAssignableFrom(t) && !t.IsAbstract
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name));

        _selected = _platforms.FirstOrDefault();
    }

    // display name for a platform type: the class name minus its "Plat_" prefix
    static string PlatformName(Type t) =>
        t.Name.StartsWith("Plat_") ? t.Name["Plat_".Length..] : t.Name;

    BuildSettings SettingsFor(Type t)
    {
        if (!_settings.TryGetValue(t, out var s)) _settings[t] = s = new BuildSettings();
        return s;
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        // --- platform list (left) ---
        ImGui.BeginChild("platform_list", new Vector2(_list_width, 0), ImGuiChildFlags.Borders);
        ImGui.SeparatorText("Platforms");
        foreach (var t in _platforms)
            if (ImGui.Selectable(PlatformName(t), t == _selected))
                _selected = t;
        ImGui.EndChild();

        ImGui.SameLine();

        // --- build config for the selected platform (right) ---
        ImGui.BeginChild("build_config", new Vector2(0, 0));
        if (_selected == null) ImGui.TextDisabled("No build platforms available");
        else DrawConfig(_selected);
        ImGui.EndChild();
    }

    void DrawConfig(Type platform)
    {
        var s = SettingsFor(platform);
        ImGui.SeparatorText(PlatformName(platform));

        // build mode: Fast vs Full (the ref-int overload writes the picked value back into `mode`)
        int mode = (int)s.mode;
        ImGui.RadioButton("Fast", ref mode, (int)EBuildMode.Fast);
        ImGui.SameLine();
        ImGui.RadioButton("Full", ref mode, (int)EBuildMode.Full);
        s.mode = (EBuildMode)mode;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (s.mode == EBuildMode.Fast) DrawFast(platform, s);
        else                           DrawFull(platform, s);
    }

    void DrawFast(Type platform, BuildSettings s)
    {
        ImGui.TextWrapped("Builds a single-file executable straight into the project folder so you " +
                          "can click it and play immediately.");
        ImGui.Checkbox("Generate debug launcher (.bat / .sh)", ref s.debug_launcher);

        ImGui.Spacing();
        if (ImGui.Button("Build & Run", new Vector2(160, 0)))
            RunFastBuild(platform, s);
    }

    void DrawFull(Type platform, BuildSettings s)
    {
        ImGui.TextWrapped("Builds the whole game to a target location for distribution.");
        ImGui.Spacing();

        ImGui.InputText("Output Location", ref s.output_path, 512);

        ImGui.Spacing();
        ImGui.TextUnformatted("Content:");
        int pack = (int)s.packaging;
        ImGui.RadioButton("Packaged (.ipk)", ref pack, (int)EPackaging.Packaged);
        ImGui.SameLine();
        ImGui.RadioButton("Loose Files", ref pack, (int)EPackaging.Loose);
        s.packaging = (EPackaging)pack;

        // TODO: per-asset include/exclude list — needs a content enumeration model first
        ImGui.Spacing();
        ImGui.TextDisabled("Asset include/exclude: TODO");

        ImGui.Spacing();
        bool can_build = s.output_path.Length > 0;
        if (!can_build) ImGui.BeginDisabled();
        if (ImGui.Button("Build", new Vector2(160, 0)))
            RunFullBuild(platform, s);
        if (!can_build) ImGui.EndDisabled();
    }

    void RunFastBuild(Type platform, BuildSettings s)
    {
        switch (PlatformName(platform))
        {
            case "Windows":
                ImpBuilder.FastWindows(s.debug_launcher);
                break;
            default:
                //TODO: fast builds for Linux / MacOs / Android
                Console.WriteLine($"[Build] Fast build not yet implemented for {PlatformName(platform)}");
                break;
        }
    }

    void RunFullBuild(Type platform, BuildSettings s)
    {
        //TODO: implement the full build (package/loose to output_path, honoring the asset include/exclude list)
        Console.WriteLine($"[Build] Full build for {PlatformName(platform)} -> {s.output_path} ({s.packaging})");
    }
}
