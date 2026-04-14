using Xunit;

namespace Kilo.Rendering.Tests;

public class ComputePipelineTests
{
    private const string ComputeShaderSource = """
        @compute @workgroup_size(1)
        fn main() {}
        """;

    [Fact]
    public void CreateComputeShaderModule_Succeeds()
    {
        var driver = new MockRenderDriver();
        var shader = driver.CreateComputeShaderModule(ComputeShaderSource, "main");

        Assert.NotNull(shader);
        Assert.Equal("main", shader.EntryPoint);
        Assert.Equal(1, driver.CreateComputeShaderModuleCallCount);
    }

    [Fact]
    public void CreateComputePipeline_Succeeds()
    {
        var driver = new MockRenderDriver();
        var shader = driver.CreateComputeShaderModule(ComputeShaderSource, "main");
        var pipeline = driver.CreateComputePipeline(shader, "main");

        Assert.NotNull(pipeline);
        Assert.Equal(1, driver.CreateComputePipelineCallCount);
    }
}
