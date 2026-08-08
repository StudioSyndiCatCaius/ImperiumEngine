namespace Editor;

// Everything a file proxy's callbacks need: the file being acted on, plus editor hooks.
public class FileProxyContext
{
    public required string FilePath;                 // absolute path of the source file
    public string Directory => Path.GetDirectoryName(FilePath) ?? "";

    // called when a proxy creates a new file (e.g. an .impasset), so the content browser can
    // refresh its listing and select the result
    public Action<string>? OnFileCreated;
}

// Per-file-type editor behaviour for raw, non-asset source files (.png, .glb, .ogg...). Source
// files are shown in the content browser but do nothing on their own; a proxy adds the right-click
// actions (e.g. "Create Texture2D" from a .png) and can define what double-clicking does.
//
// Proxies never modify the source file. Instead they spawn .impasset files that *reference* it via
// ImpAsset.file_source, so several assets can wrap one source with different import settings.
//
// To handle a new file type, subclass this in Editor/Proxies and list its extensions — the registry
// discovers it by reflection.
public abstract class EditorFileProxy
{
    // file extensions this proxy handles, lower-case and dotted (e.g. ".png")
    public abstract string[] Extensions { get; }

    // add ImGui.MenuItem(...) entries to this file's right-click context menu
    public virtual void OnContextMenu(FileProxyContext ctx) { }

    // double-click behaviour; raw source files do nothing by default
    public virtual void OnOpen(FileProxyContext ctx) { }

    // --- registry: extension -> proxy, built once by reflection over the editor assembly ---

    static Dictionary<string, EditorFileProxy>? s_by_ext;

    // The proxy registered for a file's extension, or null if the type has no custom behaviour.
    public static EditorFileProxy? For(string path)
    {
        s_by_ext ??= Build();
        return s_by_ext.GetValueOrDefault(Path.GetExtension(path));
    }

    static Dictionary<string, EditorFileProxy> Build()
    {
        var map = new Dictionary<string, EditorFileProxy>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in typeof(EditorFileProxy).Assembly.GetTypes())
        {
            if (t.IsAbstract || !typeof(EditorFileProxy).IsAssignableFrom(t)) continue;
            if (t.GetConstructor(Type.EmptyTypes) == null) continue;

            var proxy = (EditorFileProxy)Activator.CreateInstance(t)!;
            foreach (var ext in proxy.Extensions)
                map[ext] = proxy;
        }
        return map;
    }
}
