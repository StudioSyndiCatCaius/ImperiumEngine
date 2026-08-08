using System.Numerics;
using JoltPhysicsSharp;

namespace ImperiumEngine.Classes;

// ============================================================================================================
// 3D Physics object
// ============================================================================================================

// A 3D component that can take part in the physics simulation. Collision and dynamics only run during
// an actual play session (see ImpPhysicsWorld); in the editor preview the component stays where placed.
// Subclasses supply their collision shape via BuildCollisionShape — the base returns none (no body).
public class ImpPhysic3D : ImpComponent3D
{
    [ImpVar] public bool collion_enabled  = true;
    [ImpVar] public bool simulate_physics = false;
    public Vector3 velocity;

    BodyID  _bodyId;
    bool    _hasBody;
    // shape-centre offset from the component origin, scaled but pre-rotation. Applied when placing the
    // body and subtracted on read-back so a feet-origin mesh lines up with its centred collider.
    Vector3 _centerScaled;

    // The collision shape (world scale already baked in). Return null for no collision → no body.
    protected virtual Shape? BuildCollisionShape(Vector3 worldScale) => null;

    // Offset of the shape centre from the component origin, in local space (pre-scale, pre-rotation).
    protected virtual Vector3 ColliderCenterLocal() => Vector3.Zero;

    // When true the body can't rotate (translation-only), so it never tips over — used by characters,
    // which stay upright like an Unreal capsule.
    protected virtual bool LockUpright => false;

    public override void OnBegin()
    {
        if (!ImpPhysicsWorld.IsActive || !collion_enabled) return;

        GetWorldTRS(out var pos, out var rot, out var scale);

        var shape = BuildCollisionShape(scale);
        if (shape == null) return;

        _centerScaled = ColliderCenterLocal() * scale;
        var bodyPos   = pos + Vector3.Transform(_centerScaled, rot);

        var  motion = simulate_physics ? MotionType.Dynamic : MotionType.Static;
        uint layer  = simulate_physics ? ImpPhysicsWorld.LAYER_MOVING : ImpPhysicsWorld.LAYER_NON_MOVING;

        using var settings = new BodyCreationSettings(shape, bodyPos, rot, motion, new ObjectLayer(layer));
        if (simulate_physics)
        {
            settings.LinearVelocity = velocity;
            if (LockUpright)
                settings.AllowedDOFs = AllowedDOFs.TranslationX | AllowedDOFs.TranslationY | AllowedDOFs.TranslationZ;
        }

        _bodyId = ImpPhysicsWorld.Bodies.CreateAndAddBody(
            settings, simulate_physics ? Activation.Activate : Activation.DontActivate);
        _hasBody = true;
    }

    public override void OnUpdate(double delta)
    {
        // static bodies never move; only dynamic ones write their simulated transform back
        if (!_hasBody || !simulate_physics) return;

        var bodyPos = ImpPhysicsWorld.Bodies.GetPosition(_bodyId);
        var bodyRot = ImpPhysicsWorld.Bodies.GetRotation(_bodyId);
        velocity    = ImpPhysicsWorld.Bodies.GetLinearVelocity(_bodyId);

        GetWorldTRS(out _, out _, out var scale);
        var worldPos = bodyPos - Vector3.Transform(_centerScaled, bodyRot);
        SetWorldTRS(worldPos, bodyRot, scale);
    }

    public override void OnEnd()
    {
        if (_hasBody && ImpPhysicsWorld.IsActive)
            ImpPhysicsWorld.Bodies.RemoveAndDestroyBody(_bodyId);
        _hasBody = false;
    }
}
