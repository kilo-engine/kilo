using BepuPhysics;
using Kilo.ECS;
using System.Numerics;

namespace Kilo.Physics;

/// <summary>
/// Synchronizes physics bodies back to ECS components.
/// Runs in the PostUpdate stage.
/// </summary>
public sealed class SyncFromPhysicsSystem
{
    /// <summary>Sync transform data from Bepu physics to ECS.</summary>
    public void Update(KiloWorld world)
    {
        var physicsWorld = world.GetResource<PhysicsWorld>();
        var simulation = physicsWorld.Simulation;

        // Query all entities with PhysicsBody
        var query = world.QueryBuilder()
            .With<PhysicsBody>()
            .Build();

        var iter = query.Iter();
        while (iter.Next())
        {
            var bodies = iter.Data<PhysicsBody>(iter.GetColumnIndexOf<PhysicsBody>());

            // Get column indices before the loop
            int velocityColumn = iter.GetColumnIndexOf<PhysicsVelocity>();
            int transformColumn = iter.GetColumnIndexOf<Transform3D>();

            for (int i = 0; i < iter.Count; i++)
            {
                ref readonly var body = ref bodies[i];

                if (simulation.Bodies.BodyExists(body.BodyHandle))
                {
                    var bepuBody = simulation.Bodies[body.BodyHandle];

                    // Sync velocity if the entity has PhysicsVelocity
                    if (velocityColumn >= 0)
                    {
                        var velocities = iter.Data<PhysicsVelocity>(velocityColumn);
                        var velocity = velocities[i];

                        velocity.Linear = new Vector3(
                            bepuBody.Velocity.Linear.X,
                            bepuBody.Velocity.Linear.Y,
                            bepuBody.Velocity.Linear.Z);

                        velocity.Angular = new Vector3(
                            bepuBody.Velocity.Angular.X,
                            bepuBody.Velocity.Angular.Y,
                            bepuBody.Velocity.Angular.Z);

                        // Set the updated velocity back to the entity
                        var entities = iter.Entities();
                        world.Set(entities[i].ID, velocity);
                    }

                    // Sync transform if the entity has Transform3D
                    if (transformColumn >= 0)
                    {
                        var transforms = iter.Data<Transform3D>(transformColumn);
                        var transform = transforms[i];

                        transform.Position = new Vector3(
                            bepuBody.Pose.Position.X,
                            bepuBody.Pose.Position.Y,
                            bepuBody.Pose.Position.Z);

                        transform.Rotation = new Quaternion(
                            bepuBody.Pose.Orientation.X,
                            bepuBody.Pose.Orientation.Y,
                            bepuBody.Pose.Orientation.Z,
                            bepuBody.Pose.Orientation.W);

                        // Set the updated transform back to the entity
                        var entities = iter.Entities();
                        world.Set(entities[i].ID, transform);
                    }
                }
            }
        }
    }
}
