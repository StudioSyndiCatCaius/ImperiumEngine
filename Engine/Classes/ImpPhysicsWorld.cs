using JoltPhysicsSharp;

namespace ImperiumEngine.Classes;

// Global Jolt physics world. Its lifetime is scoped to an actual play session: the packaged Engine
// and Play-In-Editor call Init() before a level begins and Shutdown() when it ends. The editor
// preview (editing, not playing) never calls Init(), so ImpPhysic3D components skip body creation and
// stay exactly where placed. Only one level simulates at a time, so a single static world suffices.
public static class ImpPhysicsWorld
{
    // Two object layers mapped 1:1 onto two broadphase layers — the standard Jolt static/dynamic split.
    public const uint LAYER_NON_MOVING = 0; // static geometry
    public const uint LAYER_MOVING     = 1; // dynamic / kinematic bodies
    const uint NUM_OBJECT_LAYERS     = 2;
    const uint NUM_BROADPHASE_LAYERS = 2;

    // Jolt is tuned for a fixed timestep; leftover frame time carries into the next Step.
    const float FIXED_STEP = 1f / 60f;
    const int   MAX_STEPS  = 8; // cap catch-up steps so a hitch can't spiral into a freeze

    public static bool IsActive { get; private set; }

    static PhysicsSystem? _system;
    static JobSystem?     _jobSystem;
    static float          _accumulator;

    // The layer filter tables are referenced by the native PhysicsSystem, so they must outlive it —
    // kept alive here and disposed only after the system in Shutdown.
    static BroadPhaseLayerInterfaceTable?      _bpLayers;
    static ObjectLayerPairFilterTable?         _objectFilter;
    static ObjectVsBroadPhaseLayerFilterTable? _objVsBpFilter;

    public static BodyInterface Bodies => _system!.BodyInterface;

    public static void Init()
    {
        if (IsActive) return;

        Foundation.Init(false);

        _bpLayers = new BroadPhaseLayerInterfaceTable(NUM_OBJECT_LAYERS, NUM_BROADPHASE_LAYERS);
        _bpLayers.MapObjectToBroadPhaseLayer(new ObjectLayer(LAYER_NON_MOVING), new BroadPhaseLayer(0));
        _bpLayers.MapObjectToBroadPhaseLayer(new ObjectLayer(LAYER_MOVING),     new BroadPhaseLayer(1));

        _objectFilter = new ObjectLayerPairFilterTable(NUM_OBJECT_LAYERS);
        // static-vs-static stays disabled (default); dynamic collides with everything
        _objectFilter.EnableCollision(new ObjectLayer(LAYER_NON_MOVING), new ObjectLayer(LAYER_MOVING));
        _objectFilter.EnableCollision(new ObjectLayer(LAYER_MOVING),     new ObjectLayer(LAYER_MOVING));

        _objVsBpFilter = new ObjectVsBroadPhaseLayerFilterTable(
            _bpLayers, NUM_BROADPHASE_LAYERS, _objectFilter, NUM_OBJECT_LAYERS);

        var settings = new PhysicsSystemSettings
        {
            MaxBodies             = 65536,
            NumBodyMutexes        = 0,
            MaxBodyPairs          = 65536,
            MaxContactConstraints = 10240,
            BroadPhaseLayerInterface      = _bpLayers,
            ObjectLayerPairFilter         = _objectFilter,
            ObjectVsBroadPhaseLayerFilter = _objVsBpFilter,
        };

        _system      = new PhysicsSystem(settings); // gravity defaults to (0, -9.81, 0)
        _jobSystem   = new JobSystemThreadPool();
        _accumulator = 0;
        IsActive     = true;
    }

    // Advances the simulation in fixed 1/60s steps. Bodies read their results back in
    // ImpPhysic3D.OnUpdate, which runs right after this each frame.
    public static void Step(double delta)
    {
        if (!IsActive) return;

        _accumulator += (float)delta;
        int steps = 0;
        while (_accumulator >= FIXED_STEP && steps < MAX_STEPS)
        {
            _system!.Update(FIXED_STEP, 1, _jobSystem!);
            _accumulator -= FIXED_STEP;
            steps++;
        }
        if (steps == MAX_STEPS) _accumulator = 0; // drop the backlog we couldn't service
    }

    public static void Shutdown()
    {
        if (!IsActive) return;
        IsActive = false;

        _system?.Dispose();        _system        = null;
        _jobSystem?.Dispose();     _jobSystem     = null;
        _objVsBpFilter?.Dispose(); _objVsBpFilter = null;
        _objectFilter?.Dispose();  _objectFilter  = null;
        _bpLayers?.Dispose();      _bpLayers      = null;

        Foundation.Shutdown();
    }
}
