using Xunit;

namespace Kilo.Rendering.Tests;

public class RenderContextTests
{
    [Fact]
    public void RenderContext_HasCorrectDefaults()
    {
        var context = new RenderContext();
        Assert.Null(context.Driver);
        Assert.Null(context.Sprite.Pipeline);
        Assert.Null(context.Sprite.QuadVertexBuffer);
        Assert.Null(context.Sprite.QuadIndexBuffer);
        Assert.Null(context.Sprite.UniformBuffer);
        Assert.Null(context.Sprite.BindingSet);
        Assert.False(context.WindowResized);
    }
}
