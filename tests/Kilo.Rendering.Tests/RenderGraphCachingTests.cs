using Xunit;

namespace Kilo.Rendering.Tests;

public class RenderGraphCachingTests
{
    [Fact]
    public void Compile_TwiceWithIdenticalStructure_ReusesCachedResult()
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

        // First compile should actually compile
        graph.Compile(driver);
        var firstCompileTextureCount = driver.CreateTextureCallCount;

        // Second compile with identical structure should skip recompilation
        graph.Compile(driver);
        var secondCompileTextureCount = driver.CreateTextureCallCount;

        // No new textures should have been created on the second compile
        Assert.Equal(firstCompileTextureCount, secondCompileTextureCount);
    }
}
