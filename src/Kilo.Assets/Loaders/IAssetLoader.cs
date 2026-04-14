namespace Kilo.Assets;

/// <summary>
/// Interface for asset loaders that can load specific asset types from files.
/// Implementations handle the actual loading logic for different asset types.
/// </summary>
/// <typeparam name="T">The type of asset this loader handles.</typeparam>
public interface IAssetLoader<T> where T : class
{
    /// <summary>
    /// Loads an asset from the specified file path.
    /// </summary>
    /// <param name="path">The path to the asset file.</param>
    /// <returns>The loaded asset.</returns>
    T Load(string path);

    /// <summary>
    /// Gets the file extensions this loader supports.
    /// </summary>
    string[] SupportedExtensions { get; }
}
