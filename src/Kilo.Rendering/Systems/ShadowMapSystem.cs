using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering;

/// <summary>
/// Renders a shadow map from the first directional light and adds it to the RenderGraph.
/// Must run before RenderSystem in KiloStage.Last.
/// </summary>
public sealed class ShadowMapSystem
{
    private const int ShadowMapSize = 2048;

    private IRenderPipeline? _shadowPipeline;
    private IBindingSet? _shadowCameraBinding;
    private IBindingSet? _shadowObjectBinding;
    private IBuffer? _shadowCameraBuffer;

    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var scene = world.GetResource<GpuSceneData>();
        var ws = world.GetResource<WindowSize>();

        // Find the first directional light
        var lightQuery = world.QueryBuilder()
            .With<DirectionalLight>()
            .Build();

        Vector3 lightDir = Vector3.UnitY;
        bool foundLight = false;

        var lightIter = lightQuery.Iter();
        while (lightIter.Next())
        {
            var lights = lightIter.Data<DirectionalLight>(lightIter.GetColumnIndexOf<DirectionalLight>());
            if (lightIter.Count > 0)
            {
                lightDir = Vector3.Normalize(lights[0].Direction);
                foundLight = true;
                break;
            }
        }

        if (!foundLight) return;

        // Calculate light-space ViewProjection (orthographic)
        float shadowRange = 20f;
        var lightPos = -lightDir * 10f;
        var lightView = Matrix4x4.CreateLookAt(lightPos, lightPos + lightDir, Vector3.UnitY);
        var lightProj = Matrix4x4.CreateOrthographicOffCenter(
            -shadowRange, shadowRange, -shadowRange, shadowRange, 0.1f, 50f);

        // WebGPU uses clip-space Z [0,1], but CreateOrthographic uses [-1,1].
        // Apply remap matrix: scale Z by 0.5 and offset by 0.5
        var remap = new Matrix4x4(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 0.5f, 0,
            0, 0, 0.5f, 1);
        var lightVP = lightView * lightProj * remap;

        scene.ShadowLightVP = lightVP;

        // Lazy init shadow resources
        if (_shadowPipeline == null)
        {
            var shadowVS = context.ShaderCache.GetOrCreateShader(driver, ShadowShaders.WGSL, "vs_main");

            _shadowPipeline = driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
            {
                VertexShader = shadowVS,
                FragmentShader = null, // depth-only pass — no fragment shader
                Topology = DriverPrimitiveTopology.TriangleList,
                ColorTargets = [],
                VertexBuffers =
                [
                    new VertexBufferLayout
                    {
                        ArrayStride = 8 * sizeof(float),
                        Attributes =
                        [
                            new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
                            new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x3, Offset = (nuint)(3 * sizeof(float)) },
                            new VertexAttributeDescriptor { ShaderLocation = 2, Format = VertexFormat.Float32x2, Offset = (nuint)(6 * sizeof(float)) },
                        ]
                    }
                ],
                DepthStencil = new DepthStencilStateDescriptor
                {
                    Format = DriverPixelFormat.Depth24Plus,
                    DepthCompare = DriverCompareFunction.Less,
                    DepthWriteEnabled = true,
                }
            }, minBindingSize: (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 2);

            _shadowCameraBuffer = driver.CreateBuffer(new BufferDescriptor
            {
                Size = (nuint)CameraData.Size,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });

            _shadowCameraBinding = driver.CreateBindingSetForPipeline(_shadowPipeline, 0, [new UniformBufferBinding { Buffer = _shadowCameraBuffer, Binding = 0 }]);
            _shadowObjectBinding = driver.CreateDynamicUniformBindingSet(
                _shadowPipeline, 1, scene.ObjectDataBuffer, (nuint)ObjectData.Size);
        }

        // Upload light-space camera data
        var shadowCamData = new CameraData
        {
            View = lightView,
            Projection = lightProj,
            Position = lightPos,
        };
        var shadowCamArray = new CameraData[1];
        shadowCamArray[0] = shadowCamData;
        _shadowCameraBuffer!.UploadData<CameraData>(shadowCamArray.AsSpan());

        // Upload shadow data for main pass (group 4, binding 2)
        if (context.ShadowDataBuffer != null)
        {
            var shadowUniform = new ShadowUniformData { LightVP = lightVP, ShadowEnabled = 1 };
            var shadowArray = new ShadowUniformData[1];
            shadowArray[0] = shadowUniform;
            context.ShadowDataBuffer.UploadData<ShadowUniformData>(shadowArray.AsSpan());
        }

        // Add shadow pass to shared RenderGraph
        var graph = context.RenderGraph;
        graph.AddPass("ShadowMap", setup: pass =>
        {
            var shadowDepth = pass.CreateTexture(new TextureDescriptor
            {
                Width = ShadowMapSize,
                Height = ShadowMapSize,
                Format = DriverPixelFormat.Depth24Plus,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.WriteTexture(shadowDepth);
            pass.DepthStencilAttachment(shadowDepth, DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);
        }, execute: ctx =>
        {
            var encoder = ctx.Encoder;
            encoder.SetViewport(0, 0, ShadowMapSize, ShadowMapSize);
            encoder.SetPipeline(_shadowPipeline!);
            encoder.SetBindingSet(0, _shadowCameraBinding!);
            // No binding set 2 (lights) or 3 (texture) needed for shadow pass

            for (int i = 0; i < scene.DrawCount; i++)
            {
                var draw = scene.DrawData[i];
                if (draw.MeshHandle < 0 || draw.MeshHandle >= context.Meshes.Count) continue;

                var mesh = context.Meshes[draw.MeshHandle];
                encoder.SetVertexBuffer(0, mesh.VertexBuffer);
                encoder.SetIndexBuffer(mesh.IndexBuffer);
                encoder.SetBindingSet(1, _shadowObjectBinding!, (uint)(i * 256));
                encoder.DrawIndexed((int)mesh.IndexCount);
            }
        });
    }
}

/// <summary>
/// GPU shadow uniform data matching WGSL ShadowData struct. Padded to 256 bytes.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct ShadowUniformData
{
    public Matrix4x4 LightVP;       // 64 bytes
    public int ShadowEnabled;       // 4 bytes
    private int _pad0;
    private int _pad1;
    private int _pad2;
    private System.Numerics.Vector4 _pad3;
    private System.Numerics.Vector4 _pad4;
    private System.Numerics.Vector4 _pad5;
    private System.Numerics.Vector4 _pad6;
    private System.Numerics.Vector4 _pad7;
    private System.Numerics.Vector4 _pad8;
    private System.Numerics.Vector4 _pad9;
}
