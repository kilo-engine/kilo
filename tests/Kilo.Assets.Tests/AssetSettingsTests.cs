using Kilo.Assets;
using Xunit;

namespace Kilo.Assets.Tests;

public class AssetSettingsTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var settings = new AssetSettings();

        // Assert
        Assert.Equal("assets", settings.RootPath);
        Assert.False(settings.EnableHotReload);
    }

    [Fact]
    public void ParameterizedConstructor_SetsCustomValues()
    {
        // Arrange & Act
        var settings = new AssetSettings("custom/path", true);

        // Assert
        Assert.Equal("custom/path", settings.RootPath);
        Assert.True(settings.EnableHotReload);
    }

    [Fact]
    public void ParameterizedConstructor_WithOnlyPath_SetsHotReloadToFalse()
    {
        // Arrange & Act
        var settings = new AssetSettings("another/path");

        // Assert
        Assert.Equal("another/path", settings.RootPath);
        Assert.False(settings.EnableHotReload);
    }

    [Fact]
    public void SetRootPath_ModifiesValue()
    {
        // Arrange
        var settings = new AssetSettings();

        // Act
        settings.RootPath = "new/path/to/assets";

        // Assert
        Assert.Equal("new/path/to/assets", settings.RootPath);
    }

    [Fact]
    public void SetEnableHotReload_ModifiesValue()
    {
        // Arrange
        var settings = new AssetSettings();

        // Act
        settings.EnableHotReload = true;

        // Assert
        Assert.True(settings.EnableHotReload);
    }

    [Fact]
    public void IsSealedClass_CannotBeInherited()
    {
        // Assert - this is a compile-time check, but we verify the type at runtime
        var type = typeof(AssetSettings);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
    }
}
