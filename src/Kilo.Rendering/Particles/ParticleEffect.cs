using System.Numerics;

namespace Kilo.Rendering.Particles;

/// <summary>
/// Particle effect asset (shareable across multiple emitters).
/// Defines visual properties and simulation parameters.
/// </summary>
public sealed class ParticleEffect
{
    // Emission
    public float SpawnRate = 50f;           // particles per second
    public int MaxParticles = 1000;         // buffer size
    public float Lifetime = 2f;             // seconds
    public float LifetimeVariance = 0.5f;

    // Initial velocity
    public Vector3 InitialVelocity = Vector3.UnitY * 2f;
    public float SpeedVariance = 0.5f;
    public float Spread = 0.3f;             // cone angle in radians

    // Appearance
    public Gradient<Color4>? ColorOverLifetime;
    public Gradient<float>? SizeOverLifetime;
    public float BaseSize = 0.1f;

    // Physics
    public Vector3 Gravity = new(0, -9.81f, 0f);
    public float Damping = 0.98f;
}
