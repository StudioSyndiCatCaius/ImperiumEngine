using System.Numerics;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Interfaces;

public interface I_Input
{
    public virtual void _Input_Notif_Key(ImpPlayer player, EInputKey key, EInputState state, double dt) {}
    public virtual void _Input_Notif_Action(ImpPlayer player, TLabel action, EInputState state, Vector3 axis, double dt) {}
}