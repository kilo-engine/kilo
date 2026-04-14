using Kilo.ECS;
using Xunit;

namespace Kilo.ECS.Tests;

public sealed class QueryTests : IDisposable
{
    private readonly KiloWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void Query_SingleComponent_Iterates()
    {
        for (int i = 0; i < 10; i++)
            _world.Entity().Set(new Position { X = i, Y = 0 });

        var query = _world.QueryBuilder().With<Position>().Build();
        var iter = query.Iter();

        int count = 0;
        while (iter.Next())
        {
            count += iter.Count;
        }
        Assert.Equal(10, count);
    }

    [Fact]
    public void Query_MultiComponents_Iterates()
    {
        for (int i = 0; i < 5; i++)
        {
            _world.Entity()
                .Set(new Position { X = i, Y = 0 })
                .Set(new Velocity { Dx = i, Dy = 0 });
        }
        // Entity without velocity
        _world.Entity().Set(new Position { X = 99, Y = 0 });

        var query = _world.QueryBuilder()
            .With<Position>()
            .With<Velocity>()
            .Build();

        var iter = query.Iter();
        int count = 0;
        while (iter.Next())
        {
            count += iter.Count;
        }
        Assert.Equal(5, count);
    }

    [Fact]
    public void Query_With_Filter()
    {
        _world.Entity().Set(new Position()).Set(new Mass());
        _world.Entity().Set(new Position());

        var query = _world.QueryBuilder()
            .With<Position>()
            .With<Mass>()
            .Build();

        var iter = query.Iter();
        int count = 0;
        while (iter.Next())
        {
            count += iter.Count;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void Query_Without_Filter()
    {
        _world.Entity().Set(new Position()).Set(new Enemy());
        _world.Entity().Set(new Position());

        var query = _world.QueryBuilder()
            .With<Position>()
            .Without<Enemy>()
            .Build();

        var iter = query.Iter();
        int count = 0;
        while (iter.Next())
        {
            count += iter.Count;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void Query_Count_Matches()
    {
        for (int i = 0; i < 20; i++)
            _world.Entity().Set(new Position());

        var query = _world.QueryBuilder().With<Position>().Build();
        Assert.Equal(20, query.Count());
    }

    struct Position { public float X, Y; }
    struct Velocity { public float Dx, Dy; }
    struct Mass { public float Value; }
    struct Enemy;
}
