using Kilo.ECS;

namespace Kilo.Assets;

/// <summary>
/// Plugin that sets up the asset management system.
/// Registers resources and systems for loading and managing game assets.
/// </summary>
public sealed class AssetsPlugin : IKiloPlugin
{
    private readonly AssetSettings _settings;

    /// <summary>
    /// Creates a new AssetsPlugin with optional custom settings.
    /// </summary>
    /// <param name="settings">Optional custom settings. Uses defaults if null.</param>
    public AssetsPlugin(AssetSettings? settings = null)
    {
        _settings = settings ?? new AssetSettings();
    }

    /// <summary>
    /// Builds the plugin by registering resources and systems.
    /// </summary>
    /// <param name="app">The Kilo application to configure.</param>
    public void Build(KiloApp app)
    {
        // Register resources
        app.AddResource(_settings);
        app.AddResource(new AssetManager());

        // Add asset loading system to PreUpdate stage
        app.AddSystem(KiloStage.PreUpdate, new AssetLoadSystem().Update);
    }
}
