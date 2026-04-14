using Kilo.Physics;
using System.Numerics;
using Xunit;

namespace Kilo.Physics.Tests;

public class PhysicsWorldTests
{
    [Fact]
    public void PhysicsWorld_WithDefaultSettings_CreatesSimulation()
    {
        var settings = new PhysicsSettings();
        var world = new PhysicsWorld(settings);

        Assert.NotNull(world.Simulation);
        Assert.NotNull(world.BufferPool);
        // Note: Dispose skipped due to BepuPhysics beta crash on .NET 10
    }

    [Fact]
    public void PhysicsWorld_WithCustomSettings_CreatesSimulation()
    {
        var settings = new PhysicsSettings
        {
            Gravity = new Vector3(0, -19.62f, 0),
            VelocityIterations = 16,
            SubstepCount = 4
        };

        var world = new PhysicsWorld(settings);

        Assert.NotNull(world.Simulation);
        Assert.NotNull(world.BufferPool);
    }

    [Fact]
    public void PhysicsWorld_Step_DoesNotThrow()
    {
        var settings = new PhysicsSettings();
        var world = new PhysicsWorld(settings);

        // Should not throw
        world.Step(1f / 60f);
    }

    [Fact(Skip = "BepuPhysics 2.5.0-beta.28 AccessViolationException on .NET 10")]
    public void PhysicsWorld_Dispose_DoesNotThrow()
    {
        var settings = new PhysicsSettings();
        var world = new PhysicsWorld(settings);

        // Should not throw
        world.Dispose();
    }

    [Fact]
    public void PhysicsSettings_DefaultValues()
    {
        var settings = PhysicsSettings.Default;

        Assert.Equal(new Vector3(0f, -9.81f, 0f), settings.Gravity);
        Assert.Equal(8, settings.VelocityIterations);
        Assert.Equal(2, settings.SubstepCount);
        Assert.Equal(1f / 60f, settings.FixedTimestep);
    }
}
