using System.Numerics;
using Engine.Assets;
using Engine.Comps._3D;
using Engine.Globals;
using Engine.Structs;
using JoltPhysicsSharp;

namespace Engine.Core;

[Title("Physics")]
public class ImpPhysics
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public const uint LAYER_STATIC = 0;
    public const uint LAYER_MOVING = 1;
    const uint LAYER_COUNT = 2;
    const uint BP_COUNT = 2;
    const float MIN_EXTENT = 0.05f;

    [ImpVar][Config][Category("World")] public static Vector3 gravity = new(0, -9.81f, 0);
    [ImpVar][Config][Category("World")] public static int max_bodies = 65536;
    [ImpVar][Config][Category("World")] public static int max_body_pairs = 65536;
    [ImpVar][Config][Category("World")] public static int max_contact_constraints = 10240;

    public static bool ready { get; private set; }

    static PhysicsSystem _system;
    static JobSystemThreadPool _jobs;
    static ObjectLayerPairFilterTable _object_filter;
    static BroadPhaseLayerInterfaceTable _bp_layers;
    static ObjectVsBroadPhaseLayerFilterTable _obj_vs_bp;
    static CharacterVsCharacterCollisionSimple _char_vs_char;
    static readonly Dictionary<Imp3D, TActor> _actors = new();
    static readonly Dictionary<uint, TActor> _by_body = new();
    static readonly List<TActor> _movers = new();
    static ulong _user_seq;
    static bool _broadphase_dirty;
    static bool _inited;

    class TActor
    {
        public Imp3D owner;
        public Shape shape;
        public BodyID body = BodyID.Invalid;
        public CharacterVirtual character;
        public ulong user;
        public Vector3 last_pos;
        public Quaternion last_rot;
        public bool mover;
    }

    public static PhysicsSystem System => _system;

    // ============================================================
    // Lifecycle
    // ============================================================
    public static void Init()
    {
        if (_inited) return;
        try
        {
            InitInner();
        }
        catch (Exception e)
        {
            GLog.Error("JoltPhysics failed to initialize: " + e.Message);
            ready = false;
        }
    }

    static void InitInner()
    {
        Foundation.SetTraceHandler(msg => GLog.Warning("[Jolt] " + msg));
#if DEBUG
        Foundation.SetAssertFailureHandler((expr, msg, file, line) =>
        {
            GLog.Error($"[Jolt] assert {file}:{line} {msg ?? expr}");
            return true;
        });
#endif
        if (!Foundation.Init(false))
        {
            GLog.Error("JoltPhysics failed to initialize");
            return;
        }
        _inited = true;

        // These filter objects must stay rooted. JoltPhysicsSharp tracks them with
        // WeakReference; if GC collects them the native PhysicsSystem is left with
        // dangling layer-filter pointers and Play crashes on the first collision query.
        _object_filter = new ObjectLayerPairFilterTable(LAYER_COUNT);
        _object_filter.EnableCollision(LAYER_STATIC, LAYER_MOVING);
        _object_filter.EnableCollision(LAYER_MOVING, LAYER_MOVING);

        _bp_layers = new BroadPhaseLayerInterfaceTable(LAYER_COUNT, BP_COUNT);
        _bp_layers.MapObjectToBroadPhaseLayer(LAYER_STATIC, 0);
        _bp_layers.MapObjectToBroadPhaseLayer(LAYER_MOVING, 1);

        _obj_vs_bp = new ObjectVsBroadPhaseLayerFilterTable(_bp_layers, BP_COUNT, _object_filter, LAYER_COUNT);

        PhysicsSystemSettings settings = new()
        {
            MaxBodies = max_bodies,
            MaxBodyPairs = max_body_pairs,
            MaxContactConstraints = max_contact_constraints,
            ObjectLayerPairFilter = _object_filter,
            BroadPhaseLayerInterface = _bp_layers,
            ObjectVsBroadPhaseLayerFilter = _obj_vs_bp,
        };
        _jobs = new JobSystemThreadPool();
        _system = new PhysicsSystem(settings);
        _system.Gravity = gravity;
        _char_vs_char = new CharacterVsCharacterCollisionSimple();
        ready = true;
    }

    public static void Shutdown()
    {
        Clear();
        ready = false;
        _char_vs_char?.Dispose();
        _char_vs_char = null;
        _system?.Dispose();
        _system = null;
        _jobs?.Dispose();
        _jobs = null;
        _obj_vs_bp?.Dispose();
        _obj_vs_bp = null;
        _bp_layers?.Dispose();
        _bp_layers = null;
        _object_filter?.Dispose();
        _object_filter = null;
        if (_inited)
        {
            Foundation.Shutdown();
            _inited = false;
        }
    }

    public static void Clear()
    {
        if (_actors.Count == 0) return;
        TActor[] all = new TActor[_actors.Count];
        _actors.Values.CopyTo(all, 0);
        for (int i = 0; i < all.Length; i++)
            DestroyActor(all[i]);
        _actors.Clear();
        _by_body.Clear();
        _movers.Clear();
        _broadphase_dirty = true;
    }

    public static void Step(float dt)
    {
        if (!ready || _system == null) return;
        if (dt <= 0f) return;
        if (dt > 0.05f) dt = 0.05f;
        try
        {
            StepInner(dt);
        }
        catch (Exception e)
        {
            GLog.Error("JoltPhysics step failed: " + e.Message);
        }
    }

    static void StepInner(float dt)
    {
        _system.Gravity = gravity;

        if (_broadphase_dirty)
        {
            _system.OptimizeBroadPhase();
            _broadphase_dirty = false;
        }

        // Push authored transform changes into Jolt (teleports / moved statics).
        foreach (KeyValuePair<Imp3D, TActor> kv in _actors)
            PushTransform(kv.Value);

        int collision_steps = 1;
        if (dt > 1f / 60f) collision_steps = Math.Clamp((int)MathF.Round(dt * 60f), 1, 4);
        _system.Update(dt, collision_steps, _jobs);

        for (int i = 0; i < _movers.Count; i++)
            StepMover(_movers[i], dt);
    }

    // ============================================================
    // Register
    // ============================================================
    public static void Ensure(Imp3D o)
    {
        if (o == null || !ready || _system == null) return;
        if (!o.physics_enabled) return;
        if (_actors.ContainsKey(o)) return;
        Register(o);
    }

    public static void Unregister(Imp3D o)
    {
        if (o == null) return;
        if (!_actors.TryGetValue(o, out TActor actor)) return;
        _actors.Remove(o);
        DestroyActor(actor);
    }

    static void Register(Imp3D o)
    {
        try
        {
            RegisterInner(o);
        }
        catch (Exception e)
        {
            GLog.Error("Physics register failed (" + o.name + "): " + e.Message);
        }
    }

    static void RegisterInner(Imp3D o)
    {
        Shape? shape = o.Phys_GetShape();
        if (shape == null) return;

        TActor actor = new()
        {
            owner = o,
            shape = shape,
            user = ++_user_seq,
            mover = o.movement_enabled,
        };

        PoseOf(o, actor.mover, out Vector3 pos, out Quaternion rot);
        if (rot.LengthSquared() < 1e-8f) rot = Quaternion.Identity;
        else rot = Quaternion.Normalize(rot);
        actor.last_pos = pos;
        actor.last_rot = rot;

        if (actor.mover)
        {
            CharacterVirtualSettings cv = new()
            {
                Shape = shape,
                Up = GMath.WORLD_UP,
                EnhancedInternalEdgeRemoval = true,
            };
            if (o is C3_Collider col && col.type == EColliderType.Capsule)
            {
                float radius = CapsuleRadius(col);
                cv.SupportingVolume = new Plane(GMath.WORLD_UP, -radius);
            }
            CharacterVirtual character = new(cv, pos, rot, actor.user, _system);
            character.SetCharacterVsCharacterCollision(_char_vs_char);
            _char_vs_char.Add(character);
            character.LinearVelocity = o.velocity;
            actor.character = character;
            _movers.Add(actor);
        }
        else
        {
            ObjectLayer layer = LAYER_STATIC;
            using BodyCreationSettings create = new(shape, pos, rot, MotionType.Static, layer);
            create.UserData = actor.user;
            create.IsSensor = o.collision_preset == A_CollisionPreset.PRESET_OVERLAP;
            actor.body = _system.BodyInterface.CreateAndAddBody(create, Activation.DontActivate);
            if (actor.body.IsValid) _by_body[actor.body.ID] = actor;
        }

        _actors[o] = actor;
        _broadphase_dirty = true;
    }

    static void DestroyActor(TActor actor)
    {
        if (actor.character != null)
        {
            _char_vs_char?.Remove(actor.character);
            _movers.Remove(actor);
            actor.character.Dispose();
            actor.character = null;
        }
        if (actor.body.IsValid && _system != null)
        {
            _by_body.Remove(actor.body.ID);
            if (_system.BodyInterface.IsAdded(actor.body))
                _system.BodyInterface.RemoveAndDestroyBody(actor.body);
            actor.body = BodyID.Invalid;
        }
        actor.shape?.Dispose();
        actor.shape = null;
        _broadphase_dirty = true;
    }

    // ============================================================
    // Trace
    // ============================================================
    public static TTraceResult3D Trace(Vector3 start, Vector3 end, Func<Imp3D, bool>? filter = null)
    {
        TTraceResult3D result = new()
        {
            start_position = start,
            start_normal = Vector3.Normalize(end - start),
        };
        if (!ready || _system == null) return result;
        Vector3 delta = end - start;
        if (delta.LengthSquared() < 1e-16f) return result;

        Ray ray = new(start, delta);
        if (!_system.NarrowPhaseQuery.CastRay(ray, out RayCastResult hit) || !hit.BodyID.IsValid)
            return result;

        Vector3 hit_pos = start + delta * hit.Fraction;
        Imp3D comp = null;
        if (_by_body.TryGetValue(hit.BodyID.ID, out TActor actor))
            comp = actor.owner;
        if (filter != null && (comp == null || !filter(comp)))
            return result;

        Vector3 normal = -result.start_normal;
        BodyLockInterface locks = _system.BodyLockInterface;
        locks.LockRead(hit.BodyID, out BodyLockRead body_lock);
        try
        {
            if (body_lock.Succeeded && body_lock.Body != null)
                normal = body_lock.Body.GetWorldSpaceSurfaceNormal(hit.subShapeID2, hit_pos);
        }
        finally
        {
            locks.UnlockRead(body_lock);
        }

        result.hit = true;
        result.hit_comp = comp;
        result.hit_position = hit_pos;
        result.hit_normal = normal;
        return result;
    }

    public static Imp3D CompOfBody(BodyID id)
    {
        if (!id.IsValid) return null;
        return _by_body.TryGetValue(id.ID, out TActor a) ? a.owner : null;
    }

    // ============================================================
    // Internals
    // ============================================================
    static void PushTransform(TActor actor)
    {
        Imp3D o = actor.owner;
        PoseOf(o, actor.mover, out Vector3 pos, out Quaternion rot);
        if ((pos - actor.last_pos).LengthSquared() < 1e-10f
            && Quaternion.Dot(rot, actor.last_rot) > 0.99999f)
            return;
        actor.last_pos = pos;
        actor.last_rot = rot;
        if (actor.character != null)
        {
            actor.character.Position = pos;
            actor.character.Rotation = rot;
        }
        else if (actor.body.IsValid)
        {
            _system.BodyInterface.SetPositionAndRotation(actor.body, pos, rot, Activation.DontActivate);
        }
    }

    static void StepMover(TActor actor, float dt)
    {
        Imp3D o = actor.owner;
        CharacterVirtual character = actor.character;
        if (character == null) return;

        A_MoveMode mode = o.move_mode ?? A_MoveMode.DEFAULT;
        bool grounded = character.GroundState == GroundState.OnGround;
        o.is_grounded = grounded;

        Vector3 wish = o._move_wish;
        o._move_wish = Vector3.Zero;
        wish.Y = 0f;
        float wish_mag = wish.Length();
        if (wish_mag > 1f)
        {
            wish /= wish_mag;
            wish_mag = 1f;
        }
        Vector3 wish_vel = wish * (mode.speed * wish_mag);

        Vector3 vel = o.velocity;
        Vector3 vel_h = new(vel.X, 0f, vel.Z);
        bool has_wish = wish_mag > 1e-4f;
        if (has_wish || grounded)
        {
            float rate = has_wish
                ? (grounded ? mode.acceleration : mode.acceleration * mode.air_control)
                : mode.deceleration;
            vel_h = MoveTowards(vel_h, wish_vel, rate * dt);
        }
        else
        {
            float damp = MathF.Pow(MathF.Max(0f, 1f - mode.air_friction), dt * 60f);
            vel_h *= damp;
        }
        vel.X = vel_h.X;
        vel.Z = vel_h.Z;

        if (mode.gravity_enabled && !(grounded && vel.Y <= 0f))
        {
            Vector3 gdir = mode.gravity_dir;
            if (gdir.LengthSquared() < 1e-8f) gdir = gravity;
            if (gdir.LengthSquared() > 1e-8f) gdir = Vector3.Normalize(gdir);
            float g = gravity.Length() * mode.gravity_scale;
            if (mode.gravity_accel_curve != null)
            {
                float cv = mode.gravity_accel_curve.Curve_GetValue(MathF.Abs(vel.Y));
                if (MathF.Abs(cv) > 1e-6f) g = cv * mode.gravity_scale;
            }
            vel += gdir * g * dt;
        }
        else if (grounded && vel.Y < 0f)
        {
            vel.Y = 0f;
        }

        character.LinearVelocity = vel;
        ExtendedUpdateSettings upd = new();
        character.ExtendedUpdate(dt, upd, LAYER_MOVING, _system);

        vel = character.LinearVelocity;
        o.velocity = vel;
        Vector3 new_pos = character.Position;
        actor.last_pos = new_pos;
        actor.last_rot = character.Rotation;
        o.Position_Set(new_pos, true);

        if (mode.rotate_with_movement)
        {
            Vector3 h = new(vel.X, 0f, vel.Z);
            if (h.LengthSquared() > 0.04f)
            {
                Vector3 look = GMath.V3_LookAt(Vector3.Zero, h);
                Vector3 rot = o.global_transform.rotation;
                rot.Z = ApproachAngle(rot.Z, look.Z, mode.velocity_rotation_rate.Y * dt);
                o.Rotation_Set(rot, true);
                actor.last_rot = o.world_rotation;
            }
        }

        o.is_grounded = character.GroundState == GroundState.OnGround;
    }

    static void PoseOf(Imp3D o, bool mover, out Vector3 pos, out Quaternion rot)
    {
        if (mover)
        {
            pos = o.global_transform.position;
            rot = o.world_rotation;
            return;
        }
        TBounds3 b = o.bounds.IsEmpty ? o.Bounds_Cache() : o.bounds;
        if (!b.IsEmpty)
        {
            pos = b.center;
            rot = GMath.Euler_2_Quat(b.rotation);
            return;
        }
        pos = o.global_transform.position;
        rot = o.world_rotation;
    }

    public static BoxShape MakeBox(Vector3 half)
    {
        half = Vector3.Max(half, new Vector3(MIN_EXTENT));
        float cr = MathF.Min(Foundation.DefaultConvexRadius, MathF.Min(half.X, MathF.Min(half.Y, half.Z)) * 0.5f);
        if (cr < 0.001f) cr = 0.001f;
        return new BoxShape(half, cr);
    }

    public static float CapsuleRadius(C3_Collider col)
    {
        Vector3 s = col.global_transform.scale;
        return 0.4f * MathF.Max(MathF.Abs(s.X), MathF.Abs(s.Z));
    }

    public static float CapsuleHeight(C3_Collider col)
    {
        return 1.8f * MathF.Abs(col.global_transform.scale.Y);
    }

    static Vector3 MoveTowards(Vector3 current, Vector3 target, float max_delta)
    {
        Vector3 d = target - current;
        float dist = d.Length();
        if (dist <= max_delta || dist < 1e-8f) return target;
        return current + d * (max_delta / dist);
    }

    static float ApproachAngle(float current, float target, float max_delta)
    {
        float d = target - current;
        while (d > 180f) d -= 360f;
        while (d < -180f) d += 360f;
        if (MathF.Abs(d) <= max_delta) return target;
        return current + MathF.CopySign(max_delta, d);
    }
}
