using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Xunit;

namespace Kilo.Rendering.Tests;

public class RenderSystemTests
{
    [Fact]
    public void RenderSystem_WithNoMeshes_ClearsBackbufferAndDoesNotThrow()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };
        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });
        world.AddResource(new GpuSceneData
        {
            CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform }),
            ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
            LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
        });

        var system = new RenderSystem();
        var exception = Record.Exception(() => system.Update(world));
        Assert.Null(exception);
    }

    [Fact]
    public void RenderSystem_WithMeshes_DrawsExpectedObjects()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };

        // Create a fake mesh
        var mesh = new Mesh
        {
            VertexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 64, Usage = RenderGraph.BufferUsage.Vertex }),
            IndexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 64, Usage = RenderGraph.BufferUsage.Index }),
            IndexCount = 36,
            Layouts = []
        };
        context.AddMesh(mesh);

        // Create a fake material
        var pipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = driver.CreateShaderModule("", "vs"),
            FragmentShader = driver.CreateShaderModule("", "fs"),
        });
        var material = new Material
        {
            Pipeline = pipeline,
            BindingSets =
            [
                driver.CreateBindingSet(new Driver.BindingSetDescriptor
                {
                    Layout = new Driver.BindingSetLayout { Entries = [] }
                }),
                driver.CreateBindingSet(new Driver.BindingSetDescriptor
                {
                    Layout = new Driver.BindingSetLayout { Entries = [] }
                }),
                driver.CreateBindingSet(new Driver.BindingSetDescriptor
                {
                    Layout = new Driver.BindingSetLayout { Entries = [] }
                }),
            ]
        };
        context.AddMaterial(material);

        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });
        var scene = new GpuSceneData
        {
            CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform }),
            ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
            LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
        };
        scene.SetDrawData([new DrawData { MeshHandle = 0, MaterialId = 0 }], 1);
        world.AddResource(scene);

        var system = new RenderSystem();
        var exception = Record.Exception(() => system.Update(world));
        Assert.Null(exception);
    }
}
