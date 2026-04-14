using BepuPhysics;
using BepuUtilities.Memory;

namespace Kilo.Physics;

/// <summary>
/// Main physics simulation world.
/// Wraps Bepu's Simulation and provides stepping functionality.
/// </summary>
public sealed class PhysicsWorld : IDisposable
{
    /// <summary>Underlying Bepu simulation.</summary>
    public Simulation Simulation { get; }

    /// <summary>Memory pool for the simulation.</summary>
    public BufferPool BufferPool { get; }

    private readonly KiloPoseIntegratorCallbacks _poseIntegratorCallbacks;

    /// <summary>Create a new physics world with the given settings.</summary>
    public PhysicsWorld(PhysicsSettings settings)
    {
        BufferPool = new BufferPool();
        _poseIntegratorCallbacks = new KiloPoseIntegratorCallbacks(settings.Gravity);

        Simulation = Simulation.Create(
            BufferPool,
            new KiloNarrowPhaseCallbacks(),
            _poseIntegratorCallbacks,
            new SolveDescription(settings.VelocityIterations, settings.SubstepCount));
    }

    /// <summary>Step the simulation forward by the given delta time.</summary>
    public void Step(float deltaTime)
    {
        Simulation.Timestep(deltaTime);
    }

    /// <summary>Dispose of simulation resources.</summary>
    public void Dispose()
    {
        BufferPool.Clear();
        Simulation.Dispose();
    }
}
