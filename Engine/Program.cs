using ImperiumEngine;
using ImperiumEngine.Assets;
using Raylib_cs;

string scene_path = "";
string game_path = "";
for (int i = 0; i < args.Length; i++)
{
    string a = args[i];
    if (a.StartsWith("--scene=", StringComparison.OrdinalIgnoreCase))
    {
        scene_path = a.Substring("--scene=".Length);
        continue;
    }
    if (a.StartsWith("--game=", StringComparison.OrdinalIgnoreCase))
    {
        game_path = a.Substring("--game=".Length);
        continue;
    }
    if ((a == "--scene" || a == "-scene") && i + 1 < args.Length)
    {
        i++;
        scene_path = args[i];
        continue;
    }
    if ((a == "--game" || a == "-game") && i + 1 < args.Length)
    {
        i++;
        game_path = args[i];
        continue;
    }
}

ImpApp app = new();

app.Run(() =>
{
    if (string.IsNullOrWhiteSpace(game_path))
    {
        return;
    }
    A_Game loaded = ImpAsset.Load<A_Game>(game_path);
    if (loaded != null)
    {
        A_Game.game = loaded;
        return;
    }
    A_Game.game = new A_Game { gamepath = game_path };
}, () =>
{
    string title = "Imperium";
    if (A_Game.game != null)
    {
        string t = A_Game.game.title.ToString();
        if (string.IsNullOrWhiteSpace(t))
        {
            t = Path.GetFileName(A_Game.game.GetRootDir().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        if (!string.IsNullOrWhiteSpace(t))
        {
            title = t;
        }
    }
    Raylib.SetWindowTitle(title);
    ImpScene play = null;
    if (!string.IsNullOrWhiteSpace(scene_path))
    {
        play = ImpAsset.Load<ImpScene>(scene_path);
    }
    if (play == null)
    {
        play = ImpGame.starting_scene.Get();
    }
    if (play == null)
    {
        play = new ImpScene();
    }
    play.is_running = true;
    ImpScene.current = play;
});
