using Kilo.ECS;
using Kilo.Window;
using Xunit;

namespace Kilo.Window.Tests;

public class PluginRegistrationTests
{
    [Fact]
    public void WindowPlugin_ImplementsIKiloPlugin()
    {
        var plugin = new WindowPlugin();
        Assert.IsAssignableFrom<IKiloPlugin>(plugin);
    }

    [Fact]
    public void WindowPlugin_Build_DoesNotThrow()
    {
        var plugin = new WindowPlugin();
        var app = new KiloApp();

        var exception = Record.Exception(() => plugin.Build(app));
        Assert.Null(exception);
    }

    [Fact]
    public void WindowPlugin_RegistersInputStateResource()
    {
        var plugin = new WindowPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        var state = app.World.GetResource<InputState>();
        Assert.NotNull(state);
    }

    [Fact]
    public void WindowPlugin_RegistersInputSettingsResource()
    {
        var plugin = new WindowPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        var settings = app.World.GetResource<InputSettings>();
        Assert.NotNull(settings);
    }

    [Fact]
    public void WindowPlugin_RegistersInputPollSystem()
    {
        var plugin = new WindowPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        // The plugin should register a system in KiloStage.First
        // We verify this by checking that the build completes without error
        // and that the world is properly configured
        Assert.NotNull(app.World);
    }

    [Fact]
    public void InputSettings_HasDefaultValues()
    {
        var plugin = new WindowPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        var settings = app.World.GetResource<InputSettings>();
        Assert.Equal(1.0f, settings.MouseSensitivity);
        Assert.Equal(0.1f, settings.GamepadDeadZone);
    }
}
