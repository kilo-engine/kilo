using Kilo.ECS;
using Kilo.Physics;
using Xunit;

namespace Kilo.Physics.Tests;

public class PluginRegistrationTests
{
    [Fact]
    public void PhysicsPlugin_ImplementsIKiloPlugin()
    {
        var plugin = new PhysicsPlugin();

        Assert.IsAssignableFrom<IKiloPlugin>(plugin);
    }

    [Fact]
    public void PhysicsPlugin_Build_DoesNotThrow()
    {
        var plugin = new PhysicsPlugin();
        var app = new KiloApp();

        // Should not throw
        var exception = Record.Exception(() => plugin.Build(app));

        Assert.Null(exception);
    }

    [Fact]
    public void PhysicsPlugin_Build_RegistersResources()
    {
        var plugin = new PhysicsPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        app.RunStartup();

        // Resources should be registered
        var settings = app.World.GetResource<PhysicsSettings>();
        var world = app.World.GetResource<PhysicsWorld>();

        Assert.NotNull(settings);
        Assert.NotNull(world);
    }

    [Fact]
    public void PhysicsPlugin_Build_WithCustomSettings()
    {
        var customSettings = new PhysicsSettings
        {
            Gravity = new System.Numerics.Vector3(0, -5f, 0),
            VelocityIterations = 4
        };

        var plugin = new PhysicsPlugin(customSettings);
        var app = new KiloApp();
        plugin.Build(app);

        app.RunStartup();

        var settings = app.World.GetResource<PhysicsSettings>();

        Assert.Equal(-5f, settings.Gravity.Y);
        Assert.Equal(4, settings.VelocityIterations);
    }
}
