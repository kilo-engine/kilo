using Friflo.Engine.ECS;
using Xunit;

namespace Kilo.ECS.Tests;

/// <summary>Tests for all newly implemented Bevy-equivalent features.</summary>
public sealed class NewFeaturesTests : IDisposable
{
    private readonly KiloWorld _world = new();
    public void Dispose() => _world.Dispose();

    // ── Test component types ──────────────────────────────────────

    struct Pos : IComponent { public float X, Y; }
    struct Vel : IComponent { public float Dx, Dy; }
    struct Health : IComponent { public float Value; }
    struct Tag : IComponent { public bool On; }

    // ── With/Without Filter ────────────────────────────────────────

    [Fact]
    public void Query_With_Filter_OnlyMatching()
    {
        // e1: Pos + Vel
        _world.Entity().Set(new Pos()).Set(new Vel());
        // e2: Pos only
        _world.Entity().Set(new Pos());
        // e3: Pos + Vel + Health
        _world.Entity().Set(new Pos()).Set(new Vel()).Set(new Health());

        var q = _world.Query<Pos>().With<Vel>();
        int count = 0;
        q.ForEach((ref Pos p) => count++);
        Assert.Equal(2, count); // e1 and e3
    }

    [Fact]
    public void Query_Without_Filter_Excludes()
    {
        _world.Entity().Set(new Pos()).Set(new Vel());
        _world.Entity().Set(new Pos());
        _world.Entity().Set(new Pos()).Set(new Vel()).Set(new Health());

        var q = _world.Query<Pos>().Without<Vel>();
        int count = 0;
        q.ForEach((ref Pos p) => count++);
        Assert.Equal(1, count); // only e2
    }

    [Fact]
    public void Query_With_Without_Combined()
    {
        _world.Entity().Set(new Pos()).Set(new Vel());              // e1
        _world.Entity().Set(new Pos());                              // e2
        _world.Entity().Set(new Pos()).Set(new Vel()).Set(new Health()); // e3

        var q = _world.Query<Pos>().With<Vel>().Without<Health>();
        int count = 0;
        q.ForEach((ref Pos p) => count++);
        Assert.Equal(1, count); // only e1
    }

    // ── Or Filter ──────────────────────────────────────────────────

    [Fact]
    public void Query_Or_Filter()
    {
        _world.Entity().Set(new Pos());                              // e1: Pos only
        _world.Entity().Set(new Pos()).Set(new Vel());               // e2: Pos + Vel
        _world.Entity().Set(new Pos()).Set(new Health());            // e3: Pos + Health

        var q = _world.Query<Pos>().Or<Vel, Health>();
        int count = 0;
        q.ForEach((ref Pos p) => count++);
        Assert.Equal(2, count); // e2 and e3 have Vel or Health
    }

    // ── Changed/Added Filter ───────────────────────────────────────

    [Fact]
    public void Query_Changed_Filter()
    {
        var e1 = _world.Entity().Set(new Pos { X = 1 });
        var e2 = _world.Entity().Set(new Pos { X = 2 });

        _world.Update(); // clear change tracking

        // Mark e1 as changed via GetPtr
        e1.GetPtr<Pos>().Ref.X = 10;
        // e2 unchanged

        var q = _world.Query<Pos>().Changed();
        int count = 0;
        q.ForEach((ref Pos p) => count++);
        Assert.Equal(1, count); // only e1
    }

    [Fact]
    public void Query_Added_Filter()
    {
        // Wire events so added tracking works
        _world.WireEvents();

        _world.Entity().Set(new Pos { X = 1 }); // e1 added this frame
        _world.Entity().Set(new Pos { X = 2 }); // e2 added this frame

        var q = _world.Query<Pos>().Added();
        int count = 0;
        q.ForEach((ref Pos p) => count++);
        Assert.Equal(2, count); // both added this frame

        _world.Update(); // clear added tracking

        var q2 = _world.Query<Pos>().Added();
        int count2 = 0;
        q2.ForEach((ref Pos p) => count2++);
        Assert.Equal(0, count2); // none added after clear
    }

    // ── State Enter/Exit/Transition ────────────────────────────────

    enum GameState { Menu, Playing, Paused }

    [Fact]
    public void State_OnEnter_Fires()
    {
        var app = new KiloApp(_world);
        app.AddState(GameState.Menu);

        GameState? enteredState = null;
        app.OnEnter<GameState>(GameState.Playing, w => enteredState = GameState.Playing);

        app.Update(); // no transition yet
        Assert.Null(enteredState);

        // Queue transition
        _world.GetResource<NextState<GameState>>().Set(GameState.Playing);
        app.Update();
        Assert.Equal(GameState.Playing, enteredState);
    }

    [Fact]
    public void State_OnExit_Fires()
    {
        var app = new KiloApp(_world);
        app.AddState(GameState.Menu);

        GameState? exitedState = null;
        app.OnExit<GameState>(GameState.Menu, w => exitedState = GameState.Menu);

        _world.GetResource<NextState<GameState>>().Set(GameState.Playing);
        app.Update();
        Assert.Equal(GameState.Menu, exitedState);
    }

    [Fact]
    public void State_OnTransition_Fires()
    {
        var app = new KiloApp(_world);
        app.AddState(GameState.Menu);

        (GameState from, GameState to)? transition = null;
        app.OnTransition<GameState>((w, from, to) => transition = (from, to));

        _world.GetResource<NextState<GameState>>().Set(GameState.Playing);
        app.Update();
        Assert.NotNull(transition);
        Assert.Equal(GameState.Menu, transition.Value.from);
        Assert.Equal(GameState.Playing, transition.Value.to);
    }

    // ── Run Condition ──────────────────────────────────────────────

    [Fact]
    public void RunCondition_SystemSkipped()
    {
        var app = new KiloApp(_world);
        bool ran = false;
        app.AddSystem(KiloStage.Update, w => ran = true, w => false);
        app.Update();
        Assert.False(ran);
    }

    [Fact]
    public void RunCondition_SystemRuns()
    {
        var app = new KiloApp(_world);
        bool ran = false;
        app.AddSystem(KiloStage.Update, w => ran = true, w => true);
        app.Update();
        Assert.True(ran);
    }

    [Fact]
    public void RunCondition_StateEquals()
    {
        var app = new KiloApp(_world);
        app.AddState(GameState.Menu);

        bool ran = false;
        app.AddSystem<GameState>(KiloStage.Update, GameState.Playing, w => ran = true);

        // Currently Menu, should not run
        app.Update();
        Assert.False(ran);

        // Transition to Playing
        _world.GetResource<NextState<GameState>>().Set(GameState.Playing);
        app.Update(); // transition happens
        app.Update(); // now Playing, system should run
        Assert.True(ran);
    }

    // ── EntityCommands ─────────────────────────────────────────────

    [Fact]
    public void EntityCommands_Insert_Deferred()
    {
        _world.BeginDeferred();
        var e = _world.Entity();
        var cmd = _world.EntityDeferred(e.Id);
        cmd.Insert(new Pos { X = 42 });

        // Not yet applied
        Assert.False(_world.Has<Pos>(e.Id));

        _world.EndDeferred();
        Assert.True(_world.Has<Pos>(e.Id));
    }

    [Fact]
    public void EntityCommands_Remove_Deferred()
    {
        var e = _world.Entity().Set(new Pos());
        _world.BeginDeferred();
        var cmd = _world.EntityDeferred(e.Id);
        cmd.Remove<Pos>();

        Assert.True(_world.Has<Pos>(e.Id)); // not yet applied
        _world.EndDeferred();
        Assert.False(_world.Has<Pos>(e.Id));
    }

    [Fact]
    public void EntityCommands_Spawn_Deferred()
    {
        _world.BeginDeferred();
        var cmd = _world.Commands().Spawn();
        cmd.Insert(new Pos { X = 99 });
        _world.EndDeferred();

        // Verify entity was created with component
        var found = false;
        _world.Query<Pos>().ForEach((ref Pos p) =>
        {
            if (p.X == 99) found = true;
        });
        Assert.True(found);
    }

    // ── RemoveResource ─────────────────────────────────────────────

    [Fact]
    public void RemoveResource_Works()
    {
        _world.AddResource(42);
        Assert.True(_world.HasResource<int>());
        _world.RemoveResource<int>();
        Assert.False(_world.HasResource<int>());
    }

    // ── IterEntities ───────────────────────────────────────────────

    [Fact]
    public void IterEntities_ReturnsAll()
    {
        _world.Entity();
        _world.Entity();
        _world.Entity();
        var ids = _world.IterEntities();
        Assert.Equal(3, ids.Length);
    }

    // ── CloneEntity ────────────────────────────────────────────────

    [Fact]
    public void CloneEntity_CopiesComponents()
    {
        var e = _world.Entity().Set(new Pos { X = 5 }).Set(new Vel { Dx = 10 });
        var cloned = _world.CloneEntity(e.Id);

        Assert.NotEqual(e.Id, cloned);
        Assert.True(_world.Has<Pos>(cloned));
        Assert.True(_world.Has<Vel>(cloned));
        Assert.Equal(5f, _world.Get<Pos>(cloned).X);
        Assert.Equal(10f, _world.Get<Vel>(cloned).Dx);
    }

    // ── RunSystemOnce ──────────────────────────────────────────────

    [Fact]
    public void RunSystemOnce_Executes()
    {
        var app = new KiloApp(_world);
        bool ran = false;
        app.RunSystemOnce(w => ran = true);
        Assert.True(ran);
    }

    // ── HasAdded ───────────────────────────────────────────────────

    [Fact]
    public void HasAdded_TracksComponents()
    {
        _world.WireEvents();
        var e = _world.Entity().Set(new Pos());
        Assert.True(_world.HasAdded<Pos>(e.Id));

        _world.Update();
        Assert.False(_world.HasAdded<Pos>(e.Id)); // cleared
    }

    // ── Dynamic Get/Set by ID ──────────────────────────────────────

    [Fact]
    public void ById_Get_Set_Works()
    {
        var posId = KiloWorld.RegisterComponentType<Pos>();
        var e = _world.Entity().Set(new Pos { X = 42 });

        // Has by ID
        Assert.True(_world.Has(e.Id, posId));

        // Get by ID (boxed)
        var boxed = _world.Get(e.Id, posId);
        Assert.IsType<Pos>(boxed);
        Assert.Equal(42f, ((Pos)boxed).X);

        // Set by ID (boxed)
        _world.Set(e.Id, posId, new Pos { X = 99 });
        Assert.Equal(99f, _world.Get<Pos>(e.Id).X);

        // GetComponentType
        Assert.Equal(typeof(Pos), KiloWorld.GetComponentType(posId));
    }

    // ── SystemSet ───────────────────────────────────────────────────

    [Fact]
    public void SystemSet_RunCondition_AppliesToAll()
    {
        var app = new KiloApp(_world);
        bool sys1Ran = false, sys2Ran = false;

        app.AddSystemToSet("logic", KiloStage.Update, w => sys1Ran = true);
        app.AddSystemToSet("logic", KiloStage.Update, w => sys2Ran = true);
        app.ConfigureSet("logic", w => false); // disable the whole set

        app.Update();
        Assert.False(sys1Ran);
        Assert.False(sys2Ran);

        // Re-enable
        app.ConfigureSet("logic", w => true);
        app.Update();
        Assert.True(sys1Ran);
        Assert.True(sys2Ran);
    }

    // ── EventBus ────────────────────────────────────────────────────

    struct DamageEvent { public int Amount; public ulong Target; }

    [Fact]
    public void EventBus_SendAndRead()
    {
        var events = _world.Events<DamageEvent>();
        events.Send(new DamageEvent { Amount = 10, Target = 1 });
        events.Send(new DamageEvent { Amount = 20, Target = 2 });

        Assert.Equal(2, events.CurrentCount);
        var span = events.Read();
        Assert.Equal(10, span[0].Amount);
        Assert.Equal(20, span[1].Amount);
    }

    [Fact]
    public void EventBus_DoubleBuffer()
    {
        var events = _world.Events<DamageEvent>();
        events.Send(new DamageEvent { Amount = 5 });

        // Before update: current has 5
        Assert.Equal(1, events.CurrentCount);
        Assert.Equal(5, events.Read()[0].Amount);

        // Update: current (5) → previous, new empty current
        events.Update();
        Assert.Equal(0, events.CurrentCount);

        // Previous frame's events still accessible
        Assert.Equal(1, events.ReadPrevious().Length);
        Assert.Equal(5, events.ReadPrevious()[0].Amount);

        // Send new events
        events.Send(new DamageEvent { Amount = 15 });
        Assert.Equal(1, events.CurrentCount);

        // Update again: current (15) → previous, old previous (5) dropped
        events.Update();
        var prev = events.ReadPrevious();
        Assert.Equal(1, prev.Length);
        Assert.Equal(15, prev[0].Amount);
    }

    [Fact]
    public void EventBus_Shortcut_API()
    {
        _world.SendEvent(new DamageEvent { Amount = 42 });
        var span = _world.ReadEvents<DamageEvent>();
        Assert.Equal(42, span[0].Amount);
    }

    // ── State Changed Flag ──────────────────────────────────────────

    enum Mode { A, B }

    [Fact]
    public void State_IsChanged_ClearsAfterFrame()
    {
        var app = new KiloApp(_world);
        app.AddState(Mode.A);

        var state = _world.GetResource<State<Mode>>();
        Assert.False(state.IsChanged);

        _world.GetResource<NextState<Mode>>().Set(Mode.B);
        app.Update(); // transition fires, then ClearAllStateChanged runs
        // State changed this frame but was immediately cleared by ClearAllStateChanged
        Assert.False(state.IsChanged);

        // No transition this frame
        app.Update();
        Assert.False(state.IsChanged);
    }
}
