using ImperiumEngine.Structs;

namespace ImperiumEngine.Nodes.Quest;

public class Node_Q_SceneTransit : ImpFlowNode
{
    [ImpVar] public TRef<ImpScene> scene;
}