using Xunit;

namespace Kilo.Rendering.Tests;

public class RenderContextTests
{
    [Fact]
    public void RenderContext_HasCorrectDefaults()
    {
        var context = new RenderContext();
        Assert.Null(context.Driver);
        Assert.Null(context.SpritePipeline);
        Assert.Null(context.QuadVertexBuffer);
        Assert.Null(context.QuadIndexBuffer);
        Assert.Null(context.UniformBuffer);
        Assert.Null(context.BindingSet);
        Assert.False(context.WindowResized);
    }
}
