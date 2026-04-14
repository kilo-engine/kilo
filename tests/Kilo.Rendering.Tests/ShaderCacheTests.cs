using Kilo.Rendering.Resources;
using Xunit;

namespace Kilo.Rendering.Tests;

public class ShaderCacheTests
{
    [Fact]
    public void GetOrCreateShader_SameSourceAndEntryPoint_ReturnsSameModule()
    {
        var driver = new MockRenderDriver();
        var cache = new ShaderCache();

        var shader1 = cache.GetOrCreateShader(driver, "source_a", "main");
        var shader2 = cache.GetOrCreateShader(driver, "source_a", "main");

        Assert.Same(shader1, shader2);
    }

    [Fact]
    public void GetOrCreateShader_DifferentSource_ReturnsNewModule()
    {
        var driver = new MockRenderDriver();
        var cache = new ShaderCache();

        var shader1 = cache.GetOrCreateShader(driver, "source_a", "main");
        var shader2 = cache.GetOrCreateShader(driver, "source_b", "main");

        Assert.NotSame(shader1, shader2);
    }

    [Fact]
    public void GetOrCreateComputeShader_SameSourceAndEntryPoint_ReturnsSameModule()
    {
        var driver = new MockRenderDriver();
        var cache = new ShaderCache();

        var shader1 = cache.GetOrCreateComputeShader(driver, "compute_source", "main");
        var shader2 = cache.GetOrCreateComputeShader(driver, "compute_source", "main");

        Assert.Same(shader1, shader2);
    }
}
