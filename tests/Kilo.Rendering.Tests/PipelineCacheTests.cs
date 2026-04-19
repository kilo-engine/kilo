using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Xunit;

namespace Kilo.Rendering.Tests;

public class PipelineCacheTests
{
    [Fact]
    public void GetOrCreate_SameKey_ReturnsSamePipeline()
    {
        var driver = new MockRenderDriver();
        var cache = new PipelineCache();
        var key = new PipelineCacheKey
        {
            VertexShaderSource = "vs",
            VertexShaderEntryPoint = "main",
            FragmentShaderSource = "fs",
            FragmentShaderEntryPoint = "main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = [],
            ColorTargets = [],
            DepthStencil = null,
        };

        var pipeline1 = cache.GetOrCreate(driver, key, () => driver.CreateRenderPipeline(new RenderPipelineDescriptor()));
        var pipeline2 = cache.GetOrCreate(driver, key, () => driver.CreateRenderPipeline(new RenderPipelineDescriptor()));

        Assert.Same(pipeline1, pipeline2);
    }

    [Fact]
    public void GetOrCreate_DifferentKey_ReturnsNewPipeline()
    {
        var driver = new MockRenderDriver();
        var cache = new PipelineCache();
        var key1 = new PipelineCacheKey
        {
            VertexShaderSource = "vs1",
            VertexShaderEntryPoint = "main",
            FragmentShaderSource = "fs1",
            FragmentShaderEntryPoint = "main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = [],
            ColorTargets = [],
            DepthStencil = null,
        };
        var key2 = new PipelineCacheKey
        {
            VertexShaderSource = "vs2",
            VertexShaderEntryPoint = "main",
            FragmentShaderSource = "fs2",
            FragmentShaderEntryPoint = "main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = [],
            ColorTargets = [],
            DepthStencil = null,
        };

        var pipeline1 = cache.GetOrCreate(driver, key1, () => driver.CreateRenderPipeline(new RenderPipelineDescriptor()));
        var pipeline2 = cache.GetOrCreate(driver, key2, () => driver.CreateRenderPipeline(new RenderPipelineDescriptor()));

        Assert.NotSame(pipeline1, pipeline2);
    }
}
