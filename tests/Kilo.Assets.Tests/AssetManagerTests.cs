using Kilo.Assets;
using Xunit;

namespace Kilo.Assets.Tests;

public class AssetManagerTests
{
    [Fact]
    public void Register_ReturnsValidHandle()
    {
        // Arrange
        var manager = new AssetManager();

        // Act
        var handle = manager.Register<string>("test/path.txt");

        // Assert
        Assert.True(handle.IsValid);
        Assert.Equal(1, handle.Id); // First registered asset gets ID 1
    }

    [Fact]
    public void Register_SamePathTwice_ReturnsSameId()
    {
        // Arrange
        var manager = new AssetManager();

        // Act
        var handle1 = manager.Register<string>("same/path.txt");
        var handle2 = manager.Register<string>("same/path.txt");

        // Assert
        Assert.Equal(handle1.Id, handle2.Id);
    }

    [Fact]
    public void Register_DifferentPaths_ReturnsDifferentIds()
    {
        // Arrange
        var manager = new AssetManager();

        // Act
        var handle1 = manager.Register<string>("path1.txt");
        var handle2 = manager.Register<string>("path2.txt");

        // Assert
        Assert.NotEqual(handle1.Id, handle2.Id);
    }

    [Fact]
    public void StoreAndGet_Roundtrip_Succeeds()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = manager.Register<string>("test.txt");
        var asset = "Hello, World!";

        // Act
        manager.Store(handle, asset);
        var retrieved = manager.Get(handle);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(asset, retrieved);
    }

    [Fact]
    public void Get_UnloadedAsset_ReturnsNull()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = manager.Register<string>("test.txt");

        // Act
        var retrieved = manager.Get(handle);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void Get_InvalidHandle_ReturnsNull()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = AssetHandle<string>.Invalid;

        // Act
        var retrieved = manager.Get(handle);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void IsLoaded_WhenAssetStored_ReturnsTrue()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = manager.Register<string>("test.txt");
        manager.Store(handle, "data");

        // Act
        var isLoaded = manager.IsLoaded(handle);

        // Assert
        Assert.True(isLoaded);
    }

    [Fact]
    public void IsLoaded_WhenAssetNotStored_ReturnsFalse()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = manager.Register<string>("test.txt");

        // Act
        var isLoaded = manager.IsLoaded(handle);

        // Assert
        Assert.False(isLoaded);
    }

    [Fact]
    public void TryGet_WhenAssetExists_ReturnsTrueAndAsset()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = manager.Register<string>("test.txt");
        var asset = "test data";
        manager.Store(handle, asset);

        // Act
        var result = manager.TryGet(handle, out var retrieved);

        // Assert
        Assert.True(result);
        Assert.NotNull(retrieved);
        Assert.Equal(asset, retrieved);
    }

    [Fact]
    public void TryGet_WhenAssetNotExists_ReturnsFalseAndNull()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = manager.Register<string>("test.txt");

        // Act
        var result = manager.TryGet(handle, out var retrieved);

        // Assert
        Assert.False(result);
        Assert.Null(retrieved);
    }

    [Fact]
    public void Clear_RemovesAllAssets()
    {
        // Arrange
        var manager = new AssetManager();
        var handle1 = manager.Register<string>("test1.txt");
        var handle2 = manager.Register<string>("test2.txt");
        manager.Store(handle1, "data1");
        manager.Store(handle2, "data2");

        // Act
        manager.Clear();

        // Assert
        Assert.Null(manager.Get(handle1));
        Assert.Null(manager.Get(handle2));
        Assert.Equal(0, manager.LoadedCount);
    }

    [Fact]
    public void LoadedCount_TracksNumberOfLoadedAssets()
    {
        // Arrange
        var manager = new AssetManager();

        // Assert initial state
        Assert.Equal(0, manager.LoadedCount);

        // Act
        var handle1 = manager.Register<string>("test1.txt");
        manager.Store(handle1, "data1");
        Assert.Equal(1, manager.LoadedCount);

        // Act
        var handle2 = manager.Register<string>("test2.txt");
        manager.Store(handle2, "data2");
        Assert.Equal(2, manager.LoadedCount);
    }

    [Fact]
    public void Store_OverwritingExistingAsset_ReplacesIt()
    {
        // Arrange
        var manager = new AssetManager();
        var handle = manager.Register<string>("test.txt");
        manager.Store(handle, "original");

        // Act
        manager.Store(handle, "replaced");
        var retrieved = manager.Get(handle);

        // Assert
        Assert.Equal("replaced", retrieved);
        Assert.Equal(1, manager.LoadedCount);
    }
}
