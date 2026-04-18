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

    /// <summary>
    /// Creates a new material from the given descriptor.
    /// The material is added to the context's Materials list and its handle is returned.
    /// </summary>
    public int CreateMaterial(RenderContext context, GpuSceneData scene, MaterialDescriptor descriptor)
    {
        var driver = context.Driver;

        // Get or create the BasicLit pipeline (all materials share the same pipeline)
        var pipeline = GetOrCreatePipeline(context, scene, driver);

        // Get camera and light binding sets (shared across all materials using this pipeline)
        var cameraBindingSet = driver.CreateBindingSetForPipeline(pipeline, 0, [new UniformBufferBinding { Buffer = scene.CameraBuffer, Binding = 0 }]);
        var objectBindingSet = driver.CreateDynamicUniformBindingSet(pipeline, 1, scene.ObjectDataBuffer, (nuint)ObjectData.Size);
        var lightBindingSet = driver.CreateBindingSetForPipeline(pipeline, 2, [new UniformBufferBinding { Buffer = scene.LightBuffer, Binding = 0 }]);

        // Create texture binding set (group 3: albedo + shadow)
        IBindingSet textureBindingSet;
        ITexture? albedoTexture = null;
        ISampler? albedoSampler = null;

        // Reuse the first material's group 3 binding set (which includes shadow resources)
        var existingMaterial = context.Materials.Count > 0 ? context.Materials[0] : null;
        if (existingMaterial?.BindingSets.Length > 3 && descriptor.AlbedoTexturePath == null)
        {
            textureBindingSet = existingMaterial.BindingSets[3];
        }
        else if (descriptor.AlbedoTexturePath != null)
        {
            albedoTexture = _textureLoader.LoadTexture(driver, descriptor.AlbedoTexturePath);
            var textureView = _textureLoader.GetOrCreateView(driver, albedoTexture);
            albedoSampler = _textureLoader.GetOrCreateSampler(driver);

            var shadowData = scene.ShadowDataBuffer;
            var shadowSampler = scene.ShadowSampler;
            var depthView = _textureLoader.GetOrCreatePlaceholderDepthView(driver);
            if (shadowData != null && shadowSampler != null)
            {
                textureBindingSet = driver.CreateBindingSetForPipeline(pipeline, 3,
                    [new UniformBufferBinding { Buffer = shadowData, Binding = 4 }],
                    [
                        new TextureBinding { Binding = 0, TextureView = textureView },
                        new TextureBinding { Binding = 2, TextureView = depthView },
                    ],
                    [
                        new SamplerBinding { Binding = 1, Sampler = albedoSampler },
                        new SamplerBinding { Binding = 3, Sampler = shadowSampler },
                    ]);
            }
            else
            {
                textureBindingSet = driver.CreateBindingSetForPipeline(pipeline, 3,
                    [new TextureBinding { Binding = 0, TextureView = textureView }],
                    [new SamplerBinding { Binding = 1, Sampler = albedoSampler }]);
            }
        }
        else if (existingMaterial?.BindingSets.Length > 3)
        {
            textureBindingSet = existingMaterial.BindingSets[3];
        }
        else
        {
            // Fallback: create a 1x1 white texture
            var whiteTexture = driver.CreateTexture(new TextureDescriptor
            {
                Width = 1, Height = 1,
                Format = DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
            });
            whiteTexture.UploadData<byte>([255, 255, 255, 255]);

            var whiteView = driver.CreateTextureView(whiteTexture, new TextureViewDescriptor
            {
                Format = DriverPixelFormat.RGBA8Unorm,
                Dimension = TextureViewDimension.View2D,
                MipLevelCount = 1,
            });

            albedoSampler = _textureLoader.GetOrCreateSampler(driver);
            var shadowData = scene.ShadowDataBuffer;
            var shadowSampler = scene.ShadowSampler;
            var depthView = _textureLoader.GetOrCreatePlaceholderDepthView(driver);
            if (shadowData != null && shadowSampler != null)
            {
                textureBindingSet = driver.CreateBindingSetForPipeline(pipeline, 3,
                    [new UniformBufferBinding { Buffer = shadowData, Binding = 4 }],
                    [
                        new TextureBinding { Binding = 0, TextureView = whiteView },
                        new TextureBinding { Binding = 2, TextureView = depthView },
                    ],
                    [
                        new SamplerBinding { Binding = 1, Sampler = albedoSampler },
                        new SamplerBinding { Binding = 3, Sampler = shadowSampler },
                    ]);
            }
            else
            {
                textureBindingSet = driver.CreateBindingSetForPipeline(pipeline, 3,
                    [new TextureBinding { Binding = 0, TextureView = whiteView }],
                    [new SamplerBinding { Binding = 1, Sampler = albedoSampler }]);
            }
        }

        var material = new Material
        {
            Pipeline = pipeline,
            BindingSets = [cameraBindingSet, objectBindingSet, lightBindingSet, textureBindingSet],
            BaseColor = descriptor.BaseColor,
            AlbedoTexture = albedoTexture,
            AlbedoSampler = albedoSampler,
        };

        return context.AddMaterial(material);
    }

    private IRenderPipeline GetOrCreatePipeline(RenderContext context, GpuSceneData scene, IRenderDriver driver)
    {
        // If a BasicLit pipeline already exists in the materials list, reuse it
        foreach (var mat in context.Materials)
        {
            return mat.Pipeline;
        }

        // Create the pipeline (first time)
        var vertexShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "vs_main");
        var fragmentShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "fs_main");

        var mesh = context.Meshes[0]; // Default cube mesh

        return driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [new ColorTargetDescriptor { Format = driver.SwapchainFormat }],
            VertexBuffers = mesh.Layouts,
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = true,
            },
        }, (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 4);
    }
}
