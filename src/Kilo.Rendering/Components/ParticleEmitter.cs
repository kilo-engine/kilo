using Kilo.Rendering.Particles;

namespace Kilo.Rendering;

/// <summary>
/// ECS component for a particle emitter instance.
/// References a shared ParticleEffect asset.
/// </summary>
public struct ParticleEmitter
{
    public ParticleEffect? Effect;
    public float SpawnTimer;
    public int AliveCount;
    public bool Active = true;

    public ParticleEmitter() { }
}
