using Kilo.ECS;
using Xunit;

namespace Kilo.Rendering.Tests;

public class PluginRegistrationTests
{
    [Fact]
    public void RenderingPlugin_ImplementsIKiloPlugin()
    {
        var plugin = new RenderingPlugin();
        Assert.IsAssignableFrom<IKiloPlugin>(plugin);
    }

    [Fact]
    public void RenderingPlugin_Build_DoesNotThrow()
    {
        var plugin = new RenderingPlugin();
        var app = new KiloApp();

        var exception = Record.Exception(() => plugin.Build(app));
        Assert.Null(exception);
    }

    [Fact]
    public void RenderingPlugin_Build_RegistersResources()
    {
        var plugin = new RenderingPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        var world = app.World;
        Assert.NotNull(world.GetResource<RenderSettings>());
        Assert.NotNull(world.GetResource<RenderContext>());
        // WindowSize is a struct, so we just verify it exists
        var windowSize = world.GetResource<WindowSize>();
        Assert.Equal(1280, windowSize.Width);
        Assert.Equal(720, windowSize.Height);
    }
}
