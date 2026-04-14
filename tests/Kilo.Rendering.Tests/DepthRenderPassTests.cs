using System.Numerics;
using Kilo.Rendering.Driver;
using Xunit;

namespace Kilo.Rendering.Tests;

public class DepthRenderPassTests
{
    [Fact]
    public void CreateRenderPassDescriptor_WithDepthAttachment_DoesNotThrow()
    {
        var driver = new MockRenderDriver();
        var colorTexture = driver.CreateTexture(new RenderGraph.TextureDescriptor { Width = 128, Height = 128 });
        var colorView = driver.CreateTextureView(colorTexture, new TextureViewDescriptor { Format = DriverPixelFormat.BGRA8Unorm });
        var depthTexture = driver.CreateTexture(new RenderGraph.TextureDescriptor
        {
            Width = 128,
            Height = 128,
            Format = DriverPixelFormat.Depth24Plus,
        });
        var depthView = driver.CreateTextureView(depthTexture, new TextureViewDescriptor { Format = DriverPixelFormat.Depth24Plus });

        var descriptor = new RenderPassDescriptor
        {
            ColorAttachments =
            [
                new ColorAttachmentDescriptor
                {
                    RenderTarget = colorView,
                    LoadAction = DriverLoadAction.Clear,
                    StoreAction = DriverStoreAction.Store,
                    ClearColor = new Vector4(0, 0, 0, 1),
                }
            ],
            DepthStencilAttachment = new DepthStencilAttachmentDescriptor
            {
                View = depthView,
                DepthLoadAction = DriverLoadAction.Clear,
                DepthStoreAction = DriverStoreAction.Store,
                ClearDepth = 1.0f,
            }
        };

        Assert.NotNull(descriptor);
        Assert.Single(descriptor.ColorAttachments);
        Assert.NotNull(descriptor.DepthStencilAttachment);
        Assert.Equal(1.0f, descriptor.DepthStencilAttachment.ClearDepth);
    }

    [Fact]
    public void BeginRenderPass_WithDepthAttachment_DoesNotThrow()
    {
        var driver = new MockRenderDriver();
        using var encoder = driver.BeginCommandEncoding();
        var colorTexture = driver.CreateTexture(new RenderGraph.TextureDescriptor { Width = 128, Height = 128 });
        var colorView = driver.CreateTextureView(colorTexture, new TextureViewDescriptor { Format = DriverPixelFormat.BGRA8Unorm });
        var depthTexture = driver.CreateTexture(new RenderGraph.TextureDescriptor
        {
            Width = 128,
            Height = 128,
            Format = DriverPixelFormat.Depth24Plus,
        });
        var depthView = driver.CreateTextureView(depthTexture, new TextureViewDescriptor { Format = DriverPixelFormat.Depth24Plus });

        var descriptor = new RenderPassDescriptor
        {
            ColorAttachments =
            [
                new ColorAttachmentDescriptor
                {
                    RenderTarget = colorView,
                    LoadAction = DriverLoadAction.Clear,
                    StoreAction = DriverStoreAction.Store,
                    ClearColor = new Vector4(0, 0, 0, 1),
                }
            ],
            DepthStencilAttachment = new DepthStencilAttachmentDescriptor
            {
                View = depthView,
                DepthLoadAction = DriverLoadAction.Clear,
                DepthStoreAction = DriverStoreAction.Store,
                ClearDepth = 1.0f,
            }
        };

        var exception = Record.Exception(() => encoder.BeginRenderPass(descriptor));
        Assert.Null(exception);
    }
}
