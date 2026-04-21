using System.Numerics;

namespace Kilo.Input.Modifiers;

/// <summary>
/// Multiplies input by a constant factor (sensitivity).
/// </summary>
public struct ScaleModifier : IInputModifier
{
    public float Factor;

    public ScaleModifier() { Factor = 1.0f; }
    public ScaleModifier(float factor) { Factor = factor; }

    public float ModifyFloat(float value, float deltaTime) => value * Factor;
    public Vector2 ModifyVector2(Vector2 value, float deltaTime) => value * Factor;
}
