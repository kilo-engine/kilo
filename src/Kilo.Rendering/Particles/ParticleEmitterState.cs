using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Particles;

/// <summary>
/// Per-emitter GPU resources. Created lazily, keyed by entity ID.
/// </summary>
public sealed class ParticleEmitterState : IDisposable
{
    public IBuffer? ParticleBuffer;       // GpuParticle[] storage buffer
    public IBuffer? ParamsBuffer;         // EmitterParams uniform
    public IBuffer? SpawnParamsBuffer;    // SpawnParams uniform (per-frame)
    public ITexture? ColorLut;            // 256x1 color gradient LUT (RGBA8)
    public ITextureView? ColorLutView;
    public ITexture? SizeLut;             // 256x1 size curve LUT (R32Float)
    public ITextureView? SizeLutView;
    public IBindingSet? UpdateBindingSet;
    public int MaxParticles;

    public void Dispose()
    {
        ParticleBuffer?.Dispose();
        ParamsBuffer?.Dispose();
        SpawnParamsBuffer?.Dispose();
        ColorLut?.Dispose();
        ColorLutView?.Dispose();
        SizeLut?.Dispose();
        SizeLutView?.Dispose();
        UpdateBindingSet?.Dispose();
    }
}
