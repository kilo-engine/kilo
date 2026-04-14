using BepuPhysics.Collidables;

namespace Kilo.Physics;

/// <summary>
/// Defines the shape properties of a physics body.
/// Links to a shape in Bepu's shape collection.
/// </summary>
public struct PhysicsShape
{
    /// <summary>Index of the shape in the simulation's shape collection.</summary>
    public TypedIndex ShapeIndex;

    /// <summary>Mass of the body (for dynamic bodies).</summary>
    public float Mass;

    /// <summary>Collision margin/shell thickness for collision detection.</summary>
    public float CollisionMargin;

    /// <summary>Default shape configuration.</summary>
    public static PhysicsShape Default => new()
    {
        ShapeIndex = new TypedIndex(0, 0),
        Mass = 1.0f,
        CollisionMargin = 0.01f
    };
}
