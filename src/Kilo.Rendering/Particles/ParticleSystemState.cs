using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Particles;

/// <summary>
/// Global particle system state (world resource).
/// Holds shared pipelines and per-emitter GPU resources.
/// </summary>
public sealed class ParticleSystemState
{
    public Dictionary<ulong, ParticleEmitterState> States = [];
    public IComputePipeline? UpdatePipeline;
    public IRenderPipeline? RenderPipeline;
    public IBuffer? QuadVB;
    public IBuffer? QuadIB;
    public ISampler? LinearSampler;
    public bool Initialized;

    public ParticleEmitterState GetOrCreateState(ulong entityId, IRenderDriver driver, ParticleEffect effect)
    {
        if (!States.TryGetValue(entityId, out var state))
        {
            state = new ParticleEmitterState { MaxParticles = effect.MaxParticles };
            States[entityId] = state;
        }
        return state;
    }

    public void Dispose()
    {
        foreach (var state in States.Values)
            state.Dispose();
        States.Clear();
        UpdatePipeline?.Dispose();
        RenderPipeline?.Dispose();
        QuadVB?.Dispose();
        QuadIB?.Dispose();
        LinearSampler?.Dispose();
        Initialized = false;
    }
}
