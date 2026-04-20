using System.Numerics;
using Kilo.Rendering.Assets;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering.Materials;

/// <summary>
/// Manages creation of materials with configurable properties.
/// Reuses pipelines and delegates texture loading to TextureLoader.
/// </summary>
public sealed class MaterialManager
{
    private readonly TextureLoader _textureLoader = new();

    private IRenderPipeline? _opaquePipeline;
    private IRenderPipeline? _transparentPipeline;
    private ITextureView? _whiteTextureView;
    private ITextureView? _flatNormalTextureView;
    private ISampler? _placeholderComparisonSampler;
    private IBuffer? _placeholderShadowBuffer;

    private static readonly BlendStateDescriptor AlphaBlend = new()
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
        },
    };

    /// <summary>
    /// Creates a new material from the given descriptor.
    /// The material is added to the context's Materials list and its handle is returned.
    /// </summary>
    public int CreateMaterial(RenderContext context, GpuSceneData scene, MaterialDescriptor descriptor)
    {
        var driver = context.Driver;

        bool isTransparent = descriptor.ResolveTransparency();
        var pipeline = GetOrCreatePipeline(context, driver, isTransparent);

        // Get camera and light binding sets (shared across all materials using this pipeline)
        var cameraBindingSet = driver.CreateBindingSetForPipeline(pipeline, 0, [new UniformBufferBinding { Buffer = scene.CameraBuffer, Binding = 0 }]);
        var objectBindingSet = driver.CreateDynamicUniformBindingSet(pipeline, 1, scene.ObjectDataBuffer, (nuint)ObjectData.Size);
        var lightBindingSet = driver.CreateBindingSetForPipeline(pipeline, 2, [new UniformBufferBinding { Buffer = scene.LightBuffer, Binding = 0 }]);

        // Load albedo texture
        ITexture? albedoTexture = null;
        ISampler? albedoSampler = null;
        ITextureView albedoView;

        if (descriptor.AlbedoTexturePath != null)
        {
            albedoTexture = _textureLoader.LoadTexture(driver, descriptor.AlbedoTexturePath);
            albedoView = _textureLoader.GetOrCreateView(driver, albedoTexture);
            albedoSampler = _textureLoader.GetOrCreateSampler(driver);
        }
        else
        {
            albedoView = GetOrCreateWhiteTextureView(driver);
            albedoSampler = _textureLoader.GetOrCreateSampler(driver);
        }

        // Load normal map texture
        ITexture? normalMapTexture = null;
        ITextureView normalMapView;
        if (descriptor.NormalMapTexturePath != null)
        {
            normalMapTexture = _textureLoader.LoadTexture(driver, descriptor.NormalMapTexturePath);
            normalMapView = _textureLoader.GetOrCreateView(driver, normalMapTexture);
        }
        else
        {
            normalMapView = GetOrCreateFlatNormalTextureView(driver);
        }

        // Create texture binding set (group 3: albedo + normal + shadow)
        var textureBindingSet = CreateTextureBindingSet(driver, pipeline, scene, albedoView, normalMapView);

        var material = new Material
        {
            Pipeline = pipeline,
            BindingSets = [cameraBindingSet, objectBindingSet, lightBindingSet, textureBindingSet],
            BaseColor = descriptor.BaseColor,
            AlbedoTexture = albedoTexture,
            AlbedoSampler = albedoSampler,
            IsTransparent = isTransparent,
            Metallic = descriptor.Metallic,
            Roughness = descriptor.Roughness,
            NormalMapTexture = normalMapTexture,
        };

        return context.AddMaterial(material);
    }

    private IRenderPipeline GetOrCreatePipeline(RenderContext context, IRenderDriver driver, bool transparent)
    {
        ref var cache = ref transparent ? ref _transparentPipeline : ref _opaquePipeline;
        if (cache != null) return cache;
        return cache = CreatePipeline(context, driver, transparent);
    }

    private IRenderPipeline CreatePipeline(RenderContext context, IRenderDriver driver, bool transparent)
    {
        var vertexShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "vs_main");
        var fragmentShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "fs_main");

        var mesh = context.Meshes[0];

        var colorTarget = transparent
            ? new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA16Float, Blend = AlphaBlend }
            : new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA16Float };

        return driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [colorTarget],
            VertexBuffers = mesh.Layouts,
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = !transparent,
            },
        }, (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 4);
    }

    /// <summary>
    /// Gets or creates a cached 1x1 white texture view for materials without an albedo texture.
    /// </summary>
    private ITextureView GetOrCreateWhiteTextureView(IRenderDriver driver)
    {
        if (_whiteTextureView != null) return _whiteTextureView;

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });
        texture.UploadData<byte>([255, 255, 255, 255]);

        return _whiteTextureView = driver.CreateTextureView(texture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });
    }

    /// <summary>
    /// Gets or creates a cached 1x1 flat normal texture view (128,128,255 = +Z in tangent space).
    /// </summary>
    private ITextureView GetOrCreateFlatNormalTextureView(IRenderDriver driver)
    {
        if (_flatNormalTextureView != null) return _flatNormalTextureView;

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });
        texture.UploadData<byte>([128, 128, 255, 255]);

        return _flatNormalTextureView = driver.CreateTextureView(texture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });
    }

    /// <summary>
    /// Creates bind group 3 (albedo + normal + shadow) for the given pipeline.
    /// Always provides all 7 bindings (0-6) to match the WGSL shader layout.
    /// When shadow resources are unavailable, uses placeholder resources.
    /// </summary>
    public IBindingSet CreateTextureBindingSet(
        IRenderDriver driver, IRenderPipeline pipeline, GpuSceneData scene,
        ITextureView albedoView, ITextureView normalMapView)
    {
        var albedoSampler = _textureLoader.GetOrCreateSampler(driver);
        var normalSampler = _textureLoader.GetOrCreateSampler(driver);

        // Shadow resources: use real ones if available, otherwise placeholders
        var shadowData = scene.ShadowDataBuffer ?? GetOrCreatePlaceholderShadowBuffer(driver);
        var shadowSampler = scene.ShadowSampler ?? GetOrCreatePlaceholderComparisonSampler(driver);
        var depthView = scene.ShadowDepthView ?? _textureLoader.GetOrCreatePlaceholderDepthView(driver);

        return driver.CreateBindingSetForPipeline(pipeline, 3,
            [new UniformBufferBinding { Buffer = shadowData, Binding = 4 }],
            [
                new TextureBinding { Binding = 0, TextureView = albedoView },
                new TextureBinding { Binding = 2, TextureView = depthView },
                new TextureBinding { Binding = 5, TextureView = normalMapView },
            ],
            [
                new SamplerBinding { Binding = 1, Sampler = albedoSampler },
                new SamplerBinding { Binding = 3, Sampler = shadowSampler },
                new SamplerBinding { Binding = 6, Sampler = normalSampler },
            ]);
    }

    private IBuffer GetOrCreatePlaceholderShadowBuffer(IRenderDriver driver)
    {
        if (_placeholderShadowBuffer != null) return _placeholderShadowBuffer;
        _placeholderShadowBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = 256, // matches ShadowUniformData
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
        // shadow_enabled = 0 (all zeros)
        var zeros = new byte[256];
        _placeholderShadowBuffer.UploadData<byte>(zeros);
        return _placeholderShadowBuffer;
    }

    private ISampler GetOrCreatePlaceholderComparisonSampler(IRenderDriver driver)
    {
        if (_placeholderComparisonSampler != null) return _placeholderComparisonSampler;
        return _placeholderComparisonSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            Compare = true,
            CompareFunction = DriverCompareFunction.Always,
        });
    }
}
