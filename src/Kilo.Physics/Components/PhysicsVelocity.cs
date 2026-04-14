using System.Numerics;

namespace Kilo.Physics;

/// <summary>
/// Velocity components for a physics body.
/// Can be used to set initial velocity or read current velocity after simulation.
/// </summary>
public struct PhysicsVelocity
{
    /// <summary>Linear velocity (m/s).</summary>
    public Vector3 Linear;

    /// <summary>Angular velocity (rad/s).</summary>
    public Vector3 Angular;

    /// <summary>Zero velocity.</summary>
    public static PhysicsVelocity Zero => new()
    {
        Linear = Vector3.Zero,
        Angular = Vector3.Zero
    };
}
