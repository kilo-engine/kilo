namespace Kilo.Assets;

/// <summary>
/// Configuration settings for the asset management system.
/// </summary>
public sealed class AssetSettings
{
    /// <summary>
    /// The root directory path where assets are located.
    /// </summary>
    public string RootPath { get; set; } = "assets";

    /// <summary>
    /// Whether to enable hot-reloading of assets during development.
    /// </summary>
    public bool EnableHotReload { get; set; } = false;

    /// <summary>
    /// Creates a new AssetSettings instance with default values.
    /// </summary>
    public AssetSettings()
    {
    }

    /// <summary>
    /// Creates a new AssetSettings instance with custom values.
    /// </summary>
    /// <param name="rootPath">The root directory path for assets.</param>
    /// <param name="enableHotReload">Whether to enable hot-reloading.</param>
    public AssetSettings(string rootPath, bool enableHotReload = false)
    {
        RootPath = rootPath;
        EnableHotReload = enableHotReload;
    }
}
