using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// Local transform component with position, rotation, and scale.
/// </summary>
public struct LocalTransform
{
    /// <summary>Position in world space.</summary>
    public Vector3 Position;

    /// <summary>Rotation as a quaternion.</summary>
    public Quaternion Rotation;

    /// <summary>Scale on each axis.</summary>
    public Vector3 Scale;

    /// <summary>Identity transform (no translation, rotation, or scale).</summary>
    public static LocalTransform Identity => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        Scale = Vector3.One
    };
}
