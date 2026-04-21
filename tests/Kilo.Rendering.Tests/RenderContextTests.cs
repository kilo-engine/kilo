using Xunit;

namespace Kilo.Rendering.Tests;

public class RenderContextTests
{
    [Fact]
    public void RenderContext_HasCorrectDefaults()
    {
        var context = new RenderContext();
        Assert.Null(context.Driver);
        Assert.False(context.WindowResized);
        Assert.NotNull(context.ShaderCache);
        Assert.NotNull(context.PipelineCache);
        Assert.NotNull(context.MaterialManager);
        Assert.NotNull(context.RenderGraph);
    }
}
