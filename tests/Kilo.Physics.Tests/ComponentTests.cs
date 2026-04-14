using BepuPhysics.Collidables;
using Kilo.Physics;
using System.Numerics;
using Xunit;

namespace Kilo.Physics.Tests;

public class ComponentTests
{
    [Fact]
    public void PhysicsBody_DefaultValues()
    {
        var body = new PhysicsBody();

        Assert.Equal(default, body.BodyHandle);
        Assert.False(body.IsDynamic);
        Assert.False(body.IsKinematic);
        Assert.True(body.IsStatic);
    }

    [Fact]
    public void PhysicsBody_DynamicBody()
    {
        var body = new PhysicsBody
        {
            IsDynamic = true
        };

        Assert.True(body.IsDynamic);
        Assert.False(body.IsStatic);
    }

    [Fact]
    public void PhysicsShape_DefaultValues()
    {
        var shape = PhysicsShape.Default;

        Assert.Equal(new TypedIndex(0, 0), shape.ShapeIndex);
        Assert.Equal(1.0f, shape.Mass);
        Assert.Equal(0.01f, shape.CollisionMargin);
    }

    [Fact]
    public void PhysicsVelocity_DefaultValues()
    {
        var velocity = PhysicsVelocity.Zero;

        Assert.Equal(Vector3.Zero, velocity.Linear);
        Assert.Equal(Vector3.Zero, velocity.Angular);
    }

    [Fact]
    public void PhysicsVelocity_CustomValues()
    {
        var velocity = new PhysicsVelocity
        {
            Linear = new Vector3(1, 2, 3),
            Angular = new Vector3(0.5f, 0, 0)
        };

        Assert.Equal(new Vector3(1, 2, 3), velocity.Linear);
        Assert.Equal(new Vector3(0.5f, 0, 0), velocity.Angular);
    }

    [Fact]
    public void PhysicsCollider_DefaultValues()
    {
        var collider = PhysicsCollider.Default;

        Assert.Equal(-1, collider.ColliderId);
        Assert.False(collider.IsTrigger);
    }

    [Fact]
    public void PhysicsCollider_Trigger()
    {
        var collider = new PhysicsCollider
        {
            ColliderId = 42,
            IsTrigger = true
        };

        Assert.Equal(42, collider.ColliderId);
        Assert.True(collider.IsTrigger);
    }

    [Fact]
    public void Transform3D_Identity()
    {
        var transform = Transform3D.Identity;

        Assert.Equal(Vector3.Zero, transform.Position);
        Assert.Equal(Quaternion.Identity, transform.Rotation);
        Assert.Equal(Vector3.One, transform.Scale);
    }

    [Fact]
    public void Transform3D_CustomValues()
    {
        var transform = new Transform3D
        {
            Position = new Vector3(10, 20, 30),
            Rotation = Quaternion.CreateFromYawPitchRoll(0.5f, 0.3f, 0.2f),
            Scale = new Vector3(2, 2, 2)
        };

        Assert.Equal(new Vector3(10, 20, 30), transform.Position);
        Assert.Equal(2, transform.Scale.X);
    }
}
