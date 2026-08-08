namespace Editor.Proxies;

// .glb source meshes. Import actions (create a mesh/skeleton/animation asset) come later; for now
// this just claims the extension so .glb files are recognised as raw source, not opened directly.
public class Proxy_GLB : EditorFileProxy
{
    public override string[] Extensions => [".glb"];
}
