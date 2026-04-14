using Kilo.Physics;
using System.Numerics;
using Xunit;

namespace Kilo.Physics.Tests;

public class PhysicsSettingsTests
{
    [Fact]
    public void PhysicsSettings_Constructor_SetsDefaults()
    {
        var settings = new PhysicsSettings();

        Assert.Equal(new Vector3(0f, -9.81f, 0f), settings.Gravity);
        Assert.Equal(8, settings.VelocityIterations);
        Assert.Equal(2, settings.SubstepCount);
        Assert.Equal(1f / 60f, settings.FixedTimestep);
    }

    [Fact]
    public void PhysicsSettings_CanModifyGravity()
    {
        var settings = new PhysicsSettings();
        settings.Gravity = new Vector3(0, -5f, 0);

        Assert.Equal(-5f, settings.Gravity.Y);
    }

    [Fact]
    public void PhysicsSettings_CanModifyIterations()
    {
        var settings = new PhysicsSettings();
        settings.VelocityIterations = 16;
        settings.SubstepCount = 8;

        Assert.Equal(16, settings.VelocityIterations);
        Assert.Equal(8, settings.SubstepCount);
    }

    [Fact]
    public void PhysicsSettings_CanModifyFixedTimestep()
    {
        var settings = new PhysicsSettings();
        settings.FixedTimestep = 1f / 120f;

        Assert.Equal(1f / 120f, settings.FixedTimestep);
    }

    [Fact]
    public void PhysicsSettings_Default_StaticProperty()
    {
        var settings = PhysicsSettings.Default;

        Assert.NotNull(settings);
        Assert.Equal(new Vector3(0f, -9.81f, 0f), settings.Gravity);
    }
}
