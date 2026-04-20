using Kilo.Rendering.Driver;
using Kilo.Rendering.Materials;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering.Scene;

internal static class SpriteResources
{
    /// <summary>
    /// Creates sprite rendering pipeline and resources (quad mesh, uniform buffer, binding set).
    /// </summary>
    public static void Create(RenderContext context, IRenderDriver driver)
    {
        var spriteVertexShader = context.ShaderCache.GetOrCreateShader(driver, SpriteShaders.WGSL, "vs_main");
        var spriteFragmentShader = context.ShaderCache.GetOrCreateShader(driver, SpriteShaders.WGSL, "fs_main");

        const int UniformStructSize = 144; // model(64) + projection(64) + color(16)
        const int UniformAlign = 256;      // WebGPU minUniformBufferOffsetAlignment
        const int MaxSprites = 64;
        const int UniformBufferSize = UniformAlign * MaxSprites;

        var swapchainFormat = DriverPixelFormat.RGBA16Float;

        var spritePipelineKey = new PipelineCacheKey
        {
            VertexShaderSource = SpriteShaders.WGSL,
            VertexShaderEntryPoint = "vs_main",
            FragmentShaderSource = SpriteShaders.WGSL,
            FragmentShaderEntryPoint = "fs_main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 2 * sizeof(float),
                    Attributes =
                    [
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 0,
                            Format = VertexFormat.Float32x2,
                            Offset = 0,
                        }
                    ]
                }
            ],
            ColorTargets =
            [
                new ColorTargetDescriptor
                {
                    Format = swapchainFormat,
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
        };

        context.Sprite.Pipeline = context.PipelineCache.GetOrCreate(driver, spritePipelineKey, () => driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = spriteVertexShader,
            FragmentShader = spriteFragmentShader,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = spritePipelineKey.ColorTargets,
            VertexBuffers = spritePipelineKey.VertexBuffers,
        }, (nuint)UniformStructSize, groupIndex: 0, bindGroupCount: 1));

        float[] quadVertices = [-0.5f, 0.5f, 0.5f, 0.5f, -0.5f, -0.5f, 0.5f, -0.5f];
        uint[] quadIndices = [0u, 1, 2, 2, 1, 3];

        context.Sprite.QuadVertexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(quadVertices.Length * sizeof(float)),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        context.Sprite.QuadVertexBuffer.UploadData<float>(quadVertices);

        context.Sprite.QuadIndexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(quadIndices.Length * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        context.Sprite.QuadIndexBuffer.UploadData<uint>(quadIndices);

        context.Sprite.UniformBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)UniformBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        context.Sprite.BindingSet = driver.CreateDynamicUniformBindingSet(
            context.Sprite.Pipeline, 0, context.Sprite.UniformBuffer, UniformStructSize);
    }
}
