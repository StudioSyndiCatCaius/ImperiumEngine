using Engine;
using Engine.Assets;
using Engine.Comps._1D;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Sandbox;

App _app=new App();
App.name = "My Game";

TAppHooks hooks=new()
{
    on_post_init = () =>
    {
        ImpSandbox.current = new Sandbox_Vis();
        App.scene_persistent = new A_Scene()
        {
            root = new C1_GlobalScene()
            {
                
            }
        };
    }
};

string force = "";
bool has_game = false;
string[] argv = Environment.GetCommandLineArgs();
for (int i = 1; i < argv.Length; i++)
{
    string a = argv[i].TrimStart('-', '/');
    if (a.StartsWith("game", StringComparison.OrdinalIgnoreCase)) has_game = true;
}
if (!has_game)
{
    string test = Path.Combine(GFile.GetDir_Root(EContentDir.Engine), "Templates", "Test", "Test.ImpGame");
    if (File.Exists(test)) force = test;
}
_app.Run(hooks, true, force);
