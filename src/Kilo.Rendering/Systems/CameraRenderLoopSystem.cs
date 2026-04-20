using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Core system that orchestrates rendering for all active cameras.
/// Iterates cameras sorted by priority, calling per-system rendering methods
/// with camera-specific context.
/// </summary>
public sealed class CameraRenderLoopSystem
{
    private readonly RenderSystem _render = new();
    private readonly SpriteRenderSystem _sprite = new();
    private readonly TextRenderSystem _text = new();
    private readonly PostProcessSystem _postProcess = new();
    private readonly ParticleRenderSystem _particle = new();

    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var graph = context.RenderGraph;
        var activeCameras = world.GetResource<ActiveCameraList>();

        for (int i = 0; i < activeCameras.Cameras.Count; i++)
        {
            var entry = activeCameras.Cameras[i];
            var ctx = new CameraRenderContext(entry);

            // For RenderTexture cameras, ensure GPU resources and register in render graph
            if (entry.Target == CameraTarget.RenderTexture && entry.RenderTexture != null)
            {
                entry.RenderTexture.EnsureResources(driver);
                graph.RegisterExternalTexture(ctx.OutputName, entry.RenderTexture.ColorTexture!);
            }

            var layers = entry.RenderLayers;

            if (entry.CameraType == CameraType.Scene)
            {
                // 3D meshes
                if (layers.HasFlag(RenderLayers.Meshes))
                    _render.AddForwardPass(ctx, world);

                // Particles
                if (layers.HasFlag(RenderLayers.Particles))
                {
                    ParticleRenderSystem.EnsureRenderPipeline(context);
                    _particle.AddParticlePass(ctx, world);
                }

                // 2D sprites and text
                if (layers.HasFlag(RenderLayers.Sprites))
                    _sprite.AddSpritePass(ctx, world);
                if (layers.HasFlag(RenderLayers.Text))
                    _text.AddTextPass(ctx, world);

                if (entry.PostProcessEnabled)
                {
                    _postProcess.AddPostProcessPasses(ctx, world);
                }
                else
                {
                    // Blit SceneColor directly to output (backbuffer or RenderTexture)
                    AddBlitToOutput(graph, driver, ctx, context);
                }
            }
            else // UIOverlay
            {
                // 2D overlay: render sprites + text to an overlay texture, then composite onto backbuffer
                var pp = context.PostProcess;
                if (!pp.Initialized)
                {
                    PostProcessSystem.InitPipelinesStatic(context, driver, pp);
                    pp.Initialized = true;
                }

                // Ensure overlay SceneColor texture (separate from main camera's)
                var overlayTex = pp.GetCameraTextures(ctx.Prefix);
                overlayTex.EnsureSceneColor(driver, ctx.Width, ctx.Height);
                graph.RegisterExternalTexture(ctx.SceneColorName, overlayTex.SceneColorTexture!);

                // Clear overlay texture to transparent
                graph.AddPass($"{ctx.Prefix}Clear", setup: pass =>
                {
                    var overlayColor = pass.ImportTexture(ctx.SceneColorName, new TextureDescriptor
                    {
                        Width = ctx.Width, Height = ctx.Height,
                        Format = DriverPixelFormat.RGBA16Float,
                        Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
                    });
                    pass.WriteTexture(overlayColor);
                    pass.ColorAttachment(overlayColor, DriverLoadAction.Clear, DriverStoreAction.Store,
                        clearColor: new System.Numerics.Vector4(0, 0, 0, 0));
                }, execute: _ => { });

                // Render sprites and text onto the overlay texture
                if (layers.HasFlag(RenderLayers.Sprites))
                    _sprite.AddSpritePass(ctx, world);
                if (layers.HasFlag(RenderLayers.Text))
                    _text.AddTextPass(ctx, world);

                // Composite overlay onto backbuffer with alpha blending
                graph.AddPass($"{ctx.Prefix}Composite", setup: pass =>
                {
                    var overlayColor = pass.ImportTexture(ctx.SceneColorName, new TextureDescriptor
                    {
                        Width = ctx.Width, Height = ctx.Height,
                        Format = DriverPixelFormat.RGBA16Float,
                        Usage = TextureUsage.ShaderBinding,
                    });
                    pass.ReadTexture(overlayColor);

                    var output = pass.ImportTexture(ctx.OutputName, new TextureDescriptor
                    {
                        Width = ctx.Width, Height = ctx.Height,
                        Format = driver.SwapchainFormat,
                        Usage = TextureUsage.RenderAttachment,
                    });
                    pass.WriteTexture(output);
                    pass.ColorAttachment(output, DriverLoadAction.Load, DriverStoreAction.Store);
                }, execute: exeCtx =>
                {
                    var overlayView = exeCtx.GetTextureView(ctx.SceneColorName);
                    var bindingSet = driver.CreateBindingSetForPipeline(
                        pp.OverlayBlitPipeline!, 0,
                        textures: [new TextureBinding { Binding = 0, TextureView = overlayView }],
                        samplers: [new SamplerBinding { Binding = 1, Sampler = pp.LinearSampler! }]);

                    exeCtx.Encoder.SetPipeline(pp.OverlayBlitPipeline!);
                    exeCtx.Encoder.SetBindingSet(0, bindingSet);
                    exeCtx.Encoder.Draw(3);
                });
            }
        }
    }

    private static void AddBlitToOutput(RenderGraph.RenderGraph graph, IRenderDriver driver,
        CameraRenderContext ctx, RenderContext context)
    {
        // Ensure blit resources are initialized even if PostProcessSystem hasn't run
        var pp = context.PostProcess;
        if (!pp.Initialized)
        {
            PostProcessSystem.InitPipelinesStatic(context, driver, pp);
            pp.Initialized = true;
        }

        graph.AddPass($"{ctx.Prefix}BlitToOutput", setup: pass =>
        {
            var source = pass.ImportTexture(ctx.SceneColorName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(source);

            var output = pass.ImportTexture(ctx.OutputName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(output);
            pass.ColorAttachment(output, DriverLoadAction.Clear, DriverStoreAction.Store);
        }, execute: exeCtx =>
        {
            var sourceView = exeCtx.GetTextureView(ctx.SceneColorName);
            var bindingSet = driver.CreateBindingSetForPipeline(
                pp.BlitPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = sourceView }],
                samplers: [new SamplerBinding { Binding = 1, Sampler = pp.LinearSampler! }]);

            exeCtx.Encoder.SetPipeline(pp.BlitPipeline!);
            exeCtx.Encoder.SetBindingSet(0, bindingSet);
            exeCtx.Encoder.Draw(3);
        });
    }
}
