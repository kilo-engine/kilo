using System.Numerics;
using Kilo.ECS;
using Xunit;

namespace Kilo.Rendering.Tests;

public class CameraSystemTests
{
    [Fact]
    public void CameraSystem_ComputesViewMatrixFromTransform()
    {
        var world = new KiloWorld();
        var system = new CameraSystem();

        // Add required resources
        world.AddResource(new RenderSettings { Width = 1280, Height = 720 });
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });
        world.AddResource(new RenderContext());

        // Create a camera entity at (0,0,5) looking toward origin (-Z)
        var entity = world.Entity();
        entity.Set(new Camera
        {
            FieldOfView = MathF.PI / 4, // 45 degrees
            NearPlane = 0.1f,
            FarPlane = 100f,
            IsActive = true
        });
        entity.Set(new LocalTransform
        {
            Position = new Vector3(0, 0, 5),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });

        // Run the camera system
        system.Update(world);

        // Verify matrices were computed (they should no longer be identity)
        ref var camera = ref world.Get<Camera>(entity.Id);
        Assert.NotEqual(Matrix4x4.Identity, camera.ViewMatrix);
        Assert.NotEqual(Matrix4x4.Identity, camera.ProjectionMatrix);
    }

    [Fact]
    public void CameraSystem_ProjectionMatrixUsesAspectRatio()
    {
        var world = new KiloWorld();
        var system = new CameraSystem();

        // Set 2:1 aspect ratio
        world.AddResource(new RenderSettings { Width = 1280, Height = 640 });
        world.AddResource(new WindowSize { Width = 1280, Height = 640 });
        world.AddResource(new RenderContext());

        var entity = world.Entity();
        entity.Set(new Camera
        {
            FieldOfView = MathF.PI / 4,
            NearPlane = 0.1f,
            FarPlane = 100f
        });
        entity.Set(LocalTransform.Identity);

        system.Update(world);
        ref var camera1 = ref world.Get<Camera>(entity.Id);
        var projection1 = camera1.ProjectionMatrix;

        // Update WindowSize resource for 16:9 aspect ratio
        var windowSize = world.GetResource<WindowSize>();
        windowSize.Height = 720;
        world.AddResource(windowSize); // Re-add to update

        system.Update(world);
        ref var camera2 = ref world.Get<Camera>(entity.Id);
        var projection2 = camera2.ProjectionMatrix;

        // Projections should differ due to aspect ratio
        Assert.NotEqual(projection1, projection2);
    }
}
