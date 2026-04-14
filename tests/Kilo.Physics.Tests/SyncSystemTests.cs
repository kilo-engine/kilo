using Kilo.ECS;
using Kilo.Physics;
using System.Numerics;
using Xunit;

namespace Kilo.Physics.Tests;

public class SyncSystemTests
{
    [Fact]
    public void SyncSystems_CanBeCreated()
    {
        var syncTo = new SyncToPhysicsSystem();
        var syncFrom = new SyncFromPhysicsSystem();

        Assert.NotNull(syncTo);
        Assert.NotNull(syncFrom);
    }

    [Fact]
    public void SyncFromPhysicsSystem_Update_WithNoEntities_DoesNotThrow()
    {
        var world = new KiloWorld();
        var physicsWorld = new PhysicsWorld(new PhysicsSettings());

        // Register as resource
        world.AddResource(physicsWorld);

        var system = new SyncFromPhysicsSystem();

        // Should not throw with no entities
        var exception = Record.Exception(() => system.Update(world));

        Assert.Null(exception);
    }

    [Fact]
    public void SyncToPhysicsSystem_Update_WithNoEntities_DoesNotThrow()
    {
        var world = new KiloWorld();
        var physicsWorld = new PhysicsWorld(new PhysicsSettings());

        // Register as resource
        world.AddResource(physicsWorld);

        var system = new SyncToPhysicsSystem();

        // Should not throw with no entities
        var exception = Record.Exception(() => system.Update(world));

        Assert.Null(exception);
    }

    [Fact]
    public void PhysicsWorld_WithFullPlugin_RunUpdate_DoesNotThrow()
    {
        var plugin = new PhysicsPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        // Run startup
        var exception = Record.Exception(() => app.RunStartup());

        Assert.Null(exception);

        // Run a single update
        exception = Record.Exception(() => app.Update());

        Assert.Null(exception);
    }

    [Fact]
    public void Transform3D_CanBeSetOnEntity()
    {
        var world = new KiloWorld();
        var entity = world.Entity().Id;

        var transform = new Transform3D
        {
            Position = new Vector3(5, 10, 15),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        };

        world.Set(entity, transform);

        Assert.True(world.Has<Transform3D>(entity));
        ref var retrieved = ref world.Get<Transform3D>(entity);
        Assert.Equal(new Vector3(5, 10, 15), retrieved.Position);
    }
}
