using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Group related components for cleaner entity spawning.
/// Mirrors TinyEcs.Bevy.IBundle.
/// </summary>
public interface IKiloBundle
{
    /// <summary>Insert all bundle components onto the entity (immediate mode).</summary>
    void Insert(KiloEntity entity);

    /// <summary>Insert all bundle components onto the entity (deferred mode via Commands).</summary>
    void Insert(TinyEcs.Bevy.EntityCommands entity);
}

/// <summary>
/// Extension methods for bundles.
/// </summary>
public static class KiloBundleExtensions
{
    /// <summary>Insert a bundle onto an existing entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KiloEntity InsertBundle<TBundle>(this KiloEntity entity, TBundle bundle)
        where TBundle : struct, IKiloBundle
    {
        bundle.Insert(entity);
        return entity;
    }
}
