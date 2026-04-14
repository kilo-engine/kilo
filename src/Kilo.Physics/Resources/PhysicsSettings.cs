using System.Numerics;

namespace Kilo.Physics;

/// <summary>
/// Configuration settings for the physics simulation.
/// </summary>
public sealed class PhysicsSettings
{
    /// <summary>Gravity vector (m/s²).</summary>
    public Vector3 Gravity { get; set; } = new(0f, -9.81f, 0f);

    /// <summary>Number of velocity solver iterations.</summary>
    public int VelocityIterations { get; set; } = 8;

    /// <summary>Number of substeps per frame for stability.</summary>
    public int SubstepCount { get; set; } = 2;

    /// <summary>Fixed timestep for physics simulation (seconds).</summary>
    public float FixedTimestep { get; set; } = 1f / 60f;

    /// <summary>Default physics settings.</summary>
    public static PhysicsSettings Default => new();
}
