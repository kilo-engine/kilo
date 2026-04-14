namespace Kilo.Assets;

/// <summary>
/// Component that holds a reference to an asset.
/// Can be attached to entities to associate them with specific loaded assets.
/// </summary>
public struct AssetReference
{
    /// <summary>
    /// The unique ID of the referenced asset.
    /// </summary>
    public int AssetId;

    /// <summary>
    /// The path to the asset file.
    /// </summary>
    public string Path;

    /// <summary>
    /// Whether the asset has been loaded into memory.
    /// </summary>
    public bool IsLoaded;

    /// <summary>
    /// Creates a new asset reference with default values.
    /// </summary>
    public AssetReference()
    {
        AssetId = 0;
        Path = string.Empty;
        IsLoaded = false;
    }

    /// <summary>
    /// Creates a new asset reference with the specified values.
    /// </summary>
    /// <param name="assetId">The asset ID.</param>
    /// <param name="path">The asset path.</param>
    /// <param name="isLoaded">Whether the asset is loaded.</param>
    public AssetReference(int assetId, string path, bool isLoaded = false)
    {
        AssetId = assetId;
        Path = path;
        IsLoaded = isLoaded;
    }
}
