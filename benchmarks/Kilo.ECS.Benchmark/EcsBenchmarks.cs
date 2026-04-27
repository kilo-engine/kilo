using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Friflo.Engine.ECS;

namespace Kilo.ECS.Benchmark;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RankColumn]
public class EcsBenchmarks
{
    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddColumn(StatisticColumn.P95);
            AddJob(Job.ShortRun
                .WithWarmupCount(5)
                .WithIterationCount(20));
        }
    }

    // ── Test components ──────────────────────────────────────

    struct Position : IComponent { public float X, Y; }
    struct Velocity : IComponent { public float Dx, Dy; }
    struct Health : IComponent { public int Value; }
    struct Damage : IComponent { public float Amount; }
    struct Name : IComponent { public string Value; }

    // ══════════════════════════════════════════════════════════
    //  B01: Entity Creation (100K entities, 3 components)
    // ══════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Create")]
    public void B01_CreateEntities_KiloWorld()
    {
        using var w = new KiloWorld();
        for (int i = 0; i < 100_000; i++)
        {
            w.Entity()
                .Set(new Position { X = i })
                .Set(new Velocity { Dx = i })
                .Set(new Health { Value = i });
        }
    }

    [Benchmark]
    [BenchmarkCategory("Create")]
    public void B01_CreateEntities_Friflo()
    {
        var store = new EntityStore(PidType.UsePidAsId);
        for (int i = 0; i < 100_000; i++)
        {
            var e = store.CreateEntity();
            e.AddComponent(new Position { X = i });
            e.AddComponent(new Velocity { Dx = i });
            e.AddComponent(new Health { Value = i });
        }
    }

    // ══════════════════════════════════════════════════════════
    //  B02: Query Iteration (100K entities, 2 components)
    // ══════════════════════════════════════════════════════════

    private KiloWorld _queryKiloWorld = null!;
    private EntityStore _queryFrifloStore = null!;

    [IterationSetup(Targets = new[] {
        nameof(B02_Query_KiloWorld),
        nameof(B02_Query_KiloWorld_Span),
        nameof(B02_Query_Friflo)
    })]
    public void SetupQueryWorld()
    {
        _queryKiloWorld = new KiloWorld();
        for (int i = 0; i < 100_000; i++)
            _queryKiloWorld.Entity().Set(new Position { X = i }).Set(new Velocity { Dx = 1 });

        _queryFrifloStore = new EntityStore(PidType.UsePidAsId);
        for (int i = 0; i < 100_000; i++)
        {
            var e = _queryFrifloStore.CreateEntity();
            e.AddComponent(new Position { X = i });
            e.AddComponent(new Velocity { Dx = 1 });
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Query")]
    public void B02_Query_KiloWorld()
    {
        var query = _queryKiloWorld.Query<Position, Velocity>();
        var iter = query.Iter();
        while (iter.Next())
        {
            var pos = iter.Data<Position>(0);
            var vel = iter.Data<Velocity>(1);
            for (int i = 0; i < iter.Count; i++)
                pos[i].X += vel[i].Dx;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Query")]
    public void B02_Query_KiloWorld_Span()
    {
        var query = _queryKiloWorld.Query<Position, Velocity>();
        var iter = query.Iter();
        while (iter.Next())
        {
            var pos = iter.Span0;
            var vel = iter.Span1;
            for (int i = 0; i < iter.Count; i++)
                pos[i].X += vel[i].Dx;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Query")]
    public void B02_Query_Friflo()
    {
        var query = _queryFrifloStore.Query<Position, Velocity>();
        foreach (var (positions, velocities, entities) in query.Chunks)
        {
            var pos = positions.Span;
            var vel = velocities.Span;
            for (int i = 0; i < positions.Length; i++)
                pos[i].X += vel[i].Dx;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  B03: Component Get/Set (100 entities, 5 components)
    // ══════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("GetSet")]
    public void B03_GetSet_KiloWorld()
    {
        using var w = new KiloWorld();
        var ids = new EntityId[100];
        for (int i = 0; i < 100; i++)
        {
            ids[i] = w.Entity()
                .Set(new Position())
                .Set(new Velocity())
                .Set(new Health())
                .Set(new Damage())
                .Id;
        }

        for (int i = 0; i < 100; i++)
        {
            ref var pos = ref w.Get<Position>(ids[i]);
            ref var vel = ref w.Get<Velocity>(ids[i]);
            ref var hp = ref w.Get<Health>(ids[i]);
            ref var dmg = ref w.Get<Damage>(ids[i]);
            pos.X = vel.Dx;
            hp.Value = (int)dmg.Amount;
        }
    }

    [Benchmark]
    [BenchmarkCategory("GetSet")]
    public void B03_GetSet_Friflo()
    {
        var store = new EntityStore(PidType.UsePidAsId);
        var ids = new int[100];
        for (int i = 0; i < 100; i++)
        {
            var e = store.CreateEntity();
            e.AddComponent<Position>(default);
            e.AddComponent<Velocity>(default);
            e.AddComponent<Health>(default);
            e.AddComponent<Damage>(default);
            ids[i] = e.Id;
        }

        for (int i = 0; i < 100; i++)
        {
            var e = store.GetEntityById(ids[i]);
            ref var pos = ref e.GetComponent<Position>();
            ref var vel = ref e.GetComponent<Velocity>();
            ref var hp = ref e.GetComponent<Health>();
            ref var dmg = ref e.GetComponent<Damage>();
            pos.X = vel.Dx;
            hp.Value = (int)dmg.Amount;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  B04: World Creation
    // ══════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("World")]
    public void B04_WorldCreate_KiloWorld()
    {
        using var w = new KiloWorld();
    }

    [Benchmark]
    [BenchmarkCategory("World")]
    public void B04_WorldCreate_Friflo()
    {
        var store = new EntityStore(PidType.UsePidAsId);
    }

    // ══════════════════════════════════════════════════════════
    //  B05: Entity Delete (10K entities)
    // ══════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Delete")]
    public void B05_Delete_KiloWorld()
    {
        using var w = new KiloWorld();
        var ids = new EntityId[10_000];
        for (int i = 0; i < 10_000; i++)
            ids[i] = w.Entity().Set(new Position()).Id;

        for (int i = 0; i < 10_000; i++)
            w.Delete(ids[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Delete")]
    public void B05_Delete_Friflo()
    {
        var store = new EntityStore(PidType.UsePidAsId);
        var ids = new int[10_000];
        for (int i = 0; i < 10_000; i++)
        {
            var e = store.CreateEntity();
            e.AddComponent<Position>(default);
            ids[i] = e.Id;
        }

        for (int i = 0; i < 10_000; i++)
            store.GetEntityById(ids[i]).DeleteEntity();
    }

    // ══════════════════════════════════════════════════════════
    //  B06: Hierarchy (100 parents x 10 children)
    // ══════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Hierarchy")]
    public void B06_Hierarchy_KiloWorld()
    {
        using var w = new KiloWorld();
        for (int p = 0; p < 100; p++)
        {
            var parent = w.Entity();
            for (int c = 0; c < 10; c++)
            {
                var child = w.Entity();
                parent.AddChild(child);
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Hierarchy")]
    public void B06_Hierarchy_Friflo()
    {
        var store = new EntityStore(PidType.UsePidAsId);
        for (int p = 0; p < 100; p++)
        {
            var parent = store.CreateEntity();
            for (int c = 0; c < 10; c++)
            {
                var child = store.CreateEntity();
                parent.AddChild(child);
            }
        }
    }
}
