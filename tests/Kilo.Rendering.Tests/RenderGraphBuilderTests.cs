using System.Numerics;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Xunit;

namespace Kilo.Rendering.Tests;

public class RenderGraphBuilderTests
{
    [Fact]
    public void ForwardOpaquePass_DeclaresCorrectAttachmentsAndReads()
    {
        var driver = new MockRenderDriver();
        var builder = new RenderGraphBuilder();
        var graph = builder.GetGraph();

        // Simulate the pass setup from RenderSystem
        builder.AddRenderPass("ForwardOpaque", setup: pass =>
        {
            var depth = pass.CreateTexture(new TextureDescriptor
            {
                Width = 1280,
                Height = 720,
                Format = DriverPixelFormat.Depth24Plus,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(depth);
            pass.DepthStencilAttachment(depth, DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);

            var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
            {
                Width = 1280,
                Height = 720,
                Format = DriverPixelFormat.BGRA8Unorm,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store, clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));

            var cameraBufferHandle = pass.ImportBuffer("CameraBuffer", new BufferDescriptor
            {
                Size = 256,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });
            var objectBufferHandle = pass.ImportBuffer("ObjectDataBuffer", new BufferDescriptor
            {
                Size = 1024,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });
            var lightBufferHandle = pass.ImportBuffer("LightBuffer", new BufferDescriptor
            {
                Size = 1024,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });

            pass.ReadBuffer(cameraBufferHandle);
            pass.ReadBuffer(objectBufferHandle);
            pass.ReadBuffer(lightBufferHandle);
        }, execute: _ => { });

        graph.Compile(driver);

        var passesField = graph.GetType().GetField("_passes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var passes = passesField!.GetValue(graph) as System.Collections.Generic.List<RenderPass>;
        Assert.NotNull(passes);
        Assert.Single(passes);

        var forwardPass = passes[0];
        Assert.Equal("ForwardOpaque", forwardPass.Name);

        var colorAttachmentsField = typeof(RenderPass).GetField("ColorAttachments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var depthStencilField = typeof(RenderPass).GetField("DepthStencilAttachment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var readResourcesField = typeof(RenderPass).GetField("ReadResources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var colorAttachments = colorAttachmentsField!.GetValue(forwardPass) as System.Collections.Generic.List<ColorAttachmentConfig>;
        var depthStencil = depthStencilField!.GetValue(forwardPass);
        var readResources = readResourcesField!.GetValue(forwardPass) as System.Collections.Generic.List<RenderResourceHandle>;

        Assert.NotNull(colorAttachments);
        Assert.Single(colorAttachments);
        Assert.NotNull(depthStencil);
        Assert.NotNull(readResources);
        Assert.Equal(3, readResources.Count);
    }
}
