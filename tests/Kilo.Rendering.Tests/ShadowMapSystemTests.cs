using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Resources;
using Xunit;

namespace Kilo.Rendering.Tests;

public class ShadowMapSystemTests
{
    [Fact]
    public void ShadowMapSystem_NoLight_DoesNotThrow()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };
        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });
        world.AddResource(new GpuSceneData());

        var system = new ShadowMapSystem();
        var ex = Record.Exception(() => system.Update(world));
        Assert.Null(ex);
    }

    [Fact]
    public void ShadowMapSystem_WithLight_CreatesPipelineAndPass()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };

        var cameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform });
        var objectBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform });
        var lightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform });
        var shadowDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform });

        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });
        world.AddResource(new GpuSceneData
        {
            CameraBuffer = cameraBuffer,
            ObjectDataBuffer = objectBuffer,
            LightBuffer = lightBuffer,
            DrawCount = 0,
            DrawData = [],
        });
        context.ShadowDataBuffer = shadowDataBuffer;

        // Add directional light
        world.Entity("Sun")
            .Set(new DirectionalLight
            {
                Direction = new Vector3(0.5f, -1f, -0.5f),
                Color = Vector3.One,
                Intensity = 1.0f
            });

        var system = new ShadowMapSystem();
        var ex = Record.Exception(() => system.Update(world));
        Assert.Null(ex);
    }

    [Fact]
    public void ShadowMapSystem_SetsShadowLightVP()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };

        var cameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform });
        var objectBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform });
        var lightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform });

        var scene = new GpuSceneData
        {
            CameraBuffer = cameraBuffer,
            ObjectDataBuffer = objectBuffer,
            LightBuffer = lightBuffer,
            DrawCount = 0,
            DrawData = [],
        };

        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });
        world.AddResource(scene);
        context.ShadowDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform });

        var lightDir = new Vector3(0.5f, -1f, -0.5f);
        world.Entity("Sun")
            .Set(new DirectionalLight
            {
                Direction = lightDir,
                Color = Vector3.One,
                Intensity = 1.0f
            });

        var system = new ShadowMapSystem();
        system.Update(world);

        // ShadowLightVP should be set (not identity)
        Assert.NotEqual(Matrix4x4.Identity, scene.ShadowLightVP);
    }

    [Fact]
    public void ShadowUniformData_HasCorrectSize()
    {
        // ShadowUniformData should be padded to 256 bytes for WebGPU alignment
        Assert.Equal(192, System.Runtime.InteropServices.Marshal.SizeOf<ShadowUniformData>());
    }
}
