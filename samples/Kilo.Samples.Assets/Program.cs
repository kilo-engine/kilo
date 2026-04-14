// ── Assets Sample: asset registration, loading, and references ──
using Kilo.ECS;
using Kilo.Assets;

var app = new KiloApp();
app.AddPlugin(new AssetsPlugin(new AssetSettings
{
    RootPath = "assets",
    EnableHotReload = false
}));

// ── Startup: register assets and create entity references ──────
app.AddSystem(KiloStage.Startup, world =>
{
    var manager = world.GetResource<AssetManager>();

    // Register some assets by path
    var texture1 = manager.Register<object>("textures/player.png");
    var texture2 = manager.Register<object>("textures/enemy.png");
    var audioClip = manager.Register<object>("audio/jump.wav");

    Console.WriteLine($"Registered {manager.LoadedCount} assets.");
    Console.WriteLine($"  texture1 valid: {texture1.IsValid}");
    Console.WriteLine($"  texture2 valid: {texture2.IsValid}");

    // Create entities with asset references
    world.Entity("Player")
        .Set(new AssetReference(texture1.Id, "textures/player.png"));

    world.Entity("Enemy")
        .Set(new AssetReference(texture2.Id, "textures/enemy.png"));

    world.Entity("SoundEmitter")
        .Set(new AssetReference(audioClip.Id, "audio/jump.wav"));

    // Simulate loading an asset
    manager.Store(texture1, "fake_texture_data");
    Console.WriteLine($"  texture1 loaded: {manager.IsLoaded(texture1)}");
    Console.WriteLine($"  texture2 loaded: {manager.IsLoaded(texture2)}");

    if (manager.TryGet(texture1, out var data))
        Console.WriteLine($"  texture1 data: {data}");
});

// ── Update: check asset load status ────────────────────────────
app.AddSystem(KiloStage.Last, world =>
{
    var manager = world.GetResource<AssetManager>();

    var query = world.QueryBuilder().With<AssetReference>().Build();
    var iter = query.Iter();
    while (iter.Next())
    {
        var refs = iter.Data<AssetReference>(0);
        for (int i = 0; i < iter.Count; i++)
        {
            ref readonly var ar = ref refs[i];
            string status = ar.IsLoaded ? "loaded" : "pending";
            Console.WriteLine($"  Asset {ar.AssetId} ({ar.Path}): {status}");
        }
    }

    Console.WriteLine($"  Total loaded: {manager.LoadedCount}");
});

Console.WriteLine("Frame 1:");
app.Run();

Console.WriteLine("\nFrame 2:");
app.Run();

// ── Cleanup ────────────────────────────────────────────────────
var mgr = app.World.GetResource<AssetManager>();
mgr.Clear();
Console.WriteLine($"\nAfter clear: {mgr.LoadedCount} assets loaded.");
Console.WriteLine("Done!");
