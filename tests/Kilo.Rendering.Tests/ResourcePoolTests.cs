using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Xunit;

namespace Kilo.Rendering.Tests;

public class ResourcePoolTests
{
    [Fact]
    public void GetTexture_CreatesNewTexture()
    {
        var driver = new MockRenderDriver();
        var pool = new RenderGraphResourcePool();
        var desc = new TextureDescriptor
        {
            Width = 128,
            Height = 128,
            Format = DriverPixelFormat.RGBA8Unorm,
        };

        var tex = pool.GetTexture(driver, desc);

        Assert.NotNull(tex);
        Assert.Equal(1, driver.CreateTextureCallCount);
    }

    [Fact]
    public void GetTexture_AfterReturn_ReusesSameInstance()
    {
        var driver = new MockRenderDriver();
        var pool = new RenderGraphResourcePool();
        var desc = new TextureDescriptor
        {
            Width = 128,
            Height = 128,
            Format = DriverPixelFormat.RGBA8Unorm,
        };

        var tex1 = pool.GetTexture(driver, desc);
        pool.ReturnTexture(tex1, desc);
        var tex2 = pool.GetTexture(driver, desc);

        Assert.Same(tex1, tex2);
        Assert.Equal(1, driver.CreateTextureCallCount);
    }

    [Fact]
    public void GetTexture_DifferentDimensions_CreatesNewTexture()
    {
        var driver = new MockRenderDriver();
        var pool = new RenderGraphResourcePool();
        var desc1 = new TextureDescriptor
        {
            Width = 128,
            Height = 128,
            Format = DriverPixelFormat.RGBA8Unorm,
        };
        var desc2 = new TextureDescriptor
        {
            Width = 256,
            Height = 256,
            Format = DriverPixelFormat.RGBA8Unorm,
        };

        var tex1 = pool.GetTexture(driver, desc1);
        pool.ReturnTexture(tex1, desc1);
        var tex2 = pool.GetTexture(driver, desc2);

        Assert.NotSame(tex1, tex2);
        Assert.Equal(2, driver.CreateTextureCallCount);
    }

    [Fact]
    public void GetBuffer_AfterReturn_ReusesSameInstance()
    {
        var driver = new MockRenderDriver();
        var pool = new RenderGraphResourcePool();
        var desc = new BufferDescriptor
        {
            Size = 256,
            Usage = BufferUsage.Uniform,
        };

        var buf1 = pool.GetBuffer(driver, desc);
        pool.ReturnBuffer(buf1, desc);
        var buf2 = pool.GetBuffer(driver, desc);

        Assert.Same(buf1, buf2);
    }

    [Fact]
    public void InvalidateForSize_RemovesMismatchedTextures()
    {
        var driver = new MockRenderDriver();
        var pool = new RenderGraphResourcePool();
        var desc = new TextureDescriptor
        {
            Width = 128,
            Height = 128,
            Format = DriverPixelFormat.RGBA8Unorm,
        };

        var tex1 = pool.GetTexture(driver, desc);
        pool.ReturnTexture(tex1, desc);
        pool.InvalidateForSize(256, 256);
        var tex2 = pool.GetTexture(driver, desc);

        Assert.NotSame(tex1, tex2);
        Assert.Equal(2, driver.CreateTextureCallCount);
    }
}
