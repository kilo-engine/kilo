using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Particles;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering;

/// <summary>
/// Renders particles as camera-facing billboards with alpha blending.
/// Called by CameraRenderLoopSystem for each scene camera.
/// </summary>
public sealed class ParticleRenderSystem
{
    /// <summary>Backward-compatible entry point with default screen context.</summary>
    public void Update(KiloWorld world)
    {
        var ws = world.GetResource<WindowSize>();
        var ctx = new CameraRenderContext(new ActiveCameraEntry
        {
            Target = CameraTarget.Screen,
            CameraType = CameraType.Scene,
            RenderWidth = ws.Width,
            RenderHeight = ws.Height,
        });
        AddParticlePass(ctx, world);
    }

    public void AddParticlePass(CameraRenderContext ctx, KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var ps = context.Particles;
        var scene = world.GetResource<GpuSceneData>();

        if (!ps.Initialized || ps.RenderPipeline == null) return;

        // Collect active particle emitter states
        var query = world.QueryBuilder()
            .With<ParticleEmitter>()
            .Build();

        var activeEmitters = new List<(ulong EntityId, ParticleEffect Effect)>();
        var iter = query.Iter();
        while (iter.Next())
        {
            var emitters = iter.Data<ParticleEmitter>(iter.GetColumnIndexOf<ParticleEmitter>());
            var entities = iter.Entities();
            for (int i = 0; i < iter.Count; i++)
            {
                if (emitters[i].Active && emitters[i].Effect != null)
                    activeEmitters.Add((entities[i].ID, emitters[i].Effect!));
            }
        }

        if (activeEmitters.Count == 0) return;

        var graph = context.RenderGraph;
        string passName = $"{ctx.Prefix}ParticleRender";

        // Use per-camera data from the render context
        var cameraData = ctx.Camera.CameraData;

        // Get per-camera buffer to avoid shared-buffer overwrite
        var pp = context.PostProcess;
        var camTex = pp.GetCameraTextures(ctx.Prefix);
        camTex.EnsureCameraBuffer(driver);

        graph.AddPass(passName, setup: pass =>
        {
            var sceneColor = pass.ImportTexture(ctx.SceneColorName, new TextureDescriptor
            {
                Width = ctx.Width,
                Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(sceneColor);
            pass.WriteTexture(sceneColor);
            pass.ColorAttachment(sceneColor, DriverLoadAction.Load, DriverStoreAction.Store);
        }, execute: exeCtx =>
        {
            // Upload camera data to per-camera buffer (not the shared scene.CameraBuffer)
            var cameraBuffer = camTex.CameraBuffer!;
            var camData = new CameraData[1];
            camData[0] = cameraData;
            camData[0].LightCount = scene.LightCount;
            cameraBuffer.UploadData<CameraData>(camData);

            foreach (var (entityId, effect) in activeEmitters)
            {
                if (!ps.States.TryGetValue(entityId, out var state)) continue;
                if (state.ParticleBuffer == null) continue;

                var bindingSet = driver.CreateBindingSetForPipeline(
                    ps.RenderPipeline!, 0,
                    uniformBuffers: [new UniformBufferBinding { Binding = 0, Buffer = cameraBuffer }],
                    storageBuffers: [new StorageBufferBinding { Binding = 1, Buffer = state.ParticleBuffer }]);

                exeCtx.Encoder.SetPipeline(ps.RenderPipeline!);
                exeCtx.Encoder.SetBindingSet(0, bindingSet);
                // Draw 4 vertices (quad) per instance, MaxParticles instances
                // Vertex shader skips dead particles via early-out
                exeCtx.Encoder.Draw(4, effect.MaxParticles);
            }
        });
    }

    /// <summary>
    /// Initializes the render pipeline if not already done.
    /// Called by CameraRenderLoopSystem before first render.
    /// </summary>
    public static void EnsureRenderPipeline(RenderContext context)
    {
        var driver = context.Driver;
        var ps = context.Particles;
        if (ps.RenderPipeline != null) return;

        var vs = context.ShaderCache.GetOrCreateShader(driver, ParticleShaders.RenderWGSL, "vs_main");
        var fs = context.ShaderCache.GetOrCreateShader(driver, ParticleShaders.RenderWGSL, "fs_main");

        ps.RenderPipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = vs,
            FragmentShader = fs,
            Topology = DriverPrimitiveTopology.TriangleStrip,
            ColorTargets =
            [
                new ColorTargetDescriptor
                {
                    Format = DriverPixelFormat.RGBA16Float,
                    Blend = new BlendStateDescriptor
                    {
                        Color = new BlendComponentDescriptor
                        {
                            SrcFactor = DriverBlendFactor.SrcAlpha,
                            DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                        },
                        Alpha = new BlendComponentDescriptor
                        {
                            SrcFactor = DriverBlendFactor.One,
                            DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                        }
                    }
                }
            ],
            DepthStencil = null,
            VertexBuffers = [],
        });
    }
}
