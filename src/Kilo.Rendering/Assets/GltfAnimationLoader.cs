using System.Numerics;
using Kilo.Rendering.Animation;

namespace Kilo.Rendering.Assets;

/// <summary>
/// Loads animation clips from a GLTF model.
/// </summary>
internal static class GltfAnimationLoader
{
    /// <summary>
    /// Loads all animations from a GLTF model.
    /// </summary>
    public static List<AnimationClip> LoadAnimations(
        SharpGLTF.Schema2.ModelRoot model,
        SkeletonData? skeleton)
    {
        var animations = new List<AnimationClip>();

        // Build mapping from node index to joint index
        var nodeToJointIndex = new Dictionary<int, int>();
        if (skeleton != null)
        {
            // Create node name to joint index mapping since we need to match animations to joints
            for (int i = 0; i < skeleton.Joints.Length; i++)
            {
                var jointName = skeleton.Joints[i].Name;
                if (!string.IsNullOrEmpty(jointName))
                {
                    // Try to find the node with this name in the model
                    foreach (var node in model.DefaultScene.VisualChildren)
                    {
                        if (FindNodeByName(node, jointName, out var foundNode))
                        {
                            nodeToJointIndex[foundNode.LogicalIndex] = i;
                            break;
                        }
                    }
                }
            }
        }

        foreach (var gltfAnim in model.LogicalAnimations)
        {
            var clip = new AnimationClip
            {
                Name = gltfAnim.Name ?? $"Animation_{animations.Count}",
                Channels = []
            };

            // Group channels by target node
            var channelsByNode = gltfAnim.Channels
                .Where(c => c.TargetNode != null)
                .GroupBy(c => c.TargetNode);

            foreach (var nodeGroup in channelsByNode)
            {
                var node = nodeGroup.Key;

                // Find which joint this node corresponds to
                int jointIndex = -1;
                if (nodeToJointIndex.TryGetValue(node.LogicalIndex, out int foundJointIndex))
                {
                    jointIndex = foundJointIndex;
                }
                else if (skeleton != null)
                {
                    for (int i = 0; i < skeleton.Joints.Length; i++)
                    {
                        if (skeleton.Joints[i].Name == node.Name)
                        {
                            jointIndex = i;
                            break;
                        }
                    }
                }

                if (jointIndex < 0) continue;

                // Collect keyframes from all property channels for this node
                var allTimes = new HashSet<float>();
                var translationKeys = new List<(float Time, Vector3 Value)>();
                var rotationKeys = new List<(float Time, Quaternion Value)>();
                var scaleKeys = new List<(float Time, Vector3 Value)>();

                foreach (var channel in nodeGroup)
                {
                    switch (channel.TargetNodePath)
                    {
                        case SharpGLTF.Schema2.PropertyPath.translation:
                            var tSampler = channel.GetTranslationSampler();
                            if (tSampler != null)
                            {
                                foreach (var (k, v) in tSampler.GetLinearKeys())
                                {
                                    allTimes.Add(k);
                                    translationKeys.Add((k, v));
                                }
                            }
                            break;
                        case SharpGLTF.Schema2.PropertyPath.rotation:
                            var rSampler = channel.GetRotationSampler();
                            if (rSampler != null)
                            {
                                foreach (var (k, v) in rSampler.GetLinearKeys())
                                {
                                    allTimes.Add(k);
                                    rotationKeys.Add((k, v));
                                }
                            }
                            break;
                        case SharpGLTF.Schema2.PropertyPath.scale:
                            var sSampler = channel.GetScaleSampler();
                            if (sSampler != null)
                            {
                                foreach (var (k, v) in sSampler.GetLinearKeys())
                                {
                                    allTimes.Add(k);
                                    scaleKeys.Add((k, v));
                                }
                            }
                            break;
                    }
                }

                if (allTimes.Count == 0) continue;

                var animChannel = new AnimationChannel
                {
                    JointIndex = jointIndex,
                    Keyframes = []
                };

                foreach (float t in allTimes.OrderBy(x => x))
                {
                    var kf = new AnimationKeyframe
                    {
                        Time = t,
                        Position = InterpolateLinear(translationKeys, t, Vector3.Zero),
                        Rotation = InterpolateRotation(rotationKeys, t, Quaternion.Identity),
                        Scale = InterpolateLinear(scaleKeys, t, Vector3.One)
                    };
                    animChannel.Keyframes.Add(kf);
                }

                clip.Channels.Add(animChannel);
            }

            // Calculate clip duration
            clip.Duration = clip.Channels.SelectMany(ch => ch.Keyframes).Select(kf => kf.Time).DefaultIfEmpty(0).Max();

            if (clip.Channels.Count > 0)
            {
                animations.Add(clip);
            }
        }

        return animations;
    }

    /// <summary>
    /// Recursively finds a node by name in the scene hierarchy.
    /// </summary>
    private static bool FindNodeByName(SharpGLTF.Schema2.Node node, string name, out SharpGLTF.Schema2.Node? found)
    {
        if (node.Name == name)
        {
            found = node;
            return true;
        }

        foreach (var child in node.VisualChildren)
        {
            if (FindNodeByName(child, name, out found))
                return true;
        }

        found = null;
        return false;
    }

    private static Vector3 InterpolateLinear(List<(float Time, Vector3 Value)> keys, float t, Vector3 fallback)
    {
        if (keys.Count == 0) return fallback;
        if (keys.Count == 1) return keys[0].Value;

        // Find surrounding keyframes
        int idx = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].Time <= t) idx = i;
            else break;
        }

        if (idx >= keys.Count - 1) return keys[^1].Value;
        if (keys[idx].Time == t) return keys[idx].Value;

        float dt = keys[idx + 1].Time - keys[idx].Time;
        float alpha = dt > 0 ? (t - keys[idx].Time) / dt : 0f;
        return Vector3.Lerp(keys[idx].Value, keys[idx + 1].Value, alpha);
    }

    private static Quaternion InterpolateRotation(List<(float Time, Quaternion Value)> keys, float t, Quaternion fallback)
    {
        if (keys.Count == 0) return fallback;
        if (keys.Count == 1) return keys[0].Value;

        int idx = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].Time <= t) idx = i;
            else break;
        }

        if (idx >= keys.Count - 1) return keys[^1].Value;
        if (keys[idx].Time == t) return keys[idx].Value;

        float dt = keys[idx + 1].Time - keys[idx].Time;
        float alpha = dt > 0 ? (t - keys[idx].Time) / dt : 0f;
        return Quaternion.Slerp(keys[idx].Value, keys[idx + 1].Value, alpha);
    }
}
