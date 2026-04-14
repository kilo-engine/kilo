namespace Kilo.Physics;

/// <summary>
/// Collider metadata for a physics body.
/// Used for identifying colliders and configuring trigger behavior.
/// </summary>
public struct PhysicsCollider
{
    /// <summary>Unique identifier for the collider.</summary>
    public int ColliderId;

    /// <summary>Whether this collider is a trigger (detects overlap but doesn't block).</summary>
    public bool IsTrigger;

    /// <summary>Default collider configuration.</summary>
    public static PhysicsCollider Default => new()
    {
        ColliderId = -1,
        IsTrigger = false
    };
}
