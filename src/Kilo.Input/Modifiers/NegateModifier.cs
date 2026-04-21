using System.Numerics;

namespace Kilo.Input.Modifiers;

/// <summary>
/// Inverts input axes. Used for axis inversion preferences.
/// </summary>
public struct NegateModifier : IInputModifier
{
    public bool NegateX;
    public bool NegateY;

    public NegateModifier() { NegateX = true; NegateY = true; }
    public NegateModifier(bool x, bool y) { NegateX = x; NegateY = y; }

    public float ModifyFloat(float value, float deltaTime) => -value;
    public Vector2 ModifyVector2(Vector2 value, float deltaTime) =>
        new(NegateX ? -value.X : value.X, NegateY ? -value.Y : value.Y);
}
