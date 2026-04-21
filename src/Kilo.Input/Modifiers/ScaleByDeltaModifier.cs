using System.Numerics;

namespace Kilo.Input.Modifiers;

/// <summary>
/// Multiplies input by deltaTime for frame-rate-independent processing.
/// </summary>
public struct ScaleByDeltaModifier : IInputModifier
{
    public float ModifyFloat(float value, float deltaTime) => value * deltaTime;
    public Vector2 ModifyVector2(Vector2 value, float deltaTime) => value * deltaTime;
}
