using System.Diagnostics;
using System.Text;
using Editor.Dialog;
using ImperiumEngine.Classes;

namespace Editor.Build;

// Compiles the game into a runnable build. For now: the Windows "Fast" build — a single-file,
// self-contained .exe published straight into the project folder so it can be clicked and played.
public static class ImpBuilder
{
    // Fast Windows build: publishes the Engine runtime as one self-contained win-x64 .exe (the .NET
    // runtime and all native libraries bundled in), copied into the project folder and named after
    // the game. Optionally drops a "<game>_debug.bat" that launches it with a console window. Runs
    // on a background thread behind a progress dialog. Needs the .NET SDK and an engine source
    // checkout (this is a dev/click-to-play build, not a distributable package).
    public static void FastWindows(bool debugLauncher)
    {
        string projectDir = ImpFile.s_projectDir;
        string engineContent = ImpFile.s_engineContentDir;
        string gameName = GameName(projectDir);

        DLG_Process.Run($"Building {gameName} for Windows…", report =>
        {
            report(0.03f, "Locating engine project…");
            // s_engineContentDir is ".../Engine/Content"; its parent holds Engine.csproj
            string engineDir = Path.GetDirectoryName(engineContent) ?? "";
            string engineProj = Path.Combine(engineDir, "Engine.csproj");
            if (!File.Exists(engineProj))
                throw new Exception($"Engine project not found at {engineProj}.\nA Fast build needs the engine source.");

            string outDir = Path.Combine(Path.GetTempPath(), "ImperiumBuild", gameName + "_win");
            report(0.08f, "Publishing single-file executable (this can take a minute)…");
            RunPublish(engineProj, outDir, report);

            report(0.9f, "Copying executable…");
            string builtExe = Path.Combine(outDir, "Engine.exe");
            if (!File.Exists(builtExe))
                throw new Exception("Publish finished but produced no executable.");

            string destExe = Path.Combine(projectDir, gameName + ".exe");
            File.Copy(builtExe, destExe, overwrite: true);

            if (debugLauncher)
                File.WriteAllText(Path.Combine(projectDir, gameName + "_debug.bat"),
                    "@echo off\r\n" +
                    $"\"%~dp0{gameName}.exe\"\r\n" +
                    "echo.\r\n" +
                    "pause\r\n");

            report(1f, $"Built {gameName}.exe");
            Console.WriteLine($"[Build] Fast Windows build -> {destExe}");
            return true;
        });
    }

    // Invokes `dotnet publish` for a single-file self-contained win-x64 build, streaming its output
    // into the progress report. Native libraries are extracted at runtime, so the result is one .exe.
    static void RunPublish(string engineProj, string outDir, Action<float, string> report)
    {
        var psi = new ProcessStartInfo("dotnet",
            $"publish \"{engineProj}\" -c Release -r win-x64 --self-contained true " +
            "-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true " +
            $"-o \"{outDir}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new Exception("Failed to start 'dotnet'. Is the .NET SDK installed and on PATH?");

        // drain stderr on a side thread so a full pipe buffer can't deadlock the stdout read below
        var err = new StringBuilder();
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
        proc.BeginErrorReadLine();

        // stream stdout as status; publish reports no real %, so nudge the bar along per line
        float p = 0.1f;
        string? line;
        while ((line = proc.StandardOutput.ReadLine()) != null)
        {
            p = Math.Min(0.85f, p + 0.015f);
            report(p, Shorten(line));
        }
        proc.WaitForExit();

        if (proc.ExitCode != 0)
            throw new Exception($"dotnet publish failed (exit {proc.ExitCode}):\n{Tail(err.ToString())}");
    }

    // Game identity is the .ImpGame file's name, falling back to the project folder name.
    static string GameName(string projectDir)
    {
        string? f = Directory.GetFiles(projectDir, "*.ImpGame").FirstOrDefault();
        return f != null ? Path.GetFileNameWithoutExtension(f) : new DirectoryInfo(projectDir).Name;
    }

    static string Shorten(string s) => s.Length <= 90 ? s : s[..90];

    // last few lines of a publish failure — enough to show the real error without a wall of text
    static string Tail(string s)
    {
        var lines = s.Split('\n');
        return lines.Length <= 12 ? s : string.Join('\n', lines[^12..]);
    }
}
