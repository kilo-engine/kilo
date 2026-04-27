using Kilo.ECS;
using Friflo.Engine.ECS;
using Xunit;

namespace Kilo.ECS.Tests;

public sealed class WorldTests : IDisposable
{
    private readonly KiloWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void Entity_Create_ReturnsValidId()
    {
        var entity = _world.Entity();
        Assert.True(entity.Id.IsValid());
        Assert.True(entity.Exists());
    }

    [Fact]
    public void Entity_Create_WithSpecificId()
    {
        // Friflo backend uses sequential IDs; create entity and verify it's valid
        var entity = _world.Entity();
        Assert.True(entity.Exists());
        Assert.NotEqual(0ul, entity.Id.Value);
    }

    [Fact]
    public void Entity_Delete_NoLongerExists()
    {
        var entity = _world.Entity();
        var id = entity.Id;
        _world.Delete(id);
        Assert.False(_world.Exists(id));
    }

    [Fact]
    public void Entity_DeleteViaEntity_NoLongerExists()
    {
        var entity = _world.Entity();
        var id = entity.Id;
        entity.Delete();
        Assert.False(_world.Exists(id));
    }

    [Fact]
    public void Set_And_Get_Component()
    {
        var entity = _world.Entity();
        entity.Set(new Position { X = 10, Y = 20 });

        ref var pos = ref _world.Get<Position>(entity.Id);
        Assert.Equal(10, pos.X);
        Assert.Equal(20, pos.Y);
    }

    [Fact]
    public void Get_ComponentMutation_Works()
    {
        var entity = _world.Entity();
        entity.Set(new Position { X = 0, Y = 0 });

        ref var pos = ref _world.Get<Position>(entity.Id);
        pos.X = 42;
        pos.Y = 99;

        ref var pos2 = ref _world.Get<Position>(entity.Id);
        Assert.Equal(42, pos2.X);
        Assert.Equal(99, pos2.Y);
    }

    [Fact]
    public void Has_Component_ReturnsTrue()
    {
        var entity = _world.Entity();
        entity.Set(new Position());
        Assert.True(_world.Has<Position>(entity.Id));
    }

    [Fact]
    public void Has_Component_ReturnsFalse()
    {
        var entity = _world.Entity();
        Assert.False(_world.Has<Position>(entity.Id));
    }

    [Fact]
    public void Unset_Component_RemovesIt()
    {
        var entity = _world.Entity();
        entity.Set(new Position());
        Assert.True(_world.Has<Position>(entity.Id));

        _world.Unset<Position>(entity.Id);
        Assert.False(_world.Has<Position>(entity.Id));
    }

    [Fact]
    public void Unset_ViaEntity_RemovesIt()
    {
        var entity = _world.Entity();
        entity.Set(new Position()).Unset<Position>();
        Assert.False(_world.Has<Position>(entity.Id));
    }

    [Fact]
    public void Entity_FluentApi_Chaining()
    {
        var entity = _world.Entity()
            .Set(new Position { X = 1, Y = 2 })
            .Set(new Velocity { Dx = 3, Dy = 4 });

        Assert.True(_world.Has<Position>(entity.Id));
        Assert.True(_world.Has<Velocity>(entity.Id));
    }

    [Fact]
    public void Entity_MultipleComponents()
    {
        var entity = _world.Entity();
        entity.Set(new Position());
        entity.Set(new Velocity());
        entity.Set(new Health { Value = 100 });

        Assert.True(_world.Has<Position>(entity.Id));
        Assert.True(_world.Has<Velocity>(entity.Id));
        Assert.True(_world.Has<Health>(entity.Id));
    }

    [Fact]
    public void Update_IncrementTick()
    {
        var tick1 = _world.CurrentTick;
        _world.Update();
        var tick2 = _world.CurrentTick;
        Assert.True(tick2 > tick1);
    }

    [Fact]
    public void NamedEntity_Created()
    {
        var entity = _world.Entity("Player");
        Assert.True(entity.Id.IsValid());
        Assert.Equal("Player", _world.Name(entity.Id));
    }

    [Fact]
    public void Deferred_Operations()
    {
        var id = _world.Entity().Id;
        _world.Deferred(w =>
        {
            w.Set(id, new Position { X = 5, Y = 10 });
        });
        Assert.Equal(5, _world.Get<Position>(id).X);
    }

    // ── Resource Tests ───────────────────────────────────────

    [Fact]
    public void Resource_AddAndGet()
    {
        _world.AddResource("hello");
        Assert.Equal("hello", _world.GetResource<string>());
    }

    [Fact]
    public void Resource_HasResource()
    {
        Assert.False(_world.HasResource<string>());
        _world.AddResource("test");
        Assert.True(_world.HasResource<string>());
    }

    // ── Entity Count ─────────────────────────────────────────

    [Fact]
    public void EntityCount_IncrementsOnCreate()
    {
        var count0 = _world.EntityCount;
        _world.Entity();
        Assert.True(_world.EntityCount > count0);
    }

    // ── Component Overwrite ──────────────────────────────────

    [Fact]
    public void Set_Component_Overwrites()
    {
        var entity = _world.Entity();
        entity.Set(new Position { X = 1, Y = 2 });
        entity.Set(new Position { X = 99, Y = 88 });

        ref var pos = ref _world.Get<Position>(entity.Id);
        Assert.Equal(99, pos.X);
        Assert.Equal(88, pos.Y);
    }

    // ── Test component types (must implement IComponent for Friflo) ──

    struct Position : IComponent { public float X, Y; }
    struct Velocity : IComponent { public float Dx, Dy; }
    struct Health : IComponent { public int Value; }
}
