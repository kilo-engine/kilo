using BepuPhysics;

namespace Kilo.Physics;

/// <summary>
/// Represents a physics body in the simulation.
/// Links an entity to a BepuPhysics body.
/// </summary>
public struct PhysicsBody
{
    /// <summary>Handle to the physics body in the simulation.</summary>
    public BodyHandle BodyHandle;

    /// <summary>Whether this body is dynamic (affected by forces).</summary>
    public bool IsDynamic;

    /// <summary>Whether this body is kinematic (moved by code, not forces).</summary>
    public bool IsKinematic;

    /// <summary>Check if this body is static (neither dynamic nor kinematic).</summary>
    public bool IsStatic => !IsDynamic && !IsKinematic;
}
