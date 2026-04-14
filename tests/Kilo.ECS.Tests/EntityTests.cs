using Kilo.ECS;
using Xunit;

namespace Kilo.ECS.Tests;

public sealed class EntityTests : IDisposable
{
    private readonly KiloWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void Entity_Equals_SameId()
    {
        var e = _world.Entity();
        var e2 = _world.Entity(e.Id.Value);
        Assert.True(e == e2);
    }

    [Fact]
    public void Entity_NotEquals_DifferentId()
    {
        var e1 = _world.Entity();
        var e2 = _world.Entity();
        Assert.True(e1 != e2);
    }

    [Fact]
    public void Entity_Implicit_To_EntityId()
    {
        var e = _world.Entity();
        EntityId id = e;
        Assert.Equal(e.Id.Value, id.Value);
    }

    [Fact]
    public void EntityId_IsValid()
    {
        Assert.True(new EntityId(1).IsValid());
        Assert.False(new EntityId(0).IsValid());
    }

    [Fact]
    public void EntityId_Equality()
    {
        var a = new EntityId(42);
        var b = new EntityId(42);
        var c = new EntityId(99);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.False(a == c);
    }

    [Fact]
    public void EntityId_Implicit_Ulong()
    {
        EntityId id = 42ul;
        ulong val = id;
        Assert.Equal(42ul, val);
    }
}
