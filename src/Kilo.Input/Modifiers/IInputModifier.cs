using System.Numerics;

namespace Kilo.Input.Modifiers;

/// <summary>
/// Transforms raw input values before they reach triggers.
/// Chain multiple modifiers for complex transformations.
/// Inspired by Unreal's UInputModifier chain.
/// </summary>
public interface IInputModifier
{
    float ModifyFloat(float value, float deltaTime);
    Vector2 ModifyVector2(Vector2 value, float deltaTime);
}
