using Kilo.ECS;
using Friflo.Engine.ECS;
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

        var query = _world.Query<Position>();
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

        var query = _world.Query<Position, Velocity>();
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

        var query = _world.Query<Position, Mass>();
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

        var query = _world.Query<Position>();
        Assert.Equal(20, query.Count());
    }

    [Fact]
    public void Query_DataAccess_ModifiesComponents()
    {
        for (int i = 0; i < 10; i++)
            _world.Entity().Set(new Position { X = i, Y = 0 });

        var query = _world.Query<Position>();
        var iter = query.Iter();
        while (iter.Next())
        {
            var pos = iter.Data<Position>(0);
            for (int i = 0; i < iter.Count; i++)
            {
                pos[i].X *= 10;
            }
        }

        // Verify changes
        var verify = _world.Query<Position>();
        var vIter = verify.Iter();
        int total = 0;
        while (vIter.Next())
        {
            var pos = vIter.Data<Position>(0);
            for (int i = 0; i < vIter.Count; i++)
                total += (int)pos[i].X;
        }
        // 0*10 + 1*10 + ... + 9*10 = 10*(0+1+...+9) = 10*45 = 450
        Assert.Equal(450, total);
    }

    [Fact]
    public void Query_TwoComponent_DataAccess()
    {
        for (int i = 0; i < 5; i++)
        {
            _world.Entity()
                .Set(new Position { X = i, Y = i })
                .Set(new Velocity { Dx = i + 1, Dy = i + 1 });
        }

        var query = _world.Query<Position, Velocity>();
        var iter = query.Iter();
        while (iter.Next())
        {
            var pos = iter.Data<Position>(0);
            var vel = iter.Data<Velocity>(1);
            for (int i = 0; i < iter.Count; i++)
            {
                pos[i].X += vel[i].Dx;
            }
        }

        var verify = _world.Query<Position, Velocity>();
        var vIter = verify.Iter();
        while (vIter.Next())
        {
            var pos = vIter.Data<Position>(0);
            var vel = vIter.Data<Velocity>(1);
            for (int i = 0; i < vIter.Count; i++)
            {
                // X should be original X + Dx = i + (i+1) = 2i+1
                Assert.Equal(vel[i].Dx, pos[i].X - (vel[i].Dx - 1));
            }
        }
    }

    [Fact]
    public void Query_ThreeComponents()
    {
        for (int i = 0; i < 3; i++)
        {
            _world.Entity()
                .Set(new Position { X = i })
                .Set(new Velocity { Dx = i })
                .Set(new Mass { Value = i });
        }

        var query = _world.Query<Position, Velocity, Mass>();
        var iter = query.Iter();
        int count = 0;
        while (iter.Next())
        {
            var pos = iter.Data<Position>(0);
            var vel = iter.Data<Velocity>(1);
            var mass = iter.Data<Mass>(2);
            count += iter.Count;
        }
        Assert.Equal(3, count);
    }

    [Fact]
    public void Query_Empty_ReturnsZero()
    {
        var query = _world.Query<Position>();
        Assert.Equal(0, query.Count());
    }

    [Fact]
    public void Query_Iterator_NoMatch_NoIteration()
    {
        var query = _world.Query<Position>();
        var iter = query.Iter();
        Assert.False(iter.Next());
    }

    [Fact]
    public void Query_MultipleIterations()
    {
        for (int i = 0; i < 5; i++)
            _world.Entity().Set(new Position { X = i });

        var query = _world.Query<Position>();

        // First iteration
        int count1 = 0;
        var iter1 = query.Iter();
        while (iter1.Next()) count1 += iter1.Count;

        // Second iteration
        int count2 = 0;
        var iter2 = query.Iter();
        while (iter2.Next()) count2 += iter2.Count;

        Assert.Equal(5, count1);
        Assert.Equal(5, count2);
    }

    // ── Large Scale Tests ────────────────────────────────────

    [Fact]
    public void Query_LargeScale_100K()
    {
        for (int i = 0; i < 100_000; i++)
            _world.Entity().Set(new Position { X = i });

        var query = _world.Query<Position>();
        Assert.Equal(100_000, query.Count());

        int count = 0;
        var iter = query.Iter();
        while (iter.Next())
        {
            var pos = iter.Data<Position>(0);
            for (int i = 0; i < iter.Count; i++)
                pos[i].X += 1;
            count += iter.Count;
        }
        Assert.Equal(100_000, count);
    }

    struct Position : IComponent { public float X, Y; }
    struct Velocity : IComponent { public float Dx, Dy; }
    struct Mass : IComponent { public float Value; }
}
