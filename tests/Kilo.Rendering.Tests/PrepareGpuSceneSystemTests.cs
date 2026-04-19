using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Xunit;

namespace Kilo.Rendering.Tests;

public sealed class RecordingBuffer : IBuffer
{
    public nuint Size { get; init; }
    public byte[] Data { get; } = [];

    public void UploadData<T>(ReadOnlySpan<T> data, nuint offset = 0) where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(data);
        var newData = new byte[Math.Max((int)(offset + (nuint)bytes.Length), Data.Length)];
        Data.CopyTo(newData, 0);
        bytes.CopyTo(newData.AsSpan((int)offset));
        // reflection-free replacement isn't trivial; we just keep latest upload in a field
        LastUpload = newData;
    }

    public byte[] LastUpload { get; private set; } = [];
    public void Dispose() { }
}

public class PrepareGpuSceneSystemTests
{
    [Fact]
    public void CameraBuffer_IsPopulatedWithViewAndProjection()
    {
        var world = new KiloWorld();
        world.AddResource(new RenderSettings { Width = 1280, Height = 720 });
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });
        world.AddResource(new RenderContext());

        var cameraBuffer = new RecordingBuffer { Size = (nuint)CameraData.Size };
        var objectBuffer = new RecordingBuffer { Size = (nuint)(256 * 4) };
        var lightBuffer = new RecordingBuffer { Size = 1024 };
        world.AddResource(new GpuSceneData
        {
            CameraBuffer = cameraBuffer,
            ObjectDataBuffer = objectBuffer,
            LightBuffer = lightBuffer,
        });

        var entity = world.Entity();
        entity.Set(new Camera
        {
            FieldOfView = MathF.PI / 4,
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

        // Run camera system first to compute matrices
        new CameraSystem().Update(world);
        new PrepareGpuSceneSystem().Update(world);

        Assert.Equal(CameraData.Size, cameraBuffer.LastUpload.Length);
        var cameraData = MemoryMarshal.Read<CameraData>(cameraBuffer.LastUpload.AsSpan());
        Assert.NotEqual(Matrix4x4.Identity, cameraData.View);
        Assert.NotEqual(Matrix4x4.Identity, cameraData.Projection);
        Assert.Equal(new Vector3(0, 0, 5), cameraData.Position);
    }

    [Fact]
    public void LightBuffer_IsPopulatedWithCorrectCounts()
    {
        var world = new KiloWorld();
        world.AddResource(new RenderSettings { Width = 1280, Height = 720 });
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });
        world.AddResource(new RenderContext());

        var cameraBuffer = new RecordingBuffer { Size = (nuint)CameraData.Size };
        var objectBuffer = new RecordingBuffer { Size = (nuint)(256 * 4) };
        var lightBuffer = new RecordingBuffer { Size = 4096 };
        world.AddResource(new GpuSceneData
        {
            CameraBuffer = cameraBuffer,
            ObjectDataBuffer = objectBuffer,
            LightBuffer = lightBuffer,
        });

        world.Entity().Set(new DirectionalLight
        {
            Direction = new Vector3(0, -1, 0),
            Color = new Vector3(1, 1, 1),
            Intensity = 1.0f
        });

        world.Entity().Set(new PointLight
        {
            Position = new Vector3(1, 2, 3),
            Color = new Vector3(1, 0, 0),
            Intensity = 2.0f,
            Range = 10.0f
        });

        new PrepareGpuSceneSystem().Update(world);

        var scene = world.GetResource<GpuSceneData>();
        Assert.Equal(2, scene.LightCount);
        Assert.True(lightBuffer.LastUpload.Length >= Marshal.SizeOf<LightData>() * 2);

        var light0 = MemoryMarshal.Read<LightData>(lightBuffer.LastUpload.AsSpan());
        Assert.Equal(new Vector3(0, -1, 0), light0.DirectionOrPosition);
        Assert.Equal(0, light0.LightType);

        var light1 = MemoryMarshal.Read<LightData>(lightBuffer.LastUpload.AsSpan(Marshal.SizeOf<LightData>()));
        Assert.Equal(new Vector3(1, 2, 3), light1.DirectionOrPosition);
        Assert.Equal(1, light1.LightType);
        Assert.Equal(10.0f, light1.Range);
    }

    [Fact]
    public void ObjectDataBuffer_PopulatedAndAlignedTo256()
    {
        var world = new KiloWorld();
        world.AddResource(new RenderSettings { Width = 1280, Height = 720 });
        world.AddResource(new WindowSize { Width = 1280, Height = 720 });
        world.AddResource(new RenderContext());

        var cameraBuffer = new RecordingBuffer { Size = (nuint)CameraData.Size };
        var objectBuffer = new RecordingBuffer { Size = (nuint)(256 * 4) };
        var lightBuffer = new RecordingBuffer { Size = 1024 };
        world.AddResource(new GpuSceneData
        {
            CameraBuffer = cameraBuffer,
            ObjectDataBuffer = objectBuffer,
            LightBuffer = lightBuffer,
        });

        var e1 = world.Entity();
        e1.Set(new LocalTransform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One });
        e1.Set(new LocalToWorld { Value = Matrix4x4.CreateTranslation(1, 2, 3) });
        e1.Set(new MeshRenderer { MeshHandle = 0, MaterialHandle = 1 });

        var e2 = world.Entity();
        e2.Set(new LocalTransform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One });
        e2.Set(new LocalToWorld { Value = Matrix4x4.CreateTranslation(4, 5, 6) });
        e2.Set(new MeshRenderer { MeshHandle = 0, MaterialHandle = 2 });

        new PrepareGpuSceneSystem().Update(world);

        var scene = world.GetResource<GpuSceneData>();
        Assert.Equal(2, scene.DrawCount);
        Assert.Equal(2, scene.DrawData.Length);
        Assert.Equal(1, scene.DrawData[0].MaterialId);
        Assert.Equal(2, scene.DrawData[1].MaterialId);

        Assert.Equal(256 * 2, objectBuffer.LastUpload.Length);
        var obj0 = MemoryMarshal.Read<ObjectData>(objectBuffer.LastUpload.AsSpan());
        Assert.Equal(Matrix4x4.CreateTranslation(1, 2, 3), obj0.Model);
        Assert.Equal(1, obj0.MaterialId);

        var obj1 = MemoryMarshal.Read<ObjectData>(objectBuffer.LastUpload.AsSpan(256));
        Assert.Equal(Matrix4x4.CreateTranslation(4, 5, 6), obj1.Model);
        Assert.Equal(2, obj1.MaterialId);
    }
}
