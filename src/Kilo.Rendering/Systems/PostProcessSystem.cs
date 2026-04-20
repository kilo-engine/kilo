using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering;

/// <summary>
/// Post-processing system that applies Bloom, ToneMapping, and FXAA.
/// Runs after TextRenderSystem, before EndFrameSystem.
/// All intermediate textures are persistent (created once, recreated on resize).
/// </summary>
public sealed class PostProcessSystem
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PostProcessParams
    {
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomEnabled;
        public float ToneMapEnabled;
        private Vector4 _pad0;
        private Vector4 _pad1;
        private Vector4 _pad2;
        private Vector4 _pad3;
        private Vector4 _pad4;
        private Vector4 _pad5;
        private Vector4 _pad6;
        private Vector4 _pad7;
        private Vector4 _pad8;
        private Vector4 _pad9;
        private Vector4 _pad10;
        private Vector4 _pad11;
        private Vector4 _pad12;
        private Vector4 _pad13;
        private Vector4 _pad14;
    }

    private const int WorkgroupSize = 16;

    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var ws = world.GetResource<WindowSize>();
        var settings = world.GetResource<RenderSettings>();
        var pp = context.PostProcess;
        var graph = context.RenderGraph;

        if (ws.Width <= 0 || ws.Height <= 0) return;

        // Lazy init GPU resources (pipelines, sampler, params buffer)
        if (!pp.Initialized)
        {
            InitPipelines(context, driver, pp);
            pp.Initialized = true;
        }

        // Ensure intermediate textures exist and match window size
        EnsureTextures(driver, pp, ws.Width, ws.Height);

        // Register all intermediate textures with the RenderGraph
        graph.RegisterExternalTexture("SceneColor", pp.SceneColorTexture!);
        graph.RegisterExternalTexture("BrightExtract", pp.BrightExtractTexture!);
        graph.RegisterExternalTexture("BloomBlurH", pp.BloomBlurHTexture!);
        graph.RegisterExternalTexture("BloomBlurV", pp.BloomBlurVTexture!);
        graph.RegisterExternalTexture("ToneMapped", pp.ToneMappedTexture!);

        // Upload params
        var paramData = new PostProcessParams[1];
        paramData[0].BloomThreshold = settings.BloomThreshold;
        paramData[0].BloomIntensity = settings.BloomIntensity;
        paramData[0].BloomEnabled = settings.BloomEnabled ? 1f : 0f;
        paramData[0].ToneMapEnabled = settings.ToneMappingEnabled ? 1f : 0f;
        pp.ParamsBuffer!.UploadData<PostProcessParams>(paramData.AsSpan());

        // Determine pass chain
        bool bloom = settings.BloomEnabled;
        bool tonemap = settings.ToneMappingEnabled;
        bool fxaa = settings.FxaaEnabled;

        if (!bloom && !tonemap && !fxaa)
        {
            AddBlitToBackbuffer(graph, driver, ws, pp, "SceneColor");
            return;
        }

        if (bloom)
        {
            AddBloomPasses(graph, driver, ws, pp);
        }

        // Composite+ToneMap always renders to ToneMapped or Backbuffer
        string compositeInput = bloom ? "BloomBlurV" : "SceneColor";

        if (fxaa)
        {
            // Composite+ToneMap → ToneMapped, then FXAA → Backbuffer
            AddCompositeToneMapPass(graph, driver, ws, pp, compositeInput);
            AddFxaaPass(graph, driver, ws, pp);
        }
        else if (tonemap)
        {
            // Composite+ToneMap → Backbuffer directly
            AddCompositeToneMapToBackbuffer(graph, driver, ws, pp, compositeInput);
        }
        else if (bloom)
        {
            // Bloom only, no tonemap: blit SceneColor+bloom → Backbuffer
            // Just blit SceneColor for now (bloom is additive in composite)
            AddBlitToBackbuffer(graph, driver, ws, pp, "SceneColor");
        }
    }

    private static void InitPipelines(RenderContext context, IRenderDriver driver, PostProcessState pp)
    {
        // Bloom extract compute pipeline
        var bloomExtractShader = driver.CreateComputeShaderModule(PostProcessShaders.BloomExtractWGSL, "main");
        pp.BloomExtractPipeline = driver.CreateComputePipeline(bloomExtractShader, "main");

        // Bloom blur compute pipelines
        var blurHShader = driver.CreateComputeShaderModule(PostProcessShaders.BloomBlurHWGSL, "main");
        pp.BloomBlurHPipeline = driver.CreateComputePipeline(blurHShader, "main");

        var blurVShader = driver.CreateComputeShaderModule(PostProcessShaders.BloomBlurVWGSL, "main");
        pp.BloomBlurVPipeline = driver.CreateComputePipeline(blurVShader, "main");

        // Composite + ToneMap render pipeline (fullscreen triangle, no vertex buffer)
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

        // FXAA render pipeline (outputs to swapchain format)
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

        // Blit pipeline (passthrough, outputs to swapchain format)
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

        // Shared linear sampler
        pp.LinearSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
        });

        // Uniform buffer for params
        pp.ParamsBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = 256,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
    }

    private static void EnsureTextures(IRenderDriver driver, PostProcessState pp, int width, int height)
    {
        if (pp.BrightExtractTexture != null && pp.TextureWidth == width && pp.TextureHeight == height)
            return;

        // Dispose old textures (SceneColor is managed by RenderSystem, not disposed here)
        pp.BrightExtractTexture?.Dispose();
        pp.BloomBlurHTexture?.Dispose();
        pp.BloomBlurVTexture?.Dispose();
        pp.ToneMappedTexture?.Dispose();
        pp.BrightExtractStorageView?.Dispose();
        pp.BloomBlurHStorageView?.Dispose();
        pp.BloomBlurVStorageView?.Dispose();

        // Bloom intermediate textures (RGBA16Float with Storage usage for compute)
        var hdrDesc = new TextureDescriptor
        {
            Width = width,
            Height = height,
            Format = DriverPixelFormat.RGBA16Float,
            Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
        };

        pp.BrightExtractTexture = driver.CreateTexture(hdrDesc);
        pp.BrightExtractStorageView = driver.CreateTextureView(pp.BrightExtractTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA16Float,
            Dimension = TextureViewDimension.View2D,
        });

        pp.BloomBlurHTexture = driver.CreateTexture(hdrDesc);
        pp.BloomBlurHStorageView = driver.CreateTextureView(pp.BloomBlurHTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA16Float,
            Dimension = TextureViewDimension.View2D,
        });

        pp.BloomBlurVTexture = driver.CreateTexture(hdrDesc);
        pp.BloomBlurVStorageView = driver.CreateTextureView(pp.BloomBlurVTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA16Float,
            Dimension = TextureViewDimension.View2D,
        });

        // ToneMapped output (RGBA8Unorm)
        pp.ToneMappedTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = width,
            Height = height,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
        });

        pp.TextureWidth = width;
        pp.TextureHeight = height;
    }

    private static void AddBloomPasses(RenderGraph.RenderGraph graph, IRenderDriver driver, WindowSize ws, PostProcessState pp)
    {
        uint wgX = (uint)((ws.Width + WorkgroupSize - 1) / WorkgroupSize);
        uint wgY = (uint)((ws.Height + WorkgroupSize - 1) / WorkgroupSize);

        // BloomExtract: SceneColor → BrightExtract
        graph.AddComputePass("BloomExtract", setup: cp =>
        {
            var sceneColor = cp.ImportTexture("SceneColor", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.ShaderBinding,
            });
            cp.ReadTexture(sceneColor);

            var brightExtract = cp.ImportTexture("BrightExtract", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.WriteTexture(brightExtract);
        }, execute: ctx =>
        {
            var sceneColorView = ctx.GetTextureView("SceneColor");

            var bindingSet = driver.CreateBindingSetForComputePipeline(
                pp.BloomExtractPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = sceneColorView }],
                storageTextures: [new StorageTextureBinding { Binding = 1, TextureView = pp.BrightExtractStorageView!, Format = DriverPixelFormat.RGBA16Float }],
                uniformBuffers: [new UniformBufferBinding { Binding = 2, Buffer = pp.ParamsBuffer! }]);

            ctx.Encoder.SetComputePipeline(pp.BloomExtractPipeline!);
            ctx.Encoder.SetComputeBindingSet(0, bindingSet);
            ctx.Encoder.Dispatch(wgX, wgY, 1);
        });

        // BloomBlurH: BrightExtract → BloomBlurH
        graph.AddComputePass("BloomBlurH", setup: cp =>
        {
            var brightExtract = cp.ImportTexture("BrightExtract", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.ReadTexture(brightExtract);

            var blurH = cp.ImportTexture("BloomBlurH", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.WriteTexture(blurH);
        }, execute: ctx =>
        {
            var brightView = ctx.GetTextureView("BrightExtract");

            var bindingSet = driver.CreateBindingSetForComputePipeline(
                pp.BloomBlurHPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = brightView }],
                storageTextures: [new StorageTextureBinding { Binding = 1, TextureView = pp.BloomBlurHStorageView!, Format = DriverPixelFormat.RGBA16Float }]);

            ctx.Encoder.SetComputePipeline(pp.BloomBlurHPipeline!);
            ctx.Encoder.SetComputeBindingSet(0, bindingSet);
            ctx.Encoder.Dispatch(wgX, wgY, 1);
        });

        // BloomBlurV: BloomBlurH → BloomBlurV
        graph.AddComputePass("BloomBlurV", setup: cp =>
        {
            var blurH = cp.ImportTexture("BloomBlurH", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.ReadTexture(blurH);

            var blurV = cp.ImportTexture("BloomBlurV", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
            });
            cp.WriteTexture(blurV);
        }, execute: ctx =>
        {
            var blurHView = ctx.GetTextureView("BloomBlurH");

            var bindingSet = driver.CreateBindingSetForComputePipeline(
                pp.BloomBlurVPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = blurHView }],
                storageTextures: [new StorageTextureBinding { Binding = 1, TextureView = pp.BloomBlurVStorageView!, Format = DriverPixelFormat.RGBA16Float }]);

            ctx.Encoder.SetComputePipeline(pp.BloomBlurVPipeline!);
            ctx.Encoder.SetComputeBindingSet(0, bindingSet);
            ctx.Encoder.Dispatch(wgX, wgY, 1);
        });
    }

    private static void AddCompositeToneMapPass(RenderGraph.RenderGraph graph, IRenderDriver driver, WindowSize ws, PostProcessState pp, string bloomInput)
    {
        graph.AddPass("CompositeToneMap", setup: pass =>
        {
            var sceneColor = pass.ImportTexture("SceneColor", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(sceneColor);

            var bloomBlur = pass.ImportTexture(bloomInput, new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(bloomBlur);

            var toneMapped = pass.ImportTexture("ToneMapped", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.WriteTexture(toneMapped);
            pass.ColorAttachment(toneMapped, DriverLoadAction.Clear, DriverStoreAction.Store);
        }, execute: ctx =>
        {
            var sceneColorView = ctx.GetTextureView("SceneColor");
            var bloomBlurView = ctx.GetTextureView(bloomInput);

            var bindingSet = driver.CreateBindingSetForPipeline(
                pp.CompositeToneMapPipeline!, 0,
                uniformBuffers: [new UniformBufferBinding { Binding = 3, Buffer = pp.ParamsBuffer! }],
                textures:
                [
                    new TextureBinding { Binding = 0, TextureView = sceneColorView },
                    new TextureBinding { Binding = 1, TextureView = bloomBlurView },
                ],
                samplers: [new SamplerBinding { Binding = 2, Sampler = pp.LinearSampler! }]);

            ctx.Encoder.SetPipeline(pp.CompositeToneMapPipeline!);
            ctx.Encoder.SetBindingSet(0, bindingSet);
            ctx.Encoder.Draw(3);
        });
    }

    private static void AddCompositeToneMapToBackbuffer(RenderGraph.RenderGraph graph, IRenderDriver driver, WindowSize ws, PostProcessState pp, string bloomInput)
    {
        AddCompositeToneMapPass(graph, driver, ws, pp, bloomInput);
        AddBlitToBackbuffer(graph, driver, ws, pp, "ToneMapped");
    }

    private static void AddFxaaPass(RenderGraph.RenderGraph graph, IRenderDriver driver, WindowSize ws, PostProcessState pp)
    {
        graph.AddPass("FXAA", setup: pass =>
        {
            var toneMapped = pass.ImportTexture("ToneMapped", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(toneMapped);

            var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store);
        }, execute: ctx =>
        {
            var toneMappedView = ctx.GetTextureView("ToneMapped");

            var bindingSet = driver.CreateBindingSetForPipeline(
                pp.FxaaPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = toneMappedView }],
                samplers: [new SamplerBinding { Binding = 1, Sampler = pp.LinearSampler! }]);

            ctx.Encoder.SetPipeline(pp.FxaaPipeline!);
            ctx.Encoder.SetBindingSet(0, bindingSet);
            ctx.Encoder.Draw(3);
        });
    }

    private static void AddBlitToBackbuffer(RenderGraph.RenderGraph graph, IRenderDriver driver, WindowSize ws, PostProcessState pp, string sourceName)
    {
        graph.AddPass("BlitToBackbuffer", setup: pass =>
        {
            var source = pass.ImportTexture(sourceName, new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = sourceName == "SceneColor" ? DriverPixelFormat.RGBA16Float : DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(source);

            var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store);
        }, execute: ctx =>
        {
            var sourceView = ctx.GetTextureView(sourceName);

            var bindingSet = driver.CreateBindingSetForPipeline(
                pp.BlitPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = sourceView }],
                samplers: [new SamplerBinding { Binding = 1, Sampler = pp.LinearSampler! }]);

            ctx.Encoder.SetPipeline(pp.BlitPipeline!);
            ctx.Encoder.SetBindingSet(0, bindingSet);
            ctx.Encoder.Draw(3);
        });
    }
}
