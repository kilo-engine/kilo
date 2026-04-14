// ── Hello ECS: Minimal Kilo engine example ──────────────────
using Kilo.ECS;

// ── Main ─────────────────────────────────────────────────────
var app = new KiloApp();

// Add plugins
app.AddPlugin(new MovementPlugin());

// Spawn entities at startup
app.AddSystem(KiloStage.Startup, world =>
{
    for (int i = 0; i < 5; i++)
    {
        world.Entity()
            .Set(new Position { X = i * 10, Y = 0 })
            .Set(new Velocity { Dx = 1, Dy = 0.5f });
    }
    Console.WriteLine($"Spawned 5 entities. Total: {world.EntityCount}");
});

// Print positions each frame
app.AddSystem(KiloStage.Last, world =>
{
    var query = world.QueryBuilder()
        .With<Position>()
        .With<Velocity>()
        .Build();

    var iter = query.Iter();
    while (iter.Next())
    {
        var positions = iter.Data<Position>(0);
        for (int i = 0; i < iter.Count; i++)
        {
            Console.WriteLine($"  Entity at ({positions[i].X:F2}, {positions[i].Y:F2})");
        }
    }
});

// Run 3 frames
Console.WriteLine("Frame 1:");
app.Run();

Console.WriteLine("\nFrame 2:");
app.Run();

Console.WriteLine("\nFrame 3:");
app.Run();

Console.WriteLine("\nDone!");

// ── Components ───────────────────────────────────────────────
struct Position { public float X, Y; }
struct Velocity { public float Dx, Dy; }

// ── Plugin ───────────────────────────────────────────────────
struct MovementPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddSystem(KiloStage.Update, world =>
        {
            var query = world.QueryBuilder()
                .With<Position>()
                .With<Velocity>()
                .Build();

            var iter = query.Iter();
            while (iter.Next())
            {
                var positions = iter.Data<Position>(0);
                var velocities = iter.Data<Velocity>(1);
                for (int i = 0; i < iter.Count; i++)
                {
                    positions[i].X += velocities[i].Dx * 0.016f;
                    positions[i].Y += velocities[i].Dy * 0.016f;
                }
            }
        });
    }
}
