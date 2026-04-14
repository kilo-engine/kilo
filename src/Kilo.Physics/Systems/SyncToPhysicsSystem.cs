using BepuPhysics;
using BepuUtilities;
using Kilo.ECS;
using System.Numerics;

namespace Kilo.Physics;

/// <summary>
/// Synchronizes ECS transforms to physics bodies.
/// Runs in the PreUpdate stage.
/// </summary>
public sealed class SyncToPhysicsSystem
{
    /// <summary>Sync transform data from ECS to Bepu physics.</summary>
    public void Update(KiloWorld world)
    {
        var physicsWorld = world.GetResource<PhysicsWorld>();
        var simulation = physicsWorld.Simulation;

        // Query all entities with PhysicsBody and PhysicsShape
        var query = world.QueryBuilder()
            .With<PhysicsBody>()
            .With<PhysicsShape>()
            .With<Transform3D>()
            .Build();

        var iter = query.Iter();
        int bodyColumn = 0;
        int transformColumn = 0;

        while (iter.Next())
        {
            var bodies = iter.Data<PhysicsBody>(iter.GetColumnIndexOf<PhysicsBody>());
            var transforms = iter.Data<Transform3D>(iter.GetColumnIndexOf<Transform3D>());

            for (int i = 0; i < iter.Count; i++)
            {
                ref readonly var body = ref bodies[i];
                ref readonly var transform = ref transforms[i];

                if (simulation.Bodies.BodyExists(body.BodyHandle))
                {
                    var bepuBody = simulation.Bodies[body.BodyHandle];

                    // Update position
                    bepuBody.Pose.Position = new System.Numerics.Vector3(
                        transform.Position.X,
                        transform.Position.Y,
                        transform.Position.Z);

                    // Update rotation
                    bepuBody.Pose.Orientation = new System.Numerics.Quaternion(
                        transform.Rotation.X,
                        transform.Rotation.Y,
                        transform.Rotation.Z,
                        transform.Rotation.W);
                }
            }
        }
    }
}

/// <summary>
/// Simple 3D transform component for physics testing.
/// In a full implementation, this would be shared with the Rendering plugin.
/// </summary>
public struct Transform3D
{
    /// <summary>Position in world space.</summary>
    public Vector3 Position;

    /// <summary>Rotation quaternion.</summary>
    public Quaternion Rotation;

    /// <summary>Scale vector.</summary>
    public Vector3 Scale;

    /// <summary>Identity transform.</summary>
    public static Transform3D Identity => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        Scale = Vector3.One
    };
}
