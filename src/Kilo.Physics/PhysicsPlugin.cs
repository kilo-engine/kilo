using Kilo.ECS;

namespace Kilo.Physics;

/// <summary>
/// Main plugin for Kilo physics integration.
/// Registers physics systems and resources.
/// </summary>
public sealed class PhysicsPlugin : IKiloPlugin
{
    private readonly PhysicsSettings _settings;

    /// <summary>Create a new physics plugin with optional settings.</summary>
    public PhysicsPlugin(PhysicsSettings? settings = null)
    {
        _settings = settings ?? new PhysicsSettings();
    }

    /// <summary>Configure the physics plugin.</summary>
    public void Build(KiloApp app)
    {
        // Create and register physics world
        var physicsWorld = new PhysicsWorld(_settings);
        app.AddResource(_settings);
        app.AddResource(physicsWorld);

        // Create systems
        var syncToPhysicsSystem = new SyncToPhysicsSystem();
        var physicsStepSystem = new PhysicsStepSystem(_settings);
        var syncFromPhysicsSystem = new SyncFromPhysicsSystem();

        // Register systems in correct stage order
        // PreUpdate: sync ECS transforms to Bepu
        app.AddSystem(KiloStage.PreUpdate, syncToPhysicsSystem.Update);

        // Update: step simulation
        app.AddSystem(KiloStage.Update, physicsStepSystem.Update);

        // PostUpdate: sync Bepu bodies back to ECS
        app.AddSystem(KiloStage.PostUpdate, syncFromPhysicsSystem.Update);
    }
}
