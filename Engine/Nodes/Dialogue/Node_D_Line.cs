using ImperiumEngine.Comps._1D;

namespace ImperiumEngine.Nodes.Dialogue;

public class Node_D_Line : C2_FlowNode
{
    [ImpVar] public ImpAsset speaker;
    [ImpVar] public TText text;
}