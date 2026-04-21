using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering;

/// <summary>
/// Post-processing system that applies Bloom, ToneMapping, and FXAA.
/// All intermediate textures are persistent per-camera (created once, recreated on resize).
/// </summary>
public sealed class PostProcessSystem
{
    [StructLayout(LayoutKind.Sequential, Size = 256)]
    private struct PostProcessParams
    {
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomEnabled;
        public float ToneMapEnabled;
    }

    private const int WorkgroupSize = 16;

    /// <summary>Backward-compatible entry point with default screen context.</summary>
    public void Update(KiloWorld world)
    {
        var ws = world.GetResource<WindowSize>();
        var scene = world.GetResource<GpuSceneData>();
        var ctx = new CameraRenderContext(new ActiveCameraEntry
        {
            CameraData = scene.PendingCamera,
            Target = CameraTarget.Screen,
            CameraType = CameraType.Scene,
            RenderWidth = ws.Width,
            RenderHeight = ws.Height,
            PostProcessEnabled = true,
        });
        AddPostProcessPasses(ctx, world);
    }

    public void AddPostProcessPasses(CameraRenderContext ctx, KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var settings = world.GetResource<RenderSettings>();
        var pp = world.GetResource<PostProcessState>();
        var graph = context.RenderGraph;

        // Lazy init GPU resources (pipelines, sampler, params buffer)
        if (!pp.Initialized)
        {
            InitPipelinesStatic(context, driver, pp);
            pp.Initialized = true;
        }

        // Get per-camera textures
        var camTex = pp.GetCameraTextures(ctx.Prefix);
        camTex.EnsureBloomTextures(driver, ctx.Width, ctx.Height);

        string sceneColorName = ctx.SceneColorName;
        string brightExtractName = $"{ctx.Prefix}BrightExtract";
        string bloomBlurHName = $"{ctx.Prefix}BloomBlurH";
        string bloomBlurVName = $"{ctx.Prefix}BloomBlurV";
        string toneMappedName = ctx.ToneMappedName;

        // Register all intermediate textures
        graph.RegisterExternalTexture(brightExtractName, camTex.BrightExtractTexture!);
        graph.RegisterExternalTexture(bloomBlurHName, camTex.BloomBlurHTexture!);
        graph.RegisterExternalTexture(bloomBlurVName, camTex.BloomBlurVTexture!);
        graph.RegisterExternalTexture(toneMappedName, camTex.ToneMappedTexture!);

        // Upload params
        var paramData = new PostProcessParams[1];
        paramData[0].BloomThreshold = settings.BloomThreshold;
        paramData[0].BloomIntensity = settings.BloomIntensity;
        paramData[0].BloomEnabled = settings.BloomEnabled ? 1f : 0f;
        paramData[0].ToneMapEnabled = settings.ToneMappingEnabled ? 1f : 0f;
        pp.ParamsBuffer!.UploadData<PostProcessParams>(paramData.AsSpan());

        bool bloom = settings.BloomEnabled;
        bool tonemap = settings.ToneMappingEnabled;
        bool fxaa = settings.FxaaEnabled;

        if (!bloom && !tonemap && !fxaa)
        {
            AddBlitToTarget(graph, driver, ctx, pp, sceneColorName, DriverPixelFormat.RGBA16Float);
            return;
        }

        if (bloom)
        {
            AddBloomPasses(graph, driver, ctx, pp, camTex, sceneColorName, brightExtractName, bloomBlurHName, bloomBlurVName);
        }

        string compositeInput = bloom ? bloomBlurVName : sceneColorName;

        if (fxaa)
        {
            AddCompositeToneMapPass(graph, driver, ctx, pp, camTex, sceneColorName, compositeInput, toneMappedName);
            AddFxaaPass(graph, driver, ctx, pp, toneMappedName);
        }
        else if (tonemap)
        {
            AddCompositeToneMapPass(graph, driver, ctx, pp, camTex, sceneColorName, compositeInput, toneMappedName);
            AddBlitToTarget(graph, driver, ctx, pp, toneMappedName, DriverPixelFormat.RGBA8Unorm);
        }
        else if (bloom)
        {
            AddBlitToTarget(graph, driver, ctx, pp, sceneColorName, DriverPixelFormat.RGBA16Float);
        }
    }

    public static void InitPipelinesStatic(RenderContext context, IRenderDriver driver, PostProcessState pp)
    {
        var bloomExtractShader = driver.CreateComputeShaderModule(PostProcessShaders.BloomExtractWGSL, "main");
        pp.BloomExtractPipeline = driver.CreateComputePipeline(bloomExtractShader, "main");

        var blurHShader = driver.CreateComputeShaderModule(PostProcessShaders.BloomBlurHWGSL, "main");
        pp.BloomBlurHPipeline = driver.CreateComputePipeline(blurHShader, "main");

        var blurVShader = driver.CreateComputeShaderModule(PostProcessShaders.BloomBlurVWGSL, "main");
        pp.BloomBlurVPipeline = driver.CreateComputePipeline(blurVShader, "main");

        var compositeVS = context.ShaderCache.GetOrCreateShader(driver, PostProcessShaders.CompositeToneMapWGSL, "vs_main");
        var compositeFS = context.ShaderCache.GetOrCreateShader(driver, PostProcessShaders.CompositeToneMapWGSL, "fs_main");
        pp.CompositeToneMapPipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = compositeVS,
            FragmentShader = compositeFS,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA8Unorm }],
            VertexBuffers = [],
        });

        var fxaaVS = context.ShaderCache.GetOrCreateShader(driver, PostProcessShaders.FxaaWGSL, "vs_main");
        var fxaaFS = context.ShaderCache.GetOrCreateShader(driver, PostProcessShaders.FxaaWGSL, "fs_main");
        pp.FxaaPipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = fxaaVS,
            FragmentShader = fxaaFS,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [new ColorTargetDescriptor { Format = driver.SwapchainFormat }],
            VertexBuffers = [],
        });

        var blitVS = context.ShaderCache.GetOrCreateShader(driver, PostProcessShaders.FullscreenBlitWGSL, "vs_main");
        var blitFS = context.ShaderCache.GetOrCreateShader(driver, PostProcessShaders.FullscreenBlitWGSL, "fs_main");
        pp.BlitPipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = blitVS,
            FragmentShader = blitFS,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [new ColorTargetDescriptor { Format = driver.SwapchainFormat }],
            VertexBuffers = [],
        });

        // Overlay blit: same shader but with alpha blending for UI overlay compositing
        pp.OverlayBlitPipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = blitVS,
            FragmentShader = blitFS,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [new ColorTargetDescriptor
            {
                Format = driver.SwapchainFormat,
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
            }],
            VertexBuffers = [],
        });

        pp.LinearSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
        });

        pp.ParamsBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = 256,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
    }

    private static void AddBloomPasses(RenderGraph.RenderGraph graph, IRenderDriver driver,
        CameraRenderContext ctx, PostProcessState pp, PerCameraTextures camTex,
        string sceneColorName, string brightExtractName, string bloomBlurHName, string bloomBlurVName)
    {
        uint wgX = (uint)((ctx.Width + WorkgroupSize - 1) / WorkgroupSize);
        uint wgY = (uint)((ctx.Height + WorkgroupSize - 1) / WorkgroupSize);

        // BloomExtract: SceneColor → BrightExtract
        graph.AddComputePass($"{ctx.Prefix}BloomExtract", setup: cp =>
        {
            var sceneColor = cp.ImportTexture(sceneColorName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.ShaderBinding,
            });
            cp.ReadTexture(sceneColor);

            var brightExtract = cp.ImportTexture(brightExtractName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.WriteTexture(brightExtract);
        }, execute: exeCtx =>
        {
            var sceneColorView = exeCtx.GetTextureView(sceneColorName);
            var bindingSet = driver.CreateBindingSetForComputePipeline(
                pp.BloomExtractPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = sceneColorView }],
                storageTextures: [new StorageTextureBinding { Binding = 1, TextureView = camTex.BrightExtractStorageView!, Format = DriverPixelFormat.RGBA16Float }],
                uniformBuffers: [new UniformBufferBinding { Binding = 2, Buffer = pp.ParamsBuffer! }]);

            exeCtx.Encoder.SetComputePipeline(pp.BloomExtractPipeline!);
            exeCtx.Encoder.SetComputeBindingSet(0, bindingSet);
            exeCtx.Encoder.Dispatch(wgX, wgY, 1);
        });

        // BloomBlurH: BrightExtract → BloomBlurH
        graph.AddComputePass($"{ctx.Prefix}BloomBlurH", setup: cp =>
        {
            var brightExtract = cp.ImportTexture(brightExtractName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.ReadTexture(brightExtract);

            var blurH = cp.ImportTexture(bloomBlurHName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.WriteTexture(blurH);
        }, execute: exeCtx =>
        {
            var brightView = exeCtx.GetTextureView(brightExtractName);
            var bindingSet = driver.CreateBindingSetForComputePipeline(
                pp.BloomBlurHPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = brightView }],
                storageTextures: [new StorageTextureBinding { Binding = 1, TextureView = camTex.BloomBlurHStorageView!, Format = DriverPixelFormat.RGBA16Float }]);

            exeCtx.Encoder.SetComputePipeline(pp.BloomBlurHPipeline!);
            exeCtx.Encoder.SetComputeBindingSet(0, bindingSet);
            exeCtx.Encoder.Dispatch(wgX, wgY, 1);
        });

        // BloomBlurV: BloomBlurH → BloomBlurV
        graph.AddComputePass($"{ctx.Prefix}BloomBlurV", setup: cp =>
        {
            var blurH = cp.ImportTexture(bloomBlurHName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.ReadTexture(blurH);

            var blurV = cp.ImportTexture(bloomBlurVName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.WriteTexture(blurV);
        }, execute: exeCtx =>
        {
            var blurHView = exeCtx.GetTextureView(bloomBlurHName);
            var bindingSet = driver.CreateBindingSetForComputePipeline(
                pp.BloomBlurVPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = blurHView }],
                storageTextures: [new StorageTextureBinding { Binding = 1, TextureView = camTex.BloomBlurVStorageView!, Format = DriverPixelFormat.RGBA16Float }]);

            exeCtx.Encoder.SetComputePipeline(pp.BloomBlurVPipeline!);
            exeCtx.Encoder.SetComputeBindingSet(0, bindingSet);
            exeCtx.Encoder.Dispatch(wgX, wgY, 1);
        });
    }

    private static void AddCompositeToneMapPass(RenderGraph.RenderGraph graph, IRenderDriver driver,
        CameraRenderContext ctx, PostProcessState pp, PerCameraTextures camTex,
        string sceneColorName, string bloomInput, string toneMappedName)
    {
        graph.AddPass($"{ctx.Prefix}CompositeToneMap", setup: pass =>
        {
            var sceneColor = pass.ImportTexture(sceneColorName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(sceneColor);

            var bloomBlur = pass.ImportTexture(bloomInput, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(bloomBlur);

            var toneMapped = pass.ImportTexture(toneMappedName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.WriteTexture(toneMapped);
            pass.ColorAttachment(toneMapped, DriverLoadAction.Clear, DriverStoreAction.Store);
        }, execute: exeCtx =>
        {
            var sceneColorView = exeCtx.GetTextureView(sceneColorName);
            var bloomBlurView = exeCtx.GetTextureView(bloomInput);

            var bindingSet = driver.CreateBindingSetForPipeline(
                pp.CompositeToneMapPipeline!, 0,
                uniformBuffers: [new UniformBufferBinding { Binding = 3, Buffer = pp.ParamsBuffer! }],
                textures:
                [
                    new TextureBinding { Binding = 0, TextureView = sceneColorView },
                    new TextureBinding { Binding = 1, TextureView = bloomBlurView },
                ],
                samplers: [new SamplerBinding { Binding = 2, Sampler = pp.LinearSampler! }]);

            exeCtx.Encoder.SetPipeline(pp.CompositeToneMapPipeline!);
            exeCtx.Encoder.SetBindingSet(0, bindingSet);
            exeCtx.Encoder.Draw(3);
        });
    }

    private static void AddFxaaPass(RenderGraph.RenderGraph graph, IRenderDriver driver,
        CameraRenderContext ctx, PostProcessState pp, string toneMappedName)
    {
        graph.AddPass($"{ctx.Prefix}FXAA", setup: pass =>
        {
            var toneMapped = pass.ImportTexture(toneMappedName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(toneMapped);

            var backbuffer = pass.ImportTexture(ctx.OutputName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store);
        }, execute: exeCtx =>
        {
            var toneMappedView = exeCtx.GetTextureView(toneMappedName);

            var bindingSet = driver.CreateBindingSetForPipeline(
                pp.FxaaPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = toneMappedView }],
                samplers: [new SamplerBinding { Binding = 1, Sampler = pp.LinearSampler! }]);

            exeCtx.Encoder.SetPipeline(pp.FxaaPipeline!);
            exeCtx.Encoder.SetBindingSet(0, bindingSet);
            exeCtx.Encoder.Draw(3);
        });
    }

    private static void AddBlitToTarget(RenderGraph.RenderGraph graph, IRenderDriver driver,
        CameraRenderContext ctx, PostProcessState pp, string sourceName, DriverPixelFormat sourceFormat)
    {
        graph.AddPass($"{ctx.Prefix}Blit", setup: pass =>
        {
            var source = pass.ImportTexture(sourceName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = sourceFormat,
                Usage = TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(source);

            var backbuffer = pass.ImportTexture(ctx.OutputName, new TextureDescriptor
            {
                Width = ctx.Width, Height = ctx.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store);
        }, execute: exeCtx =>
        {
            var sourceView = exeCtx.GetTextureView(sourceName);

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
