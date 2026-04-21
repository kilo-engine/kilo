using System.Numerics;

namespace Kilo.Input.Modifiers;

/// <summary>
/// Applies dead zone at the binding level.
/// For 1D: remaps [Lower, Upper] → [0, 1].
/// For 2D: radial dead zone — magnitude below Lower becomes zero.
/// </summary>
public struct DeadZoneModifier : IInputModifier
{
    public float Lower;
    public float Upper;

    public DeadZoneModifier() { Lower = 0.1f; Upper = 0.9f; }
    public DeadZoneModifier(float lower, float upper) { Lower = lower; Upper = upper; }

    public float ModifyFloat(float value, float deltaTime)
    {
        if (value < Lower) return 0f;
        if (value > Upper) return 1f;
        return (value - Lower) / (Upper - Lower);
    }

    public Vector2 ModifyVector2(Vector2 value, float deltaTime) =>
        value.Length() < Lower ? Vector2.Zero : value;
}
