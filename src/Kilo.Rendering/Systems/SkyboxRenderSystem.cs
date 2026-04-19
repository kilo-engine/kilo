using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Assets;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering;

/// <summary>
/// Lazy-initializes skybox resources (cubemap, pipeline, mesh).
/// The actual drawing happens in RenderSystem within the Forward pass.
/// </summary>
public sealed class SkyboxRenderSystem
{
    private bool _initialized;

    public void Update(KiloWorld world)
    {
        if (_initialized) return;

        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var settings = world.GetResource<RenderSettings>();

        InitResources(world, driver, context, settings);
        _initialized = true;
    }

    private void InitResources(KiloWorld world, IRenderDriver driver, RenderContext context, RenderSettings settings)
    {
        var skybox = context.Skybox;
        // Load cubemap
        var loader = new CubemapLoader();
        ITextureView cubemapView;
        if (settings.SkyboxFacePaths is { Length: 6 })
        {
            cubemapView = loader.LoadCubemap(driver, settings.SkyboxFacePaths);
        }
        else
        {
            cubemapView = loader.CreateGradientCubemap(driver,
                new Vector3(0.53f, 0.81f, 0.92f), // #87CEEB sky blue
                new Vector3(0.53f, 0.81f, 0.92f),
                64);
        }

        // Skybox cube mesh (position only)
        float[] cubeVertices =
        [
            -1, -1,  1,   1, -1,  1,   1,  1,  1,  -1,  1,  1,
            -1, -1, -1,   1, -1, -1,   1,  1, -1,  -1,  1, -1,
        ];
        uint[] cubeIndices =
        [
            0, 1, 2,  0, 2, 3,
            5, 4, 7,  5, 7, 6,
            4, 0, 3,  4, 3, 7,
            1, 5, 6,  1, 6, 2,
            3, 2, 6,  3, 6, 7,
            4, 5, 1,  4, 1, 0,
        ];

        var vb = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(cubeVertices.Length * sizeof(float)),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        vb.UploadData<float>(cubeVertices);

        var ib = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(cubeIndices.Length * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        ib.UploadData<uint>(cubeIndices);

        // Pipeline
        var vs = context.ShaderCache.GetOrCreateShader(driver, SkyboxShaders.WGSL, "vs_main");
        var fs = context.ShaderCache.GetOrCreateShader(driver, SkyboxShaders.WGSL, "fs_main");

        var pipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = vs,
            FragmentShader = fs,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [new ColorTargetDescriptor { Format = driver.SwapchainFormat }],
            VertexBuffers =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 3 * sizeof(float),
                    Attributes =
                    [
                        new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
                    ]
                }
            ],
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.LessEqual,
                DepthWriteEnabled = false,
            },
        });

        // Camera uniform buffer
        var cameraBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)CameraData.Size,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        var cameraBinding = driver.CreateBindingSetForPipeline(pipeline, 0,
            [new UniformBufferBinding { Buffer = cameraBuffer, Binding = 0 }]);

        var sampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
        });
        var textureBinding = driver.CreateBindingSetForPipeline(pipeline, 1,
            [new TextureBinding { Binding = 0, TextureView = cubemapView }],
            [new SamplerBinding { Binding = 1, Sampler = sampler }]);

        // Store in shared context
        skybox.Pipeline = pipeline;
        skybox.VertexBuffer = vb;
        skybox.IndexBuffer = ib;
        skybox.CameraBuffer = cameraBuffer;
        skybox.CameraBinding = cameraBinding;
        skybox.TextureBinding = textureBinding;
    }
}

/// <summary>
/// Shared skybox rendering state, initialized by SkyboxRenderSystem, used by RenderSystem.
/// </summary>
public sealed class SkyboxState
{
    public IRenderPipeline Pipeline { get; set; } = null!;
    public IBuffer VertexBuffer { get; set; } = null!;
    public IBuffer IndexBuffer { get; set; } = null!;
    public IBuffer CameraBuffer { get; set; } = null!;
    public IBindingSet CameraBinding { get; set; } = null!;
    public IBindingSet TextureBinding { get; set; } = null!;
}
