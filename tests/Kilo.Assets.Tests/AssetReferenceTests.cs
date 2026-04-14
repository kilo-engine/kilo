using Kilo.Assets;
using Xunit;

namespace Kilo.Assets.Tests;

public class AssetReferenceTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaults()
    {
        // Arrange & Act
        var reference = new AssetReference();

        // Assert
        Assert.Equal(0, reference.AssetId);
        Assert.Equal(string.Empty, reference.Path);
        Assert.False(reference.IsLoaded);
    }

    [Fact]
    public void ParameterizedConstructor_SetsValues()
    {
        // Arrange & Act
        var reference = new AssetReference(42, "textures/player.png", true);

        // Assert
        Assert.Equal(42, reference.AssetId);
        Assert.Equal("textures/player.png", reference.Path);
        Assert.True(reference.IsLoaded);
    }

    [Fact]
    public void ParameterizedConstructor_WithIsLoadedFalse_SetsValues()
    {
        // Arrange & Act
        var reference = new AssetReference(10, "models/enemy.fbx", false);

        // Assert
        Assert.Equal(10, reference.AssetId);
        Assert.Equal("models/enemy.fbx", reference.Path);
        Assert.False(reference.IsLoaded);
    }

    [Fact]
    public void SetProperties_ModifyValues()
    {
        // Arrange
        var reference = new AssetReference();

        // Act
        reference.AssetId = 99;
        reference.Path = "audio/music.mp3";
        reference.IsLoaded = true;

        // Assert
        Assert.Equal(99, reference.AssetId);
        Assert.Equal("audio/music.mp3", reference.Path);
        Assert.True(reference.IsLoaded);
    }

    [Fact]
    public void StructCopy_CopiesValues()
    {
        // Arrange
        var original = new AssetReference(5, "test.txt", true);

        // Act
        var copy = original;

        // Assert
        Assert.Equal(original.AssetId, copy.AssetId);
        Assert.Equal(original.Path, copy.Path);
        Assert.Equal(original.IsLoaded, copy.IsLoaded);

        // Modify copy - should not affect original (struct behavior)
        copy.AssetId = 100;
        Assert.Equal(5, original.AssetId);
        Assert.Equal(100, copy.AssetId);
    }
}
