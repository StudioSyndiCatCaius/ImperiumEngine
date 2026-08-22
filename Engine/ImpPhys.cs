using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using JoltPhysicsSharp;

namespace ImperiumEngine;

// One Jolt world per ImpGame. Foundation / JobSystem are process-wide.
public class ImpPhys
{
    public const uint LayerStatic = 0;
    public const uint LayerMoving = 1;

    static bool _foundation;
    static JobSystemThreadPool _jobs;

    PhysicsSystem _system;
    // PhysicsSystem_Destroy deletes these native objects. Keep the C# wrappers
    // rooted so the GC cannot Dispose them while the world is live.
    ObjectLayerPairFilterTable _pair_filter;
    BroadPhaseLayerInterfaceTable _bp_interface;
    ObjectVsBroadPhaseLayerFilterTable _bp_filter;

    readonly Dictionary<Imp3D, Entry> _entries = new();
    readonly Dictionary<uint, Imp3D> _body_to_comp = new();

    class Entry
    {
        public Imp3D comp;
        public BodyID body;
        public CharacterVirtual character;
        public Shape shape;
        public Shape shape_inner;
        public Vector3 scale;
        public bool is_character;
    }

    public bool IsCreated
    {
        get { return _system != null; }
    }

    public static void Init()
    {
        if (_foundation)
        {
            return;
        }
        if (!Foundation.Init(false))
        {
            Console.WriteLine("[ImpPhys] Foundation.Init failed");
            return;
        }
        Foundation.SetTraceHandler((message) =>
        {
            Console.WriteLine("[Jolt] " + message);
        });
        Foundation.SetAssertFailureHandler((expression, message, file, line) =>
        {
            Console.WriteLine("[Jolt] assert " + file + ":" + line + " " + expression + " " + message);
            return true;
        });
        _jobs = new JobSystemThreadPool();
        _foundation = true;
    }

    public static void Shutdown()
    {
        if (!_foundation)
        {
            return;
        }
        if (_jobs != null)
        {
            _jobs.Dispose();
            _jobs = null;
        }
        Foundation.Shutdown();
        _foundation = false;
    }

    public void Create()
    {
        if (_system != null)
        {
            return;
        }
        Init();
        if (!_foundation)
        {
            return;
        }

        _pair_filter = new(2);
        _pair_filter.EnableCollision(LayerStatic, LayerMoving);
        _pair_filter.EnableCollision(LayerMoving, LayerMoving);

        _bp_interface = new(2, 2);
        _bp_interface.MapObjectToBroadPhaseLayer(LayerStatic, (BroadPhaseLayer)0);
        _bp_interface.MapObjectToBroadPhaseLayer(LayerMoving, (BroadPhaseLayer)1);

        _bp_filter = new(_bp_interface, 2, _pair_filter, 2);

        PhysicsSystemSettings settings = new()
        {
            MaxBodies = 10240,
            MaxBodyPairs = 65536,
            MaxContactConstraints = 10240,
            ObjectLayerPairFilter = _pair_filter,
            BroadPhaseLayerInterface = _bp_interface,
            ObjectVsBroadPhaseLayerFilter = _bp_filter,
        };
        _system = new PhysicsSystem(settings);
        _system.Gravity = Vector3.Zero;
        _system.OptimizeBroadPhase();
    }

    public void Dispose()
    {
        if (_system == null)
        {
            return;
        }
        List<Imp3D> comps = new(_entries.Keys);
        for (int i = 0; i < comps.Count; i++)
        {
            Unregister(comps[i]);
        }
        _entries.Clear();
        _body_to_comp.Clear();
        _system.Dispose();
        _system = null;
        // Native filters were deleted by PhysicsSystem_Destroy. Do not Dispose the
        // C# wrappers — that would double-free. Just drop them so they are not finalized.
        if (_pair_filter != null)
        {
            GC.SuppressFinalize(_pair_filter);
            _pair_filter = null;
        }
        if (_bp_interface != null)
        {
            GC.SuppressFinalize(_bp_interface);
            _bp_interface = null;
        }
        if (_bp_filter != null)
        {
            GC.SuppressFinalize(_bp_filter);
            _bp_filter = null;
        }
    }

    public void Register(Imp3D comp)
    {
        if (comp == null)
        {
            return;
        }
        if (_entries.ContainsKey(comp))
        {
            Unregister(comp);
        }
        if (_system == null)
        {
            Create();
        }
        if (_system == null)
        {
            return;
        }

        TTransform3 world = comp.Transform_Get(true);
        Shape shape = comp.Phys_MakeShape(world.scale);
        if (shape == null)
        {
            Console.WriteLine("[ImpPhys] no shape for " + comp.name);
            return;
        }

        Entry e = new();
        e.comp = comp;
        e.shape = shape;
        e.scale = world.scale;
        e.is_character = comp.movement_enabled;
        e.body = BodyID.Invalid;

        Quaternion rot = ImpMath.Euler_2_Quat(world.rotation);

        if (e.is_character)
        {
            Vector3 feet = comp.Phys_ShapeOffset(world.scale);
            Shape standing = shape;
            if (feet.LengthSquared() > 1e-8f)
            {
                standing = new RotatedTranslatedShape(feet, Quaternion.Identity, shape);
                e.shape_inner = shape;
                e.shape = standing;
            }
            CharacterVirtualSettings cs = new();
            cs.Shape = standing;
            cs.Up = Vector3.UnitY;
            cs.MaxSlopeAngle = 45f * (MathF.PI / 180f);
            float stand_h = feet.Y;
            if (stand_h < 0.5f)
            {
                stand_h = 0.5f;
            }
            cs.SupportingVolume = new Plane(Vector3.UnitY, -stand_h);

            CharacterVirtual character = new(cs, world.position, rot, 0, _system);
            e.character = character;
        }
        else
        {
            using BodyCreationSettings body_settings = new(
                shape,
                world.position,
                rot,
                MotionType.Static,
                LayerStatic);
            e.body = _system.BodyInterface.CreateAndAddBody(body_settings, Activation.DontActivate);
            if (e.body.IsInvalid)
            {
                Console.WriteLine("[ImpPhys] failed to create body for " + comp.name);
                return;
            }
            _body_to_comp[e.body.ID] = comp;
        }

        _entries[comp] = e;
        comp._phys_dirty = false;
        comp._phys_scale = world.scale;
    }

    public void Unregister(Imp3D comp)
    {
        if (comp == null)
        {
            return;
        }
        if (!_entries.TryGetValue(comp, out Entry e))
        {
            return;
        }
        _entries.Remove(comp);
        if (!e.body.IsInvalid)
        {
            _body_to_comp.Remove(e.body.ID);
        }
        if (e.character != null)
        {
            e.character.Dispose();
            e.character = null;
        }
        else if (_system != null && !e.body.IsInvalid)
        {
            _system.BodyInterface.RemoveAndDestroyBody(e.body);
        }
        e.body = default;
    }

    public bool IsRegistered(Imp3D comp)
    {
        return comp != null && _entries.ContainsKey(comp);
    }

    public void Step(double dt)
    {
        if (_system == null)
        {
            return;
        }
        float step = (float)dt;
        if (step <= 0f)
        {
            return;
        }
        if (step > 1f / 15f)
        {
            step = 1f / 15f;
        }

        BodyInterface bodies = _system.BodyInterface;
        ExtendedUpdateSettings update_settings = new();
        List<Imp3D> comps = new(_entries.Keys);

        for (int i = 0; i < comps.Count; i++)
        {
            Imp3D comp = comps[i];
            if (!_entries.TryGetValue(comp, out Entry e))
            {
                continue;
            }
            TTransform3 world = comp.Transform_Get(true);
            Quaternion rot = ImpMath.Euler_2_Quat(world.rotation);

            Vector3 ds = world.scale - e.scale;
            if (ds.LengthSquared() > 1e-6f)
            {
                Register(comp);
                continue;
            }

            if (e.is_character && e.character != null)
            {
                if (comp._phys_dirty)
                {
                    e.character.Position = world.position;
                    e.character.Rotation = rot;
                    comp._phys_dirty = false;
                }
                e.character.LinearVelocity = comp.velocity;
                e.character.ExtendedUpdate(step, update_settings, LayerMoving, _system);
                comp.velocity = e.character.LinearVelocity;
                GroundState ground = e.character.GroundState;
                comp.is_grounded = ground == GroundState.OnGround;
                TTransform3 next = world;
                next.position = e.character.Position;
                comp.Transform_Set(next, true);
                comp._phys_dirty = false;
            }
            else
            {
                if (comp._phys_dirty)
                {
                    bodies.SetPositionAndRotation(e.body, world.position, rot, Activation.DontActivate);
                    comp._phys_dirty = false;
                }
            }
        }

        if (_jobs != null)
        {
            _system.Update(step, 1, _jobs);
        }
    }

    public TTraceResult3D Trace_Line(Vector3 start, Vector3 end, ECollisionChannel channel, Func<Imp3D, bool> filter)
    {
        TTraceResult3D result = new();
        result.start_position = start;
        if (_system == null)
        {
            return result;
        }
        Vector3 cur = start;
        Vector3 full_end = end;
        for (int step = 0; step < 8; step++)
        {
            Vector3 delta = full_end - cur;
            float len = delta.Length();
            if (len < 1e-6f)
            {
                return result;
            }
            JoltPhysicsSharp.Ray ray = new(cur, delta);
            if (!_system.NarrowPhaseQuery.CastRay(ray, out RayCastResult hit))
            {
                return result;
            }
            Imp3D hit_comp = null;
            if (_body_to_comp.TryGetValue(hit.BodyID.ID, out Imp3D mapped))
            {
                hit_comp = mapped;
            }
            Vector3 hit_pos = cur + delta * hit.Fraction;
            if (filter != null && (hit_comp == null || !filter(hit_comp)))
            {
                Vector3 dir = delta / len;
                Vector3 next = hit_pos + dir * 0.02f;
                if (Vector3.Dot(next - cur, delta) <= 0f)
                {
                    return result;
                }
                cur = next;
                continue;
            }
            result.hit = true;
            result.hit_comp = hit_comp;
            result.hit_position = hit_pos;
            result.hit_normal = Vector3.Zero;
            return result;
        }
        return result;
    }
}
