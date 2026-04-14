using Xunit;

namespace Kilo.Rendering.Tests;

public class RenderGraphCompilerTests
{
    [Fact]
    public void Compile_TopologicalOrder_IsCorrect()
    {
        var driver = new MockRenderDriver();
        var graph = new RenderGraph.RenderGraph();

        RenderGraph.RenderResourceHandle textureA = default;

        graph.AddPass("Pass0", builder =>
        {
            textureA = builder.CreateTexture(new RenderGraph.TextureDescriptor { Width = 128, Height = 128 });
            builder.Write(textureA);
        }, _ => { });

        graph.AddPass("Pass1", builder =>
        {
            builder.Read(textureA);
            var backbuffer = builder.ImportTexture("Backbuffer", new RenderGraph.TextureDescriptor
            {
                Width = 128,
                Height = 128,
                Usage = RenderGraph.TextureUsage.RenderAttachment,
            });
            builder.Write(backbuffer);
        }, _ => { });

        graph.Compile(driver);

        var compiledPasses = graph.GetType()
            .GetField("_passes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(graph) as System.Collections.Generic.List<RenderGraph.RenderPass>;

        Assert.NotNull(compiledPasses);
        Assert.Equal(2, compiledPasses.Count);
        Assert.Equal("Pass0", compiledPasses[0].Name);
        Assert.Equal("Pass1", compiledPasses[1].Name);
    }

    [Fact]
    public void Compile_ComputeThenRender_TopologicalOrder_IsCorrect()
    {
        var driver = new MockRenderDriver();
        var graph = new RenderGraph.RenderGraph();

        RenderGraph.RenderResourceHandle bufferA = default;

        graph.AddComputePass("ComputePass", builder =>
        {
            bufferA = builder.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform });
            builder.WriteBuffer(bufferA);
        }, _ => { });

        graph.AddPass("RenderPass", builder =>
        {
            builder.ReadBuffer(bufferA);
            var backbuffer = builder.ImportTexture("Backbuffer", new RenderGraph.TextureDescriptor
            {
                Width = 128,
                Height = 128,
                Usage = RenderGraph.TextureUsage.RenderAttachment,
            });
            builder.WriteTexture(backbuffer);
        }, _ => { });

        graph.Compile(driver);

        var compiledPasses = graph.GetType()
            .GetField("_passes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(graph) as System.Collections.Generic.List<RenderGraph.RenderPass>;

        Assert.NotNull(compiledPasses);
        Assert.Equal(2, compiledPasses.Count);
        Assert.Equal("ComputePass", compiledPasses[0].Name);
        Assert.Equal("RenderPass", compiledPasses[1].Name);
    }
}
