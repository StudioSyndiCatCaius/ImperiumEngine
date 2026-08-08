using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace Editor.Proxies;

// .png source images: create A_Texture2D assets that reference the image via file_source, so one
// image can back several textures with different hue/tint/nine-slice settings.
public class Proxy_PNG : EditorFileProxy
{
    public override string[] Extensions => [".png"];

    public override void OnContextMenu(FileProxyContext ctx)
    {
        if (ImGui.MenuItem("Create Texture2D"))
            CreateSourceAsset(ctx, new A_Texture2D());
    }

    // Writes a new .impasset beside the source file, wrapping it via file_source. The name defaults
    // to the source's, with a numeric suffix when that .impasset already exists (so multiple wraps
    // of one image don't collide).
    static void CreateSourceAsset(FileProxyContext ctx, ImpAsset asset)
    {
        asset.file_source = ctx.FilePath;

        string dir = ctx.Directory;
        string stem = Path.GetFileNameWithoutExtension(ctx.FilePath);
        string ext = asset.GetExtension();

        string full = Path.Combine(dir, stem + ext);
        for (int n = 1; File.Exists(full); n++)
            full = Path.Combine(dir, $"{stem}_{n}{ext}");

        if (asset.File_Save(full)) ctx.OnFileCreated?.Invoke(full);
        else Console.WriteLine($"[Proxy_PNG] Failed to create asset for {ctx.FilePath}");
    }
}
