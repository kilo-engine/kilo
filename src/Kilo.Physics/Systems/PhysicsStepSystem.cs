using Kilo.ECS;

namespace Kilo.Physics;

/// <summary>
/// Steps the physics simulation each frame.
/// Runs in the Update stage.
/// </summary>
public sealed class PhysicsStepSystem
{
    private readonly PhysicsSettings _settings;

    public PhysicsStepSystem(PhysicsSettings settings)
    {
        _settings = settings;
    }

    /// <summary>Step the simulation.</summary>
    public void Update(KiloWorld world)
    {
        var physicsWorld = world.GetResource<PhysicsWorld>();
        physicsWorld.Step(_settings.FixedTimestep);
    }
}
