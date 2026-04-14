using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;
using Xunit;

namespace Kilo.Rendering.Tests;

public class SpriteRenderSystemTests
{
    [Fact]
    public void SpriteRenderSystem_WithSprites_DrawsExpectedCount()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };

        // Create sprite resources
        context.QuadVertexBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 64, Usage = BufferUsage.Vertex });
        context.QuadIndexBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 64, Usage = BufferUsage.Index });
        context.UniformBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 4096, Usage = BufferUsage.Uniform | BufferUsage.CopyDst });
        context.SpritePipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = driver.CreateShaderModule("", "vs"),
            FragmentShader = driver.CreateShaderModule("", "fs"),
        });
        context.BindingSet = driver.CreateBindingSet(new BindingSetDescriptor
        {
            Layout = new BindingSetLayout { Entries = [] }
        });

        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });

        // Create 3 sprite entities
        for (int i = 0; i < 3; i++)
        {
            world.Entity($"Sprite{i}")
                .Set(new LocalTransform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One })
                .Set(new LocalToWorld())
                .Set(new Sprite { Tint = Vector4.One, Size = Vector2.One, TextureHandle = -1, ZIndex = i });
        }

        // Compute LocalToWorld like the plugin does
        var computeQuery = world.QueryBuilder().With<LocalTransform>().With<LocalToWorld>().Build();
        var citer = computeQuery.Iter();
        while (citer.Next())
        {
            var transforms = citer.Data<LocalTransform>(citer.GetColumnIndexOf<LocalTransform>());
            var worlds = citer.Data<LocalToWorld>(citer.GetColumnIndexOf<LocalToWorld>());
            for (int i = 0; i < citer.Count; i++)
            {
                ref readonly var t = ref transforms[i];
                worlds[i].Value = Matrix4x4.CreateTranslation(t.Position)
                    * Matrix4x4.CreateFromQuaternion(t.Rotation)
                    * Matrix4x4.CreateScale(t.Scale);
            }
        }

        var system = new SpriteRenderSystem();
        var exception = Record.Exception(() => system.Update(world));

        Assert.Null(exception);
        context.RenderGraph.Execute(driver);
        Assert.NotNull(driver.LastEncoder);
        Assert.Equal(3, driver.LastEncoder.DrawIndexedCallCount);
    }

    [Fact]
    public void SpriteRenderSystem_WithNoSprites_DoesNotThrow()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };

        context.QuadVertexBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 64, Usage = BufferUsage.Vertex });
        context.QuadIndexBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 64, Usage = BufferUsage.Index });
        context.UniformBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 4096, Usage = BufferUsage.Uniform | BufferUsage.CopyDst });
        context.SpritePipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = driver.CreateShaderModule("", "vs"),
            FragmentShader = driver.CreateShaderModule("", "fs"),
        });
        context.BindingSet = driver.CreateBindingSet(new BindingSetDescriptor
        {
            Layout = new BindingSetLayout { Entries = [] }
        });

        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });

        var system = new SpriteRenderSystem();
        var exception = Record.Exception(() => system.Update(world));

        Assert.Null(exception);
    }
}
