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

public class FrustumCullingSystemTests
{
    /// <summary>Helper: creates a world with a RenderContext that has a default unit-cube mesh in the store.</summary>
    private static (RenderContext context, RenderResourceStore store) CreateContextWithDefaultMesh()
    {
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };
        var store = new RenderResourceStore();
        var vb = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 128, Usage = RenderGraph.BufferUsage.Vertex });
        var ib = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 64, Usage = RenderGraph.BufferUsage.Index });
        store.AddMesh(new Mesh
        {
            VertexBuffer = vb,
            IndexBuffer = ib,
            IndexCount = 36,
            Bounds = (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f)),
        });
        return (context, store);
    }

    [Fact]
    public void FrustumCulling_NoCamera_DoesNotThrow()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        world.AddResource(new RenderContext { Driver = driver });
        world.AddResource(new RenderResourceStore());
        world.AddResource(new WindowSize { Width = 800, Height = 600 });

        var system = new FrustumCullingSystem();
        var ex = Record.Exception(() => system.Update(world));
        Assert.Null(ex);
    }

    [Fact]
    public void FrustumCulling_VisibleEntity_NotCulled()
    {
        var world = new KiloWorld();
        var (context, store) = CreateContextWithDefaultMesh();
        world.AddResource(context);
        world.AddResource(store);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });

        // Camera at (0,0,10) looking forward
        world.Entity("Camera")
            .Set(new LocalTransform { Position = new Vector3(0, 0, 10), Rotation = Quaternion.Identity, Scale = Vector3.One })
            .Set(new Camera
            {
                FieldOfView = MathF.PI / 4,
                NearPlane = 0.1f,
                FarPlane = 100f,
                IsActive = true,
                ViewMatrix = Matrix4x4.CreateLookAt(new Vector3(0, 0, 10), new Vector3(0, 0, 0), Vector3.UnitY),
                ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f),
            });

        // Visible mesh at origin
        var entity = world.Entity("VisibleMesh")
            .Set(new MeshRenderer { MeshHandle = new MeshHandle(0), MaterialHandle = new MaterialHandle(0) })
            .Set(new LocalToWorld { Value = Matrix4x4.Identity });

        var system = new FrustumCullingSystem();
        system.Update(world);

        var entityId = new EntityId(entity.Id);
        Assert.False(world.Has<Culled>(entityId));
    }

    [Fact]
    public void FrustumCulling_BehindCamera_GetsCulled()
    {
        var world = new KiloWorld();
        var (context, store) = CreateContextWithDefaultMesh();
        world.AddResource(context);
        world.AddResource(store);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });

        world.Entity("Camera")
            .Set(new LocalTransform { Position = new Vector3(0, 0, 10), Rotation = Quaternion.Identity, Scale = Vector3.One })
            .Set(new Camera
            {
                FieldOfView = MathF.PI / 4,
                NearPlane = 0.1f,
                FarPlane = 100f,
                IsActive = true,
                ViewMatrix = Matrix4x4.CreateLookAt(new Vector3(0, 0, 10), new Vector3(0, 0, 0), Vector3.UnitY),
                ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f),
            });

        // Mesh far behind camera
        var entity = world.Entity("HiddenMesh")
            .Set(new MeshRenderer { MeshHandle = new MeshHandle(0), MaterialHandle = new MaterialHandle(0) })
            .Set(new LocalToWorld { Value = Matrix4x4.CreateTranslation(0, 0, -100) });

        var system = new FrustumCullingSystem();
        system.Update(world);

        var entityId = new EntityId(entity.Id);
        Assert.True(world.Has<Culled>(entityId));
    }

    [Fact]
    public void FrustumCulling_InvalidMeshHandle_GetsCulled()
    {
        var world = new KiloWorld();
        var (context, store) = CreateContextWithDefaultMesh();
        world.AddResource(context);
        world.AddResource(store);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });

        world.Entity("Camera")
            .Set(new LocalTransform { Position = new Vector3(0, 0, 10), Rotation = Quaternion.Identity, Scale = Vector3.One })
            .Set(new Camera
            {
                FieldOfView = MathF.PI / 4,
                NearPlane = 0.1f,
                FarPlane = 100f,
                IsActive = true,
                ViewMatrix = Matrix4x4.CreateLookAt(new Vector3(0, 0, 10), new Vector3(0, 0, 0), Vector3.UnitY),
                ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f),
            });

        var entity = world.Entity("InvalidMesh")
            .Set(new MeshRenderer { MeshHandle = MeshHandle.Invalid, MaterialHandle = new MaterialHandle(0) })
            .Set(new LocalToWorld { Value = Matrix4x4.Identity });

        var system = new FrustumCullingSystem();
        system.Update(world);

        var entityId = new EntityId(entity.Id);
        Assert.True(world.Has<Culled>(entityId));
    }
}
