using ImperiumEngine.Assets.Flow;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Nodes.Quest;

public class Node_Q_Dialogue
{
    [ImpVar] public Flow_Dialogue dialogue;
    [ImpVar] public Imp3D spawn_point_after; //reference point to spawn the player after the dialogue. NEEDS ti be TRef, and TRef needs to support ImpComp references from other scenes
}