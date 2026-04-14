using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// Local-space axis-aligned bounding box for frustum culling.
/// </summary>
public struct BoundsLocal
{
    public Vector3 Min;
    public Vector3 Max;

    /// <summary>Default unit cube centered at origin (-0.5 to 0.5).</summary>
    public static BoundsLocal UnitCube => new()
    {
        Min = new Vector3(-0.5f),
        Max = new Vector3(0.5f)
    };
}
