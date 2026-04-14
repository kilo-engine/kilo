using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// World transformation matrix computed from LocalTransform.
/// </summary>
public struct LocalToWorld
{
    /// <summary>The world transformation matrix.</summary>
    public Matrix4x4 Value;
}
