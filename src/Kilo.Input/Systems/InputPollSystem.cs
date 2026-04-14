using Kilo.ECS;

namespace Kilo.Input;

/// <summary>
/// Polls input from the windowing system and updates InputState.
/// In this version without Silk.NET, just resets frame state.
/// </summary>
public sealed class InputPollSystem
{
    /// <summary>
    /// Update the input state. Without Silk.NET, this just resets per-frame state.
    /// </summary>
    public void Update(KiloWorld world)
    {
        var inputState = world.GetResource<InputState>();
        inputState.ResetFrame();
    }
}
