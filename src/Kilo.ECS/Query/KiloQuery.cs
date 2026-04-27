using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;

namespace Kilo.ECS;

// ── ForEach delegates ────────────────────────────────────────────

public delegate void RefAction<T1>(ref T1 v1);
public delegate void RefAction<T1, T2>(ref T1 v1, ref T2 v2);
public delegate void RefAction<T1, T2, T3>(ref T1 v1, ref T2 v2, ref T3 v3);
public delegate void RefAction<T1, T2, T3, T4>(ref T1 v1, ref T2 v2, ref T3 v3, ref T4 v4);
public delegate void RefAction<T1, T2, T3, T4, T5>(ref T1 v1, ref T2 v2, ref T3 v3, ref T4 v4, ref T5 v5);

// ── 1-component ──────────────────────────────────────────────────

public readonly struct KiloQuery<T1>
    where T1 : struct, IComponent
{
    private readonly ArchetypeQuery<T1> _query;
    private readonly KiloWorld? _world;
    private readonly bool _changedOnly;
    private readonly bool _addedOnly;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQuery(ArchetypeQuery<T1> query, KiloWorld? world = null, bool changedOnly = false, bool addedOnly = false)
    { _query = query; _world = world; _changedOnly = changedOnly; _addedOnly = addedOnly; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count() => _query.Chunks.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryIterator<T1> Iter() => new(_query);

    /// <summary>Require an additional component type (Bevy's <c>With&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1> With<TFilter>() where TFilter : struct, IComponent
        { _query.AllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Exclude entities that have the specified component (Bevy's <c>Without&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1> Without<TFilter>() where TFilter : struct, IComponent
        { _query.WithoutAllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Require at least one of the specified component types (Bevy's <c>Or&lt;(With&lt;A&gt;, With&lt;B&gt;)&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1> Or<TA, TB>() where TA : struct, IComponent where TB : struct, IComponent
        { _query.AnyComponents(ComponentTypes.Get<TA, TB>()); return this; }

    /// <summary>Only iterate entities whose first component changed this frame (Bevy's <c>Changed&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1> Changed() => new(_query, _world, true, _addedOnly);

    /// <summary>Only iterate entities whose first component was added this frame (Bevy's <c>Added&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1> Added() => new(_query, _world, _changedOnly, true);

    public void ForEach(RefAction<T1> action)
    {
        if (!_changedOnly && !_addedOnly)
        {
            var iter = Iter();
            while (iter.Next())
            {
                var span = iter.Span0;
                for (int i = 0; i < iter.Count; i++)
                    action(ref span[i]);
            }
            return;
        }
        // Filtered path
        var filtered = Iter();
        while (filtered.Next())
        {
            var span = filtered.Span0;
            var ids = filtered.Entities();
            for (int i = 0; i < filtered.Count; i++)
            {
                var eid = ids[i];
                if (_changedOnly && (_world == null || !_world.HasChanged<T1>(eid))) continue;
                if (_addedOnly && (_world == null || !_world.HasAdded<T1>(eid))) continue;
                action(ref span[i]);
            }
        }
    }
}

[SkipLocalsInit]
public ref struct KiloQueryIterator<T1>
    where T1 : struct, IComponent
{
    private ChunkEnumerator<T1> _enumerator;
    private (Chunk<T1> C1, ChunkEntities Entities) _current;
    private bool _hasChunks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQueryIterator(ArchetypeQuery<T1> query)
    {
        _enumerator = query.Chunks.GetEnumerator();
        _hasChunks = true;
        _current = default;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current.C1.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Next()
    {
        if (!_hasChunks) return false;
        if (!_enumerator.MoveNext()) { _hasChunks = false; return false; }
        var (c1, entities) = _enumerator.Current;
        _current = (c1, entities);
        return true;
    }

    public Span<T1> Span0 => _current.C1.Span;

    unsafe public Span<T> Data<T>(int componentIndex) where T : struct
    {
        var span = Span0;
        return span.Length > 0
            ? new Span<T>(Unsafe.AsPointer(ref span[0]), _current.C1.Length)
            : [];
    }

    public ReadOnlySpan<EntityId> Entities()
    {
        var ids = _current.Entities.Ids;
        var result = new EntityId[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            result[i] = new EntityId((ulong)ids[i]);
        return result;
    }
}

// ── 2-component ──────────────────────────────────────────────────

public readonly struct KiloQuery<T1, T2>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    private readonly ArchetypeQuery<T1, T2> _query;
    private readonly KiloWorld? _world;
    private readonly bool _changedOnly;
    private readonly bool _addedOnly;

    internal KiloQuery(ArchetypeQuery<T1, T2> query, KiloWorld? world = null, bool changedOnly = false, bool addedOnly = false)
    { _query = query; _world = world; _changedOnly = changedOnly; _addedOnly = addedOnly; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count() => _query.Chunks.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryIterator<T1, T2> Iter() => new(_query);

    /// <summary>Require an additional component type (Bevy's <c>With&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2> With<TFilter>() where TFilter : struct, IComponent
        { _query.AllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Exclude entities that have the specified component (Bevy's <c>Without&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2> Without<TFilter>() where TFilter : struct, IComponent
        { _query.WithoutAllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Require at least one of the specified component types (Bevy's <c>Or</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2> Or<TA, TB>() where TA : struct, IComponent where TB : struct, IComponent
        { _query.AnyComponents(ComponentTypes.Get<TA, TB>()); return this; }

    /// <summary>Only iterate entities whose first component changed this frame (Bevy's <c>Changed&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2> Changed() => new(_query, _world, true, _addedOnly);

    /// <summary>Only iterate entities whose first component was added this frame (Bevy's <c>Added&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2> Added() => new(_query, _world, _changedOnly, true);

    public void ForEach(RefAction<T1, T2> action)
    {
        if (!_changedOnly && !_addedOnly)
        {
            var iter = Iter();
            while (iter.Next())
            {
                var s1 = iter.Span0;
                var s2 = iter.Span1;
                for (int i = 0; i < iter.Count; i++)
                    action(ref s1[i], ref s2[i]);
            }
            return;
        }
        var filtered = Iter();
        while (filtered.Next())
        {
            var s1 = filtered.Span0;
            var s2 = filtered.Span1;
            var ids = filtered.Entities();
            for (int i = 0; i < filtered.Count; i++)
            {
                var eid = ids[i];
                if (_changedOnly && (_world == null || !_world.HasChanged<T1>(eid))) continue;
                if (_addedOnly && (_world == null || !_world.HasAdded<T1>(eid))) continue;
                action(ref s1[i], ref s2[i]);
            }
        }
    }
}

[SkipLocalsInit]
public ref struct KiloQueryIterator<T1, T2>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    private ChunkEnumerator<T1, T2> _enumerator;
    private (Chunk<T1> C1, Chunk<T2> C2, ChunkEntities Entities) _current;
    private bool _hasChunks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQueryIterator(ArchetypeQuery<T1, T2> query)
    {
        _enumerator = query.Chunks.GetEnumerator();
        _hasChunks = true;
        _current = default;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current.C1.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Next()
    {
        if (!_hasChunks) return false;
        if (!_enumerator.MoveNext()) { _hasChunks = false; return false; }
        var (c1, c2, entities) = _enumerator.Current;
        _current = (c1, c2, entities);
        return true;
    }

    public Span<T1> Span0 => _current.C1.Span;
    public Span<T2> Span1 => _current.C2.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public Span<T> Data<T>(int componentIndex) where T : struct
    {
        ref var c = ref _current;
        if (componentIndex == 0)
        {
            var span = c.C1.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C1.Length)
                : [];
        }
        {
            var span = c.C2.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C2.Length)
                : [];
        }
    }

    public ReadOnlySpan<EntityId> Entities()
    {
        var ids = _current.Entities.Ids;
        var result = new EntityId[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            result[i] = new EntityId((ulong)ids[i]);
        return result;
    }
}

// ── 3-component ──────────────────────────────────────────────────

public readonly struct KiloQuery<T1, T2, T3>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent
{
    private readonly ArchetypeQuery<T1, T2, T3> _query;
    private readonly KiloWorld? _world;
    private readonly bool _changedOnly;
    private readonly bool _addedOnly;

    internal KiloQuery(ArchetypeQuery<T1, T2, T3> query, KiloWorld? world = null, bool changedOnly = false, bool addedOnly = false)
    { _query = query; _world = world; _changedOnly = changedOnly; _addedOnly = addedOnly; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count() => _query.Chunks.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryIterator<T1, T2, T3> Iter() => new(_query);

    /// <summary>Require an additional component type (Bevy's <c>With&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3> With<TFilter>() where TFilter : struct, IComponent
        { _query.AllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Exclude entities that have the specified component (Bevy's <c>Without&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3> Without<TFilter>() where TFilter : struct, IComponent
        { _query.WithoutAllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Require at least one of the specified component types (Bevy's <c>Or</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3> Or<TA, TB>() where TA : struct, IComponent where TB : struct, IComponent
        { _query.AnyComponents(ComponentTypes.Get<TA, TB>()); return this; }

    /// <summary>Only iterate entities whose first component changed this frame (Bevy's <c>Changed&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3> Changed() => new(_query, _world, true, _addedOnly);

    /// <summary>Only iterate entities whose first component was added this frame (Bevy's <c>Added&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3> Added() => new(_query, _world, _changedOnly, true);

    public void ForEach(RefAction<T1, T2, T3> action)
    {
        if (!_changedOnly && !_addedOnly)
        {
            var iter = Iter();
            while (iter.Next())
            {
                var s1 = iter.Span0;
                var s2 = iter.Span1;
                var s3 = iter.Span2;
                for (int i = 0; i < iter.Count; i++)
                    action(ref s1[i], ref s2[i], ref s3[i]);
            }
            return;
        }
        var filtered = Iter();
        while (filtered.Next())
        {
            var s1 = filtered.Span0;
            var s2 = filtered.Span1;
            var s3 = filtered.Span2;
            var ids = filtered.Entities();
            for (int i = 0; i < filtered.Count; i++)
            {
                var eid = ids[i];
                if (_changedOnly && (_world == null || !_world.HasChanged<T1>(eid))) continue;
                if (_addedOnly && (_world == null || !_world.HasAdded<T1>(eid))) continue;
                action(ref s1[i], ref s2[i], ref s3[i]);
            }
        }
    }
}

[SkipLocalsInit]
public ref struct KiloQueryIterator<T1, T2, T3>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent
{
    private ChunkEnumerator<T1, T2, T3> _enumerator;
    private (Chunk<T1> C1, Chunk<T2> C2, Chunk<T3> C3, ChunkEntities Entities) _current;
    private bool _hasChunks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQueryIterator(ArchetypeQuery<T1, T2, T3> query)
    {
        _enumerator = query.Chunks.GetEnumerator();
        _hasChunks = true;
        _current = default;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current.C1.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Next()
    {
        if (!_hasChunks) return false;
        if (!_enumerator.MoveNext()) { _hasChunks = false; return false; }
        var (c1, c2, c3, entities) = _enumerator.Current;
        _current = (c1, c2, c3, entities);
        return true;
    }

    public Span<T1> Span0 => _current.C1.Span;
    public Span<T2> Span1 => _current.C2.Span;
    public Span<T3> Span2 => _current.C3.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public Span<T> Data<T>(int componentIndex) where T : struct
    {
        ref var c = ref _current;
        if (componentIndex == 0)
        {
            var span = c.C1.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C1.Length) : [];
        }
        if (componentIndex == 1)
        {
            var span = c.C2.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C2.Length) : [];
        }
        {
            var span = c.C3.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C3.Length) : [];
        }
    }

    public ReadOnlySpan<EntityId> Entities()
    {
        var ids = _current.Entities.Ids;
        var result = new EntityId[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            result[i] = new EntityId((ulong)ids[i]);
        return result;
    }
}

// ── 4-component ──────────────────────────────────────────────────

public readonly struct KiloQuery<T1, T2, T3, T4>
    where T1 : struct, IComponent where T2 : struct, IComponent
    where T3 : struct, IComponent where T4 : struct, IComponent
{
    private readonly ArchetypeQuery<T1, T2, T3, T4> _query;
    private readonly KiloWorld? _world;
    private readonly bool _changedOnly;
    private readonly bool _addedOnly;

    internal KiloQuery(ArchetypeQuery<T1, T2, T3, T4> query, KiloWorld? world = null, bool changedOnly = false, bool addedOnly = false)
    { _query = query; _world = world; _changedOnly = changedOnly; _addedOnly = addedOnly; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count() => _query.Chunks.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryIterator<T1, T2, T3, T4> Iter() => new(_query);

    /// <summary>Require an additional component type (Bevy's <c>With&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4> With<TFilter>() where TFilter : struct, IComponent
        { _query.AllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Exclude entities that have the specified component (Bevy's <c>Without&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4> Without<TFilter>() where TFilter : struct, IComponent
        { _query.WithoutAllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Require at least one of the specified component types (Bevy's <c>Or</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4> Or<TA, TB>() where TA : struct, IComponent where TB : struct, IComponent
        { _query.AnyComponents(ComponentTypes.Get<TA, TB>()); return this; }

    /// <summary>Only iterate entities whose first component changed this frame (Bevy's <c>Changed&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4> Changed() => new(_query, _world, true, _addedOnly);

    /// <summary>Only iterate entities whose first component was added this frame (Bevy's <c>Added&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4> Added() => new(_query, _world, _changedOnly, true);

    public void ForEach(RefAction<T1, T2, T3, T4> action)
    {
        if (!_changedOnly && !_addedOnly)
        {
            var iter = Iter();
            while (iter.Next())
            {
                var s1 = iter.Span0;
                var s2 = iter.Span1;
                var s3 = iter.Span2;
                var s4 = iter.Span3;
                for (int i = 0; i < iter.Count; i++)
                    action(ref s1[i], ref s2[i], ref s3[i], ref s4[i]);
            }
            return;
        }
        var filtered = Iter();
        while (filtered.Next())
        {
            var s1 = filtered.Span0;
            var s2 = filtered.Span1;
            var s3 = filtered.Span2;
            var s4 = filtered.Span3;
            var ids = filtered.Entities();
            for (int i = 0; i < filtered.Count; i++)
            {
                var eid = ids[i];
                if (_changedOnly && (_world == null || !_world.HasChanged<T1>(eid))) continue;
                if (_addedOnly && (_world == null || !_world.HasAdded<T1>(eid))) continue;
                action(ref s1[i], ref s2[i], ref s3[i], ref s4[i]);
            }
        }
    }
}

[SkipLocalsInit]
public ref struct KiloQueryIterator<T1, T2, T3, T4>
    where T1 : struct, IComponent where T2 : struct, IComponent
    where T3 : struct, IComponent where T4 : struct, IComponent
{
    private ChunkEnumerator<T1, T2, T3, T4> _enumerator;
    private (Chunk<T1> C1, Chunk<T2> C2, Chunk<T3> C3, Chunk<T4> C4, ChunkEntities Entities) _current;
    private bool _hasChunks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQueryIterator(ArchetypeQuery<T1, T2, T3, T4> query)
    {
        _enumerator = query.Chunks.GetEnumerator();
        _hasChunks = true;
        _current = default;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current.C1.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Next()
    {
        if (!_hasChunks) return false;
        if (!_enumerator.MoveNext()) { _hasChunks = false; return false; }
        var (c1, c2, c3, c4, entities) = _enumerator.Current;
        _current = (c1, c2, c3, c4, entities);
        return true;
    }

    public Span<T1> Span0 => _current.C1.Span;
    public Span<T2> Span1 => _current.C2.Span;
    public Span<T3> Span2 => _current.C3.Span;
    public Span<T4> Span3 => _current.C4.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public Span<T> Data<T>(int componentIndex) where T : struct
    {
        ref var c = ref _current;
        if (componentIndex == 0)
        {
            var span = c.C1.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C1.Length) : [];
        }
        if (componentIndex == 1)
        {
            var span = c.C2.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C2.Length) : [];
        }
        if (componentIndex == 2)
        {
            var span = c.C3.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C3.Length) : [];
        }
        {
            var span = c.C4.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C4.Length) : [];
        }
    }

    public ReadOnlySpan<EntityId> Entities()
    {
        var ids = _current.Entities.Ids;
        var result = new EntityId[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            result[i] = new EntityId((ulong)ids[i]);
        return result;
    }
}

// ── 5-component ──────────────────────────────────────────────────

public readonly struct KiloQuery<T1, T2, T3, T4, T5>
    where T1 : struct, IComponent where T2 : struct, IComponent
    where T3 : struct, IComponent where T4 : struct, IComponent where T5 : struct, IComponent
{
    private readonly ArchetypeQuery<T1, T2, T3, T4, T5> _query;
    private readonly KiloWorld? _world;
    private readonly bool _changedOnly;
    private readonly bool _addedOnly;

    internal KiloQuery(ArchetypeQuery<T1, T2, T3, T4, T5> query, KiloWorld? world = null, bool changedOnly = false, bool addedOnly = false)
    { _query = query; _world = world; _changedOnly = changedOnly; _addedOnly = addedOnly; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count() => _query.Chunks.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryIterator<T1, T2, T3, T4, T5> Iter() => new(_query);

    /// <summary>Require an additional component type (Bevy's <c>With&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4, T5> With<TFilter>() where TFilter : struct, IComponent
        { _query.AllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Exclude entities that have the specified component (Bevy's <c>Without&lt;T&gt;</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4, T5> Without<TFilter>() where TFilter : struct, IComponent
        { _query.WithoutAllComponents(ComponentTypes.Get<TFilter>()); return this; }

    /// <summary>Require at least one of the specified component types (Bevy's <c>Or</c> filter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4, T5> Or<TA, TB>() where TA : struct, IComponent where TB : struct, IComponent
        { _query.AnyComponents(ComponentTypes.Get<TA, TB>()); return this; }

    /// <summary>Only iterate entities whose first component changed this frame (Bevy's <c>Changed&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4, T5> Changed() => new(_query, _world, true, _addedOnly);

    /// <summary>Only iterate entities whose first component was added this frame (Bevy's <c>Added&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4, T5> Added() => new(_query, _world, _changedOnly, true);

    public void ForEach(RefAction<T1, T2, T3, T4, T5> action)
    {
        if (!_changedOnly && !_addedOnly)
        {
            var iter = Iter();
            while (iter.Next())
            {
                var s1 = iter.Span0;
                var s2 = iter.Span1;
                var s3 = iter.Span2;
                var s4 = iter.Span3;
                var s5 = iter.Span4;
                for (int i = 0; i < iter.Count; i++)
                    action(ref s1[i], ref s2[i], ref s3[i], ref s4[i], ref s5[i]);
            }
            return;
        }
        var filtered = Iter();
        while (filtered.Next())
        {
            var s1 = filtered.Span0;
            var s2 = filtered.Span1;
            var s3 = filtered.Span2;
            var s4 = filtered.Span3;
            var s5 = filtered.Span4;
            var ids = filtered.Entities();
            for (int i = 0; i < filtered.Count; i++)
            {
                var eid = ids[i];
                if (_changedOnly && (_world == null || !_world.HasChanged<T1>(eid))) continue;
                if (_addedOnly && (_world == null || !_world.HasAdded<T1>(eid))) continue;
                action(ref s1[i], ref s2[i], ref s3[i], ref s4[i], ref s5[i]);
            }
        }
    }
}

[SkipLocalsInit]
public ref struct KiloQueryIterator<T1, T2, T3, T4, T5>
    where T1 : struct, IComponent where T2 : struct, IComponent
    where T3 : struct, IComponent where T4 : struct, IComponent where T5 : struct, IComponent
{
    private ChunkEnumerator<T1, T2, T3, T4, T5> _enumerator;
    private (Chunk<T1> C1, Chunk<T2> C2, Chunk<T3> C3, Chunk<T4> C4, Chunk<T5> C5, ChunkEntities Entities) _current;
    private bool _hasChunks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQueryIterator(ArchetypeQuery<T1, T2, T3, T4, T5> query)
    {
        _enumerator = query.Chunks.GetEnumerator();
        _hasChunks = true;
        _current = default;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current.C1.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Next()
    {
        if (!_hasChunks) return false;
        if (!_enumerator.MoveNext()) { _hasChunks = false; return false; }
        var (c1, c2, c3, c4, c5, entities) = _enumerator.Current;
        _current = (c1, c2, c3, c4, c5, entities);
        return true;
    }

    public Span<T1> Span0 => _current.C1.Span;
    public Span<T2> Span1 => _current.C2.Span;
    public Span<T3> Span2 => _current.C3.Span;
    public Span<T4> Span3 => _current.C4.Span;
    public Span<T5> Span4 => _current.C5.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public Span<T> Data<T>(int componentIndex) where T : struct
    {
        ref var c = ref _current;
        if (componentIndex == 0)
        {
            var span = c.C1.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C1.Length) : [];
        }
        if (componentIndex == 1)
        {
            var span = c.C2.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C2.Length) : [];
        }
        if (componentIndex == 2)
        {
            var span = c.C3.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C3.Length) : [];
        }
        if (componentIndex == 3)
        {
            var span = c.C4.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C4.Length) : [];
        }
        {
            var span = c.C5.Span;
            return span.Length > 0
                ? new Span<T>(Unsafe.AsPointer(ref span[0]), c.C5.Length) : [];
        }
    }

    public ReadOnlySpan<EntityId> Entities()
    {
        var ids = _current.Entities.Ids;
        var result = new EntityId[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            result[i] = new EntityId((ulong)ids[i]);
        return result;
    }
}
