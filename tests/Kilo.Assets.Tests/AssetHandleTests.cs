using Kilo.Assets;
using Xunit;

namespace Kilo.Assets.Tests;

public class AssetHandleTests
{
    [Fact]
    public void Create_HandleWithPositiveId_IsValid()
    {
        // Arrange & Act
        var handle = new AssetHandle<string>(42);

        // Assert
        Assert.Equal(42, handle.Id);
        Assert.True(handle.IsValid);
    }

    [Fact]
    public void Create_HandleWithZeroId_IsValid()
    {
        // Arrange & Act
        var handle = new AssetHandle<object>(0);

        // Assert
        Assert.Equal(0, handle.Id);
        Assert.True(handle.IsValid);
    }

    [Fact]
    public void Create_HandleWithNegativeId_IsInvalid()
    {
        // Arrange & Act
        var handle = new AssetHandle<string>(-1);

        // Assert
        Assert.Equal(-1, handle.Id);
        Assert.False(handle.IsValid);
    }

    [Fact]
    public void Invalid_ReturnsHandleWithNegativeOne()
    {
        // Arrange & Act
        var handle = AssetHandle<string>.Invalid;

        // Assert
        Assert.Equal(-1, handle.Id);
        Assert.False(handle.IsValid);
    }

    [Fact]
    public void Equality_SameId_ReturnsTrue()
    {
        // Arrange
        var handle1 = new AssetHandle<string>(5);
        var handle2 = new AssetHandle<string>(5);

        // Act & Assert
        Assert.Equal(handle1, handle2);
        Assert.True(handle1 == handle2);
        Assert.False(handle1 != handle2);
    }

    [Fact]
    public void Equality_DifferentId_ReturnsFalse()
    {
        // Arrange
        var handle1 = new AssetHandle<string>(5);
        var handle2 = new AssetHandle<string>(10);

        // Act & Assert
        Assert.NotEqual(handle1, handle2);
        Assert.False(handle1 == handle2);
        Assert.True(handle1 != handle2);
    }

    [Fact]
    public void GetHashCode_SameId_ReturnsSameValue()
    {
        // Arrange
        var handle1 = new AssetHandle<object>(7);
        var handle2 = new AssetHandle<object>(7);

        // Act & Assert
        Assert.Equal(handle1.GetHashCode(), handle2.GetHashCode());
    }

    [Fact]
    public void DifferentTypes_SameId_HaveSameId()
    {
        // AssetHandle<T> is a struct, so each instantiation is a separate type
        // But the Id values can be compared

        // Arrange
        var handle1 = new AssetHandle<string>(3);
        var handle2 = new AssetHandle<object>(3);

        // Act & Assert
        Assert.Equal(handle1.Id, handle2.Id);
        Assert.True(handle1.IsValid);
        Assert.True(handle2.IsValid);
    }
}
