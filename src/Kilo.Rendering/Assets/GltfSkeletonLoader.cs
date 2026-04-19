using System.Numerics;
using Kilo.Rendering.Animation;

namespace Kilo.Rendering.Assets;

/// <summary>
/// Loads skeleton/joint hierarchy data from a GLTF skin.
/// </summary>
internal static class GltfSkeletonLoader
{
    /// <summary>
    /// Loads skeleton data from a GLTF skin.
    /// </summary>
    public static SkeletonData LoadSkeleton(
        SharpGLTF.Schema2.Skin skin,
        SharpGLTF.Schema2.ModelRoot model)
    {
        var joints = skin.Joints;
        var jointCount = joints.Count;

        var skeletonData = new SkeletonData
        {
            Joints = new JointInfo[jointCount]
        };

        // Build a mapping from node index to joint index
        var nodeToJointIndex = new Dictionary<int, int>();
        for (int i = 0; i < jointCount; i++)
        {
            nodeToJointIndex[joints[i].LogicalIndex] = i;
        }

        // Get inverse bind matrices
        Matrix4x4[]? inverseBindMatrices = null;
        if (skin.InverseBindMatrices != null)
        {
            inverseBindMatrices = skin.InverseBindMatrices.ToArray();
        }

        // Populate joint info
        for (int i = 0; i < jointCount; i++)
        {
            var jointNode = joints[i];
            // Read rest pose local transform from the GLTF node
            var localTf = jointNode.LocalTransform;
            var jointInfo = new JointInfo
            {
                Name = jointNode.Name ?? $"Joint_{i}",
                InverseBindMatrix = inverseBindMatrices != null && i < inverseBindMatrices.Length
                    ? inverseBindMatrices[i]
                    : Matrix4x4.Identity,
                RestPosition = localTf.Translation,
                RestRotation = localTf.Rotation,
                RestScale = localTf.Scale,
            };

            // Find parent joint
            var parent = jointNode.VisualParent;
            while (parent != null)
            {
                if (nodeToJointIndex.TryGetValue(parent.LogicalIndex, out int parentJointIndex))
                {
                    jointInfo.ParentIndex = parentJointIndex;
                    break;
                }
                parent = parent.VisualParent;
            }

            skeletonData.Joints[i] = jointInfo;
        }

        // Compute ancestor correction: world transform of non-joint nodes above root joints.
        for (int i = 0; i < jointCount; i++)
        {
            if (skeletonData.Joints[i].ParentIndex != -1) continue;
            var node = joints[i].VisualParent;
            var ancestorChain = Matrix4x4.Identity;
            while (node != null)
            {
                if (!nodeToJointIndex.ContainsKey(node.LogicalIndex))
                {
                    var local = Matrix4x4.CreateScale(node.LocalTransform.Scale)
                        * Matrix4x4.CreateFromQuaternion(node.LocalTransform.Rotation)
                        * Matrix4x4.CreateTranslation(node.LocalTransform.Translation);
                    ancestorChain = ancestorChain * local;
                }
                node = node.VisualParent;
            }
            skeletonData.AncestorCorrection = ancestorChain;
            break;
        }

        return skeletonData;
    }
}
