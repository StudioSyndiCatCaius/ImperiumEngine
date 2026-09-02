using Engine.Core;

namespace Engine.Sandbox;

public class Sandbox_Vis : ImpSandbox
{
    public override void Init() { }
    public override void Shutdown() { }
    public override void RunGlobal(string path) { }
    public override TScriptValue? RunInstance(string path, ImpComp owner) { return null; }
    public override bool Has(TScriptValue inst, string name) { return false; }
    public override void Call(TScriptValue inst, string name, params object[] args) { }
}
