using Engine;
using Engine.Sandbox;
using Engine.Structs;

namespace Engine.Vis.Nodes;

[Title("Delay")]
[Category("Flow")]
public class VN_Delay : VisNode
{
    public float duration = 1f;

    public VN_Delay()
    {
        inputs = new[]
        {
            new VisPin { name = "exec", type = typeof(VisExec) },
            new VisPin { name = "duration", type = typeof(float) },
        };
        outputs = new[] { new VisPin { name = "then", type = typeof(VisExec) } };
    }

    public override Raylib_cs.Color ED_GetTitleColor() => new(40, 160, 190, 255);
    public override string ED_GetTitle() => "Delay";

    public override void Write(TTable tbl) { tbl.Set("duration", duration); }
    public override void Read(TTable tbl) { duration = tbl.get_Float("duration", 1f); }
}
