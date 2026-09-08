using System.Runtime.CompilerServices;
using System.Text;

namespace Engine;

// Installed from a module initializer so TypeLoadException during Main JIT
// (before App's static ctor) still writes Cache/Crashes/{exe}_{date}_{time}.log
static class CrashLog
{
    static readonly StringBuilder console = new();
    static int installed;
    static int written;

    [ModuleInitializer]
    internal static void Init() => Install();

    internal static void Install()
    {
        if (Interlocked.Exchange(ref installed, 1) != 0) return;
        try
        {
            HookConsole();
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Exception ex = e.ExceptionObject as Exception
                    ?? new Exception(Convert.ToString(e.ExceptionObject) ?? "Unknown crash");
                Write(ex);
            };
        }
        catch { }
    }

    internal static void HookConsole()
    {
        try
        {
            // Never wrap Console.Error. The runtime writes fatal CLR errors there;
            // intercepting it can recurse into "Fatal error while logging another fatal error."
            if (Console.Out is not Tee)
                Console.SetOut(new Tee(Console.Out));
        }
        catch { }
    }

    internal static void Write(Exception ex)
    {
        if (Interlocked.Exchange(ref written, 1) != 0) return;
        try
        {
            string console_copy;
            lock (console) console_copy = console.ToString();

            DateTime n = DateTime.Now;
            string exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "app") ?? "app";
            exe = exe.ToLowerInvariant();
            foreach (char c in Path.GetInvalidFileNameChars())
                exe = exe.Replace(c, '_');
            if (string.IsNullOrEmpty(exe)) exe = "app";

            string stamp = $"{exe}_{n.Year}-{n.Month}-{n.Day}_{n.Hour:00}-{n.Minute:00}-{n.Second:00}";
            string dir = CrashDir();
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, stamp + ".log");

            string version = "";
            string game = "";
            try { version = App.version; } catch { }
            try { game = App.game_file ?? ""; } catch { }

            StringBuilder sb = new();
            sb.AppendLine("IMPERIUM CRASH");
            sb.AppendLine("time: " + n.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("exe: " + (Environment.ProcessPath ?? ""));
            if (version.Length > 0) sb.AppendLine("version: " + version);
            sb.AppendLine("os: " + Environment.OSVersion);
            sb.AppendLine("cwd: " + Environment.CurrentDirectory);
            sb.AppendLine("args: " + Environment.CommandLine);
            if (game.Length > 0) sb.AppendLine("game: " + game);
            sb.AppendLine();
            sb.AppendLine("----- exception -----");
            sb.AppendLine(ex?.ToString() ?? "(null)");
            sb.AppendLine();
            sb.AppendLine("----- console -----");
            sb.Append(console_copy);
            if (console_copy.Length > 0 && !console_copy.EndsWith('\n'))
                sb.AppendLine();

            File.WriteAllText(path, sb.ToString());
        }
        catch { }
    }

    static string CrashDir()
    {
        try
        {
            string game = App.game_file;
            if (!string.IsNullOrEmpty(game))
            {
                string? g = Path.GetDirectoryName(game);
                if (!string.IsNullOrEmpty(g))
                    return Path.Combine(g, "Cache", "Crashes");
            }
        }
        catch { }

        string start = AppContext.BaseDirectory;
        DirectoryInfo? current = new DirectoryInfo(start);
        while (current != null)
        {
            bool content = Directory.Exists(Path.Combine(current.FullName, "Content"));
            bool repo = File.Exists(Path.Combine(current.FullName, "ImperiumEngine.sln"))
                        || Directory.Exists(Path.Combine(current.FullName, "Engine"));
            if (content && repo)
                return Path.Combine(current.FullName, "Cache", "Crashes");
            current = current.Parent;
        }
        return Path.Combine(start, "Cache", "Crashes");
    }

    sealed class Tee : TextWriter
    {
        readonly TextWriter inner;
        public Tee(TextWriter inner) { this.inner = inner; }
        public override Encoding Encoding => inner?.Encoding ?? Encoding.UTF8;
        public override void Write(char value)
        {
            try { inner.Write(value); } catch { }
            Capture(value.ToString());
        }
        public override void Write(string? value)
        {
            if (value == null) return;
            try { inner.Write(value); } catch { }
            Capture(value);
        }
        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null || count <= 0) return;
            try { inner.Write(buffer, index, count); } catch { }
            Capture(new string(buffer, index, count));
        }
        public override void Flush() { try { inner.Flush(); } catch { } }
        static void Capture(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            lock (console)
            {
                console.Append(s);
                if (console.Length > 524288)
                    console.Remove(0, console.Length - 262144);
            }
        }
    }
}
