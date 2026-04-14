using Kilo.ECS;

namespace Kilo.Assets;

/// <summary>
/// System that processes pending asset loads for entities with AssetReference components.
/// </summary>
public sealed class AssetLoadSystem
{
    /// <summary>
    /// Updates the asset loading system, processing entities with unloaded assets.
    /// This is a placeholder implementation for Phase 3 - actual loading requires
    /// the loader registry which will be implemented in later phases.
    /// </summary>
    /// <param name="world">The Kilo world.</param>
    public void Update(KiloWorld world)
    {
        // TODO: Implement actual asset loading in later phases
        // This will:
        // 1. Query entities with AssetReference where IsLoaded == false
        // 2. Use AssetManager to get the asset
        // 3. If not loaded, use the appropriate IAssetLoader<T> to load it
        // 4. Store the loaded asset in AssetManager
        // 5. Update IsLoaded to true on the AssetReference component
    }
}
