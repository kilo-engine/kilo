namespace Kilo.ECS;

/// <summary>
/// Base interface for observer triggers. Inspired by Bevy's trigger/observer system.
/// Implement this on a struct to define a custom trigger type.
/// </summary>
public interface IKiloTrigger { }

/// <summary>
/// Trigger that carries an entity ID as context.
/// Used for component lifecycle observers (OnAdd, OnInsert, OnRemove).
/// </summary>
public interface IKiloEntityTrigger : IKiloTrigger
{
    /// <summary>The entity this trigger is associated with.</summary>
    ulong EntityId { get; }
}

/// <summary>
/// Trigger that can propagate up the entity hierarchy.
/// When emitted on a child entity, the observer fires on each ancestor
/// until <see cref="ShouldPropagate"/> returns false or the root is reached.
/// </summary>
public interface IKiloPropagatingTrigger : IKiloTrigger
{
    /// <summary>Whether this trigger should continue propagating to the parent.</summary>
    bool ShouldPropagate { get; }
}
