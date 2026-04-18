using System.Numerics;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Skeleton component attached to entities with skinned meshes.
/// Stores joint hierarchy data and references to joint transform entities.
/// </summary>
public struct Skeleton
{
    /// <summary>Skeleton data (shared reference).</summary>
    public SkeletonData Data = new();

    /// <summary>
    /// Entity IDs for each joint's LocalTransform.
    /// These are updated by the animation system each frame.
    /// </summary>
    public int[] JointEntities = [];

    public Skeleton() { }
}
