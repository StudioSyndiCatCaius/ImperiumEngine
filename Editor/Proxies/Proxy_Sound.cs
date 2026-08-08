namespace Editor.Proxies;

// .ogg / .wav source audio. Import actions (create a sound asset) come later; for now these just
// claim their extensions so the files are recognised as raw source rather than opened directly.
public class Proxy_OGG : EditorFileProxy
{
    public override string[] Extensions => [".ogg"];
}

public class Proxy_WAV : EditorFileProxy
{
    public override string[] Extensions => [".wav"];
}
