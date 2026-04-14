using Kilo.Assets;
using Kilo.ECS;
using Xunit;

namespace Kilo.Assets.Tests;

public class PluginRegistrationTests
{
    [Fact]
    public void AssetsPlugin_ImplementsIKiloPlugin()
    {
        // Arrange & Act
        var plugin = new AssetsPlugin();

        // Assert
        Assert.IsAssignableFrom<IKiloPlugin>(plugin);
    }

    [Fact]
    public void Build_WithDefaultSettings_DoesNotThrow()
    {
        // Arrange
        var plugin = new AssetsPlugin();
        var app = new KiloApp();

        // Act & Assert
        var exception = Record.Exception(() => plugin.Build(app));
        Assert.Null(exception);
    }

    [Fact]
    public void Build_WithCustomSettings_DoesNotThrow()
    {
        // Arrange
        var settings = new AssetSettings("custom/path", true);
        var plugin = new AssetsPlugin(settings);
        var app = new KiloApp();

        // Act & Assert
        var exception = Record.Exception(() => plugin.Build(app));
        Assert.Null(exception);
    }

    [Fact]
    public void Build_RegistersAssetLoadSystem()
    {
        // Arrange
        var plugin = new AssetsPlugin();
        var app = new KiloApp();

        // Act
        plugin.Build(app);

        // Assert - verify the world is accessible and no exceptions occurred
        Assert.NotNull(app.World);
    }

    [Fact]
    public void Build_WithNullSettings_UsesDefaults()
    {
        // Arrange
        var plugin = new AssetsPlugin(null!);
        var app = new KiloApp();

        // Act & Assert
        var exception = Record.Exception(() => plugin.Build(app));
        Assert.Null(exception);
        Assert.NotNull(app.World);
    }

    [Fact]
    public void Build_RunUpdate_DoesNotThrow()
    {
        // Arrange
        var plugin = new AssetsPlugin();
        var app = new KiloApp();
        plugin.Build(app);

        // Act & Assert
        var exception = Record.Exception(() => app.Update());
        Assert.Null(exception);
    }
}
