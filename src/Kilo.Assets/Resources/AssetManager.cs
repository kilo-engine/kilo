using System.Collections.Concurrent;

namespace Kilo.Assets;

/// <summary>
/// Central manager for loading, caching, and storing assets.
/// Maintains mappings from paths to IDs and stores loaded asset objects.
/// </summary>
public sealed class AssetManager
{
    private readonly ConcurrentDictionary<int, object> _assets = new();
    private readonly ConcurrentDictionary<string, int> _cache = new();
    private int _nextId;

    /// <summary>
    /// Registers a new asset handle for the given path without loading it.
    /// </summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="path">The path to the asset.</param>
    /// <returns>A new asset handle.</returns>
    public AssetHandle<T> Register<T>(string path) where T : class
    {
        if (_cache.TryGetValue(path, out var existingId))
        {
            return new AssetHandle<T>(existingId);
        }

        var id = System.Threading.Interlocked.Increment(ref _nextId);
        _cache[path] = id;
        return new AssetHandle<T>(id);
    }

    /// <summary>
    /// Retrieves a loaded asset by its handle.
    /// </summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="handle">The asset handle.</param>
    /// <returns>The loaded asset, or null if not found.</returns>
    public T? Get<T>(AssetHandle<T> handle) where T : class
    {
        return _assets.TryGetValue(handle.Id, out var asset) ? asset as T : null;
    }

    /// <summary>
    /// Stores a loaded asset for the given handle.
    /// </summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="handle">The asset handle.</param>
    /// <param name="asset">The loaded asset.</param>
    public void Store<T>(AssetHandle<T> handle, T asset) where T : class
    {
        _assets[handle.Id] = asset;
    }

    /// <summary>
    /// Checks if an asset has been loaded.
    /// </summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="handle">The asset handle.</param>
    /// <returns>True if the asset is loaded, false otherwise.</returns>
    public bool IsLoaded<T>(AssetHandle<T> handle) where T : class
    {
        return _assets.ContainsKey(handle.Id);
    }

    /// <summary>
    /// Attempts to retrieve a loaded asset by its handle.
    /// </summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="handle">The asset handle.</param>
    /// <param name="asset">The loaded asset, if found.</param>
    /// <returns>True if the asset was found, false otherwise.</returns>
    public bool TryGet<T>(AssetHandle<T> handle, out T? asset) where T : class
    {
        if (_assets.TryGetValue(handle.Id, out var obj) && obj is T typed)
        {
            asset = typed;
            return true;
        }
        asset = null;
        return false;
    }

    /// <summary>
    /// Clears all cached assets and path mappings.
    /// </summary>
    public void Clear()
    {
        _assets.Clear();
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of currently loaded assets.
    /// </summary>
    public int LoadedCount => _assets.Count;
}
