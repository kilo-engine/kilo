using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering.Materials;

internal static class BuiltinMaterials
{
    /// <summary>
    /// Creates the default BasicLit material with shadow support.
    /// </summary>
    public static void CreateDefaultMaterial(RenderContext context, GpuSceneData scene, IRenderDriver driver)
    {
        var basicLitVertexShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "vs_main");
        var basicLitFragmentShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "fs_main");

        var swapchainFormat = driver.SwapchainFormat;
        var cubeMesh = context.Meshes[0];

        var basicLitPipelineKey = new PipelineCacheKey
        {
            VertexShaderSource = BasicLitShaders.WGSL,
            VertexShaderEntryPoint = "vs_main",
            FragmentShaderSource = BasicLitShaders.WGSL,
            FragmentShaderEntryPoint = "fs_main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = cubeMesh.Layouts,
            ColorTargets =
            [
                new ColorTargetDescriptor
                {
                    Format = swapchainFormat,
                }
            ],
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = true,
            }
        };

        var basicLitPipeline = context.PipelineCache.GetOrCreate(driver, basicLitPipelineKey, () => driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = basicLitVertexShader,
            FragmentShader = basicLitFragmentShader,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = basicLitPipelineKey.ColorTargets,
            VertexBuffers = cubeMesh.Layouts,
            DepthStencil = basicLitPipelineKey.DepthStencil,
        }, (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 4));

        var cameraBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 0, [new UniformBufferBinding { Buffer = scene.CameraBuffer, Binding = 0 }]);
        var objectBindingSet = driver.CreateDynamicUniformBindingSet(basicLitPipeline, 1, scene.ObjectDataBuffer, (nuint)ObjectData.Size);
        var lightBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 2, [new UniformBufferBinding { Buffer = scene.LightBuffer, Binding = 0 }]);

        // Default white 1x1 texture
        var defaultTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1,
            Height = 1,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
            MipLevelCount = 1,
            SampleCount = 1,
        });
        defaultTexture.UploadData<byte>([255, 255, 255, 255]);

        var defaultTextureView = driver.CreateTextureView(defaultTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });

        var defaultSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            MipFilter = FilterMode.Linear,
            AddressModeU = WrapMode.Repeat,
            AddressModeV = WrapMode.Repeat,
            AddressModeW = WrapMode.Repeat,
        });

        // Shadow resources
        var shadowSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            AddressModeU = WrapMode.ClampToEdge,
            AddressModeV = WrapMode.ClampToEdge,
            Compare = true,
            CompareFunction = DriverCompareFunction.Less,
        });

        var shadowDataBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = 256,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
        context.ShadowDataBuffer = shadowDataBuffer;
        context.ShadowSampler = shadowSampler;

        var placeholderDepthTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1,
            Format = DriverPixelFormat.Depth24Plus,
            Usage = TextureUsage.ShaderBinding,
        });
        var placeholderDepthView = driver.CreateTextureView(placeholderDepthTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.Depth24Plus,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });

        var textureBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 3,
            [new UniformBufferBinding { Buffer = shadowDataBuffer, Binding = 4 }],
            [
                new TextureBinding { Binding = 0, TextureView = defaultTextureView },
                new TextureBinding { Binding = 2, TextureView = placeholderDepthView },
            ],
            [
                new SamplerBinding { Binding = 1, Sampler = defaultSampler },
                new SamplerBinding { Binding = 3, Sampler = shadowSampler },
            ]);

        var basicLitMaterial = new Material
        {
            Pipeline = basicLitPipeline,
            BindingSets = [cameraBindingSet, objectBindingSet, lightBindingSet, textureBindingSet],
            AlbedoTexture = defaultTexture,
            AlbedoSampler = defaultSampler,
        };

        context.Materials.Add(basicLitMaterial);
    }
}
