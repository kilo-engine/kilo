// ── Physics Sample: BepuPhysics simulation through Kilo.Physics ──
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using Kilo.ECS;
using Kilo.Physics;

var app = new KiloApp();

app.AddPlugin(new PhysicsPlugin(new PhysicsSettings
{
    Gravity = new Vector3(0, -9.81f, 0),
    VelocityIterations = 8,
    SubstepCount = 2,
    FixedTimestep = 1f / 60f
}));

// ── Startup: set up a ground plane + falling spheres ────────────
app.AddSystem(KiloStage.Startup, world =>
{
    var physicsWorld = world.GetResource<PhysicsWorld>();
    var simulation = physicsWorld.Simulation;

    // Static ground plane
    simulation.Statics.Add(new StaticDescription(
        new Vector3(0, -0.5f, 0),
        simulation.Shapes.Add(new Box(100, 1, 100))));

    // Falling spheres
    float[] heights = [5f, 8f, 12f];
    for (int i = 0; i < heights.Length; i++)
    {
        var shapeIndex = simulation.Shapes.Add(new Sphere(0.5f));
        var handle = simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new Vector3(i * 2 - 2, heights[i], 0),
            new BodyInertia { InverseMass = 1f },
            shapeIndex,
            0.1f));

        // ECS entity mirrors the Bepu body
        world.Entity($"Ball{i}")
            .Set(new Transform3D
            {
                Position = new Vector3(i * 2 - 2, heights[i], 0),
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            })
            .Set(new PhysicsBody { BodyHandle = handle, IsDynamic = true })
            .Set(new PhysicsVelocity());
    }

    Console.WriteLine("Physics scene: 3 spheres falling onto a ground plane.\n");
});

// ── Last: read Bepu body states and print ───────────────────────
app.AddSystem(KiloStage.Last, world =>
{
    var physicsWorld = world.GetResource<PhysicsWorld>();
    var simulation = physicsWorld.Simulation;

    // Query ECS entities to get their Bepu body handles
    var query = world.QueryBuilder()
        .With<PhysicsBody>()
        .Build();

    Console.WriteLine($"  Tick {world.CurrentTick:D2}:");

    var iter = query.Iter();
    while (iter.Next())
    {
        var bodies = iter.Data<PhysicsBody>(0);
        for (int i = 0; i < iter.Count; i++)
        {
            ref readonly var body = ref bodies[i];
            if (body.IsDynamic && simulation.Bodies.BodyExists(body.BodyHandle))
            {
                var bepuBody = simulation.Bodies[body.BodyHandle];
                Console.WriteLine(
                    $"    Y={bepuBody.Pose.Position.Y,7:F2}  " +
                    $"vel={bepuBody.Velocity.Linear.Y,7:F2} m/s");
            }
        }
    }
});

// Run 15 frames
for (int i = 0; i < 15; i++)
    app.Run();

Console.WriteLine("\nDone! Spheres accelerate under gravity and settle on the ground.");
