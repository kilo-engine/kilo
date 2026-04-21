using Kilo.ECS;
using Kilo.Input.Contexts;
using Kilo.Input.Systems;

namespace Kilo.Input;

/// <summary>
/// Registers input action mapping resources and systems.
/// Add this plugin after WindowPlugin (which provides InputState).
/// </summary>
public sealed class InputPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddResource(new InputMapStack());
        app.AddResource(new InputMapSystem());
    }
}
