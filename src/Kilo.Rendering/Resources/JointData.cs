using System.Numerics;

namespace Kilo.Rendering.Resources;

/// <summary>
/// Data for a single joint in a skeleton.
/// </summary>
public struct JointInfo
{
    /// <summary>Name of the joint.</summary>
    public string Name = "";

    /// <summary>Index of the parent joint (-1 for root).</summary>
    public int ParentIndex = -1;

    /// <summary>Inverse bind matrix: transforms from mesh space to joint local space.</summary>
    public Matrix4x4 InverseBindMatrix = Matrix4x4.Identity;

    /// <summary>Rest pose local translation from GLTF node.</summary>
    public Vector3 RestPosition = Vector3.Zero;

    /// <summary>Rest pose local rotation from GLTF node.</summary>
    public Quaternion RestRotation = Quaternion.Identity;

    /// <summary>Rest pose local scale from GLTF node.</summary>
    public Vector3 RestScale = Vector3.One;

    public JointInfo() { }
}

/// <summary>
/// Complete skeleton data for a skinned mesh.
/// </summary>
public sealed class SkeletonData
{
    public JointInfo[] Joints = [];
    public int JointCount => Joints.Length;

    /// <summary>Maximum number of joints supported in the shader.</summary>
    public const int MaxJoints = 64;

    /// <summary>
    /// World transform of non-joint ancestor nodes above the root joint.
    /// Applied as a prefix to joint world matrices in SkinnedMeshPrepareSystem
    /// so that jointMatrix = AncestorCorrection * jointECSWorld * inverseBindMatrix.
    /// </summary>
    public Matrix4x4 AncestorCorrection = Matrix4x4.Identity;
}
