using System.Numerics;
using Xunit;

namespace Kilo.Rendering.Tests;

public class ComponentTests
{
    [Fact]
    public void LocalTransform_Identity_HasCorrectValues()
    {
        var identity = LocalTransform.Identity;
        Assert.Equal(Vector3.Zero, identity.Position);
        Assert.Equal(Quaternion.Identity, identity.Rotation);
        Assert.Equal(Vector3.One, identity.Scale);
    }

    [Fact]
    public void LocalToWorld_DefaultsToIdentity()
    {
        var localToWorld = new LocalToWorld();
        Assert.Equal(Matrix4x4.Identity, localToWorld.Value);
    }

    [Fact]
    public void Camera_HasCorrectDefaults()
    {
        var camera = new Camera();
        Assert.Equal(default(Matrix4x4), camera.ViewMatrix);
        Assert.Equal(default(Matrix4x4), camera.ProjectionMatrix);
        Assert.Equal(0f, camera.FieldOfView);
        Assert.Equal(0f, camera.NearPlane);
        Assert.Equal(0f, camera.FarPlane);
        Assert.False(camera.IsActive);
    }

    [Fact]
    public void MeshRenderer_HasCorrectDefaults()
    {
        var renderer = new MeshRenderer();
        Assert.Equal(0, renderer.MeshHandle);
        Assert.Equal(0, renderer.MaterialHandle);
    }

    [Fact]
    public void Sprite_HasCorrectDefaults()
    {
        var sprite = new Sprite();
        Assert.Equal(Vector4.Zero, sprite.Tint);
        Assert.Equal(Vector2.Zero, sprite.Size);
        Assert.Equal(0, sprite.TextureHandle);
        Assert.Equal(0f, sprite.ZIndex);
    }

    [Fact]
    public void PointLight_HasCorrectDefaults()
    {
        var light = new PointLight();
        Assert.Equal(Vector3.Zero, light.Position);
        Assert.Equal(Vector3.Zero, light.Color);
        Assert.Equal(0f, light.Intensity);
        Assert.Equal(0f, light.Range);
    }

    [Fact]
    public void DirectionalLight_HasCorrectDefaults()
    {
        var light = new DirectionalLight();
        Assert.Equal(Vector3.Zero, light.Direction);
        Assert.Equal(Vector3.Zero, light.Color);
        Assert.Equal(0f, light.Intensity);
    }

    [Fact]
    public void WindowSize_HasCorrectDefaults()
    {
        var windowSize = new WindowSize();
        Assert.Equal(0, windowSize.Width);
        Assert.Equal(0, windowSize.Height);
    }
}
