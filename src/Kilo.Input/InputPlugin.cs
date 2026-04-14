using Kilo.ECS;

namespace Kilo.Input;

/// <summary>
/// Kilo plugin for input handling.
/// </summary>
public sealed class InputPlugin : IKiloPlugin
{
    /// <summary>
    /// Build the plugin by registering resources and systems.
    /// </summary>
    public void Build(KiloApp app)
    {
        // Register resources
        app.AddResource(new InputState());
        app.AddResource(new InputSettings());

        // Add input polling system to First stage (before any game logic)
        app.AddSystem(KiloStage.First, new InputPollSystem().Update);
    }
}
