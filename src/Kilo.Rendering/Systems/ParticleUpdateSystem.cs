using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Particles;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering;

/// <summary>
/// Compute-based particle simulation. Runs in PostUpdate stage.
/// Updates spawn timers, uploads parameters, and adds a compute pass to the RenderGraph.
/// </summary>
public sealed class ParticleUpdateSystem
{
    private static DateTime _lastTime = DateTime.Now;

    [StructLayout(LayoutKind.Sequential)]
    private struct EmitterParams
    {
        public Vector3 Gravity;
        public float _pad0;
        public float Damping;
        public float BaseSize;
        public float Lifetime;
        public float LifetimeVariance;
        public uint MaxParticles;
        public uint _pad1, _pad2, _pad3, _pad4, _pad5, _pad6;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpawnParams
    {
        public Vector4 EmitterPosition;
        public Vector4 InitialVelocity;
        public float SpeedVariance;
        public float Spread;
        public float Dt;
        public float SpawnCount;
        public float Time;
        private float _pad0, _pad1, _pad2, _pad3;
    }

    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var ps = context.Particles;

        if (driver == null) return;

        // Lazy init shared resources
        if (!ps.Initialized)
        {
            InitSharedResources(driver, ps);
            ps.Initialized = true;
        }

        var now = DateTime.Now;
        float dt = (float)(now - _lastTime).TotalSeconds;
        _lastTime = now;
        dt = Math.Clamp(dt, 0f, 0.1f);

        var query = world.QueryBuilder()
            .With<ParticleEmitter>()
            .With<LocalToWorld>()
            .Build();

        var graph = context.RenderGraph;

        var iter = query.Iter();
        while (iter.Next())
        {
            var emitters = iter.Data<ParticleEmitter>(iter.GetColumnIndexOf<ParticleEmitter>());
            var transforms = iter.Data<LocalToWorld>(iter.GetColumnIndexOf<LocalToWorld>());
            var entities = iter.Entities();

            for (int i = 0; i < iter.Count; i++)
            {
                ref var emitter = ref emitters[i];
                var effect = emitter.Effect;
                if (effect == null || !emitter.Active) continue;

                var entityId = entities[i].ID;
                var state = ps.GetOrCreateState(entityId, driver, effect);

                // Ensure per-emitter GPU resources
                EnsureEmitterResources(driver, state, effect, context);

                // Update spawn timer
                emitter.SpawnTimer += dt;
                float spawnCount = 0f;
                float spawnInterval = effect.SpawnRate > 0 ? 1f / effect.SpawnRate : float.MaxValue;
                while (emitter.SpawnTimer >= spawnInterval)
                {
                    emitter.SpawnTimer -= spawnInterval;
                    spawnCount += 1f;
                }

                // Upload emitter params
                var emitterParamsData = new EmitterParams[1];
                emitterParamsData[0].Gravity = effect.Gravity;
                emitterParamsData[0].Damping = effect.Damping;
                emitterParamsData[0].BaseSize = effect.BaseSize;
                emitterParamsData[0].Lifetime = effect.Lifetime;
                emitterParamsData[0].LifetimeVariance = effect.LifetimeVariance;
                emitterParamsData[0].MaxParticles = (uint)effect.MaxParticles;
                state.ParamsBuffer!.UploadData<EmitterParams>(emitterParamsData);

                // Upload spawn params
                var origin = transforms[i].Value.Translation;
                var spawnParamsData = new SpawnParams[1];
                spawnParamsData[0].EmitterPosition = new Vector4(origin, 1f);
                spawnParamsData[0].InitialVelocity = new Vector4(effect.InitialVelocity, 0f);
                spawnParamsData[0].SpeedVariance = effect.SpeedVariance;
                spawnParamsData[0].Spread = effect.Spread;
                spawnParamsData[0].Dt = dt;
                spawnParamsData[0].SpawnCount = spawnCount;
                spawnParamsData[0].Time = (float)now.TimeOfDay.TotalSeconds;
                state.SpawnParamsBuffer!.UploadData<SpawnParams>(spawnParamsData);

                var stateCopy = state; // capture for lambda
                uint wgX = (uint)((effect.MaxParticles + 63) / 64);

                // Add compute pass to the shared RenderGraph
                graph.AddComputePass($"ParticleUpdate_{entityId}", setup: _ => { }, execute: ctx =>
                {
                    var bindingSet = driver.CreateBindingSetForComputePipeline(
                        ps.UpdatePipeline!, 0,
                        storageBuffers: [new StorageBufferBinding { Binding = 0, Buffer = stateCopy.ParticleBuffer! }],
                        uniformBuffers:
                        [
                            new UniformBufferBinding { Binding = 1, Buffer = stateCopy.ParamsBuffer! },
                            new UniformBufferBinding { Binding = 2, Buffer = stateCopy.SpawnParamsBuffer! },
                        ],
                        textures: [],
                        samplers: []);

                    ctx.Encoder.SetComputePipeline(ps.UpdatePipeline!);
                    ctx.Encoder.SetComputeBindingSet(0, bindingSet);
                    ctx.Encoder.Dispatch(wgX, 1, 1);
                });
            }
        }
    }

    private static void InitSharedResources(IRenderDriver driver, ParticleSystemState ps)
    {
        var updateShader = driver.CreateComputeShaderModule(ParticleShaders.UpdateWGSL, "main");
        ps.UpdatePipeline = driver.CreateComputePipeline(updateShader, "main");

        ps.LinearSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
        });
    }

    private static void EnsureEmitterResources(IRenderDriver driver, ParticleEmitterState state, ParticleEffect effect, RenderContext context)
    {
        // Particle buffer — temporarily disabled for debugging
        if (state.ParticleBuffer == null || state.MaxParticles != effect.MaxParticles)
        {
            state.ParticleBuffer?.Dispose();
            state.MaxParticles = effect.MaxParticles;
            int bufferSize = effect.MaxParticles * Marshal.SizeOf<GpuParticle>();
            state.ParticleBuffer = driver.CreateBuffer(new BufferDescriptor
            {
                Size = (nuint)bufferSize,
                Usage = BufferUsage.Storage | BufferUsage.CopyDst,
            });
        }

        // Uniform buffers (256-byte aligned)
        if (state.ParamsBuffer == null)
        {
            state.ParamsBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 256, Usage = BufferUsage.Uniform | BufferUsage.CopyDst });
        }
        if (state.SpawnParamsBuffer == null)
        {
            state.SpawnParamsBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 256, Usage = BufferUsage.Uniform | BufferUsage.CopyDst });
        }

        // LUT textures (skip for now — will be added back when LUT sampling is re-enabled)
        // if (state.ColorLut == null) ...
        // if (state.SizeLut == null) ...
    }
}
