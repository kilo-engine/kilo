using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Trigger interface for observer events. Mirrors TinyEcs.Bevy.ITrigger.
/// </summary>
public interface IKiloTrigger { }

/// <summary>
/// Trigger that carries an entity ID. Mirrors TinyEcs.Bevy.IEntityTrigger.
/// </summary>
public interface IKiloEntityTrigger : IKiloTrigger
{
    /// <summary>The entity this trigger is associated with.</summary>
    ulong EntityId { get; }
}

/// <summary>
/// Trigger that can propagate up the entity hierarchy.
/// </summary>
public interface IKiloPropagatingTrigger : IKiloTrigger
{
    /// <summary>Whether this trigger should propagate.</summary>
    bool ShouldPropagate { get; }
}
